using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Simulador de etapa llana (PRD §7, §18, §19): recorre las secciones de la etapa,
/// gestiona fuga temprana + persecución con gap objetivo + sprint masivo.
/// Determinista por seed (PRD §32).
/// </summary>
public sealed class FlatStageSimulator
{
    private readonly RulesConfig _cfg;
    private readonly SeededRandom _rng;

    public FlatStageSimulator(RulesConfig cfg, ulong seed)
    {
        _cfg = cfg;
        _rng = new SeededRandom(seed);
    }

    public List<StageResultRider> Run(RaceState state)
    {
        if (state.Stage is null || state.Stage.Sections.Count == 0)
            throw new InvalidOperationException("La etapa no tiene secciones.");

        var stage = state.Stage;
        _rng.RestoreState(state.RngState);
        state.CurrentSectionIndex = 0;
        state.KmCovered = 0;
        state.ActionLog.Clear();

        var peloton = state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Peloton);
        if (peloton is null) throw new InvalidOperationException("Estado sin pelotón inicial.");

        // Fase 1 — fuga temprana.
        var breakaway = TryFormBreakaway(state, peloton);

        // IA del pelotón (Director Mode: decisiones automáticas y registradas).
        var ia = new RaceDecisionEngine(_cfg, state);

        // Fase 2 — recorrido por secciones.
        double kmFront = 0;
        for (int i = 0; i < stage.Sections.Count; i++)
        {
            var sec = stage.Sections[i];
            state.CurrentSectionIndex = i;
            double secLen = sec.LengthKm;
            if (secLen <= 0) continue;
            kmFront = Math.Min(stage.DistanceKm, kmFront + secLen);
            state.KmCovered = kmFront;

            bool survivalRoll = breakaway is not null &&
                _rng.NextDouble() < _cfg.BreakawaySurvivalChance;
            ApplySection(state, sec, secLen, kmFront, breakaway, survivalRoll, ia, i, stage.Sections.Count);

            if (sec.IntermediateSprint is { } sprint && sprint.Km > 0 && kmFront >= sprint.Km)
                AwardIntermediateSprint(state, sprint);
        }

        state.RngState = _rng.GetState();

        // Fase 3 — llegada: fuga aguantada o sprint masivo.
        bool survived = breakaway is not null && breakaway.GapSeconds > 30;
        var results = survived
            ? ResolveSurvivingBreakaway(state, breakaway!)
            : ResolveMassSprint(state, peloton);

        state.Classifications.RegisterStage(results);
        FinalizeTimes(state, results);
        return results;
    }

    // ---------- Fase 1: fuga ----------

    private RiderGroup? TryFormBreakaway(RaceState state, RiderGroup peloton)
    {
        int count = _rng.Next(_cfg.BreakawayMinSize, _cfg.BreakawayMaxSize + 1);
        if (count <= 0) return null;

        var active = state.RiderStates.Where(s => s.Status == RiderStatus.Active).ToList();
        var scores = active.Select(s =>
        {
            var r = state.Riders[s.RiderId];
            if (r.HasSpecialty(RiderSpecialty.Sprinter)) return (s.RiderId, 0.0);
            double penalty = FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                r.Attributes.Resistance, _cfg);
            double raw = BlendFor(r, "breakaway_flat");
            return (s.RiderId, raw * (1 - penalty));
        }).Where(x => x.Item2 > 55).ToList();

        if (scores.Count == 0) return null;

        var pool = scores.ToList();
        var chosen = new List<int>();
        while (chosen.Count < count && pool.Count > 0)
        {
            double total = pool.Sum(x => x.Item2);
            double pick = _rng.NextDouble() * total;
            int idx = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                pick -= pool[i].Item2;
                if (pick <= 0) { idx = i; break; }
            }
            chosen.Add(pool[idx].Item1);
            pool.RemoveAt(idx);
        }
        if (chosen.Count < _cfg.BreakawayMinSize) return null;

        int groupId = state.Groups.Max(g => g.Id) + 1;
        var group = new RiderGroup
        {
            Id = groupId,
            Kind = GroupKind.Breakaway,
            MemberRiderIds = chosen,
            Cohesion = 0.85,
            GapSeconds = 0,
            SpeedKmh = _cfg.TerrainBaseSpeedKmh["flat"]
        };
        foreach (var id in chosen)
        {
            state.RiderStates.First(s => s.RiderId == id).GroupId = groupId;
            state.ActionLog.Add($"[PCRM] Fuga: {Name(state, id)}");
        }
        peloton.MemberRiderIds.RemoveAll(id => chosen.Contains(id));
        state.Groups.Add(group);
        return group;
    }

    // ---------- Fase 2: avance por sección ----------

    private void ApplySection(RaceState state, StageSection sec, double secLen, double kmFront,
        RiderGroup? breakaway, bool breakawaySurvives, RaceDecisionEngine ia,
        int sectionIndex, int totalSections)
    {
        bool hasBreakaway = breakaway is not null;
        double pSpeed = EstimateSpeed(state, pelotonIds(state), _cfg.WorkingRidersPeloton, sec, "flat_tempo");
        double bSpeed = hasBreakaway
            ? EstimateSpeed(state, breakaway!.MemberRiderIds, _cfg.WorkingRidersBreakaway, sec, "breakaway_flat")
            : 0;

        // Gap objetivo: si la fuga va a aguantar hasta meta su valor no baja (dramaturgia de caza fallida);
        // en caso normal, el pelotón persigue hasta neutralizar antes de la meta.
        double target = 0;
        if (hasBreakaway)
        {
            if (breakawaySurvives)
                target = _cfg.BreakawayMaxGapSeconds * 0.6;
            else
                target = TargetGap(kmFront, state.Stage!.DistanceKm);
        }

        // El pelotón ajusta su ritmo: persecución dirigida por la IA + acercamiento al gap objetivo.
        double speedDeltaPeloton = 0;
        if (hasBreakaway)
        {
            // Solo emitimos decisión de IA una vez por sección límite (evita spam).
            if (sectionIndex == 0 || (target > 0 && breakaway!.GapSeconds < 90 && sectionIndex > totalSections / 2))
            {
                double chase = ia.ApplyChase(state, breakaway, kmFront, state.Stage!.DistanceKm);
                speedDeltaPeloton = chase;
            }
            double err = target - breakaway!.GapSeconds;
            speedDeltaPeloton = Math.Clamp(speedDeltaPeloton + err * _cfg.GapCorrectionFactor, -8, 8);
        }

        double dtBreak = hasBreakaway ? SecondsForKm(secLen, bSpeed) : 0;
        double dtPelo = SecondsForKm(secLen, Math.Max(20, pSpeed + speedDeltaPeloton));

        foreach (var rs in state.RiderStates)
        {
            if (rs.Status != RiderStatus.Active) continue;
            var r = state.Riders[rs.RiderId];
            bool inBreak = rs.GroupId == breakaway?.Id;
            double dt = inBreak ? dtBreak : dtPelo;
            double elevM = secLen * Math.Max(0, sec.GradientPct) * 10;
            double effort = inBreak ? _cfg.BreakawayEffortFatigue : 0;

            rs.StageTimeSeconds += dt;
            rs.StageElevationMeters += elevM;
            rs.StageEffortScore += effort;
            rs.Fatigue = FatigueCalculator.AddFatigue(rs.Fatigue, secLen, elevM, effort,
                r.Attributes.Endurance, r.Attributes.Resistance, _cfg);
        }

        if (hasBreakaway)
        {
            // Gap real: diferencia acumulada de tiempos entre los dos grupos.
            breakaway!.GapSeconds = Math.Max(0,
                breakaway.GapSeconds + (dtPelo - dtBreak));
            // La persecución se modera según el objetivo: dejamos crecer o cazamos.
            double diff = dtPelo - dtBreak;
            double adj = Math.Clamp(diff * 0.5 + (target - breakaway.GapSeconds) * 0.02, -3, 3);
            breakaway.GapSeconds = Math.Max(0, breakaway.GapSeconds - adj);
            breakaway.FrontKmPos = kmFront;
            breakaway.SpeedKmh = bSpeed;
        }

        state.ActionLog.Add($"[PCRM] km {kmFront:0.0} · pelotón {pSpeed + speedDeltaPeloton:0.0} km/h · fuga gap {breakaway?.GapSeconds ?? 0:0} s");
    }

    private static List<int> pelotonIds(RaceState state) =>
        state.RiderStates.Where(s => s.Status == RiderStatus.Active).Select(s => s.RiderId).ToList();

    private double EstimateSpeed(RaceState state, IEnumerable<int> memberIds, double workers,
        StageSection sec, string profile)
    {
        var values = memberIds
            .Select(id => state.RiderStates.FirstOrDefault(s => s.RiderId == id))
            .Where(s => s is not null && s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s!.RiderId];
                double raw = BlendFor(r, profile);
                double penalty = FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                    r.Attributes.Resistance, _cfg);
                return raw * (1 - penalty);
            })
            .OrderByDescending(v => v)
            .ToList();
        double avg = values.Count == 0 ? _cfg.GvRef : values.Take((int)Math.Clamp(workers, 1, values.Count)).Average();
        string key = TerrainKey(sec.DominantTerrain);
        double baseSpeed = _cfg.TerrainBaseSpeedKmh.TryGetValue(key, out var b) ? b : 40;
        return Math.Max(20, baseSpeed + (avg - _cfg.GvRef) * _cfg.GvKmhPerPoint);
    }

    private double BlendFor(Rider r, string profile)
    {
        double sum = 0, wsum = 0;
        foreach (var (att, w) in _cfg.WeightsFor(profile))
        {
            if (w <= 0) continue;
            sum += r.Attributes.Get(att) * w;
            wsum += w;
        }
        return wsum > 0 ? sum / wsum : 50;
    }

    private static string TerrainKey(Terrain t) => t switch
    {
        Terrain.Flat => "flat", Terrain.Rolling => "rolling", Terrain.Hill => "hill",
        Terrain.Climb => "climb", Terrain.Descent => "descent",
        Terrain.Cobbles => "cobbles", Terrain.TimeTrial => "tt", _ => "flat"
    };

    private double TargetGap(double kmFront, double stageKm)
    {
        double catchKm = Math.Max(1, stageKm - _cfg.CatchPointKmFromFinish);
        if (kmFront >= catchKm) return 0;
        double envelope = Math.Sin(Math.PI * Math.Min(1, kmFront / catchKm));
        return _cfg.BreakawayMaxGapSeconds * envelope;
    }

    private double AdjustForTarget(RiderGroup? breakaway, double target)
    {
        if (breakaway is null || target <= 0) return 0;
        double delta = (target - breakaway.GapSeconds) * _cfg.GapCorrectionFactor;
        return Math.Clamp(delta, -8, 8);
    }

    private static double SecondsForKm(double km, double speedKmh) =>
        speedKmh <= 0 ? 0 : km / speedKmh * 3600;

    // ---------- Sprint intermedio ----------

    private void AwardIntermediateSprint(RaceState state, SprintInfo sprint)
    {
        var group = state.Groups
            .Where(g => g.GapSeconds < _cfg.IntermediateSprintGroupBonus)
            .OrderBy(g => g.GapSeconds)
            .FirstOrDefault();
        if (group is null) return;

        var contenders = group.MemberRiderIds
            .Select(id => state.RiderStates.First(s => s.RiderId == id))
            .Where(s => s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s.RiderId];
                double per = BlendFor(r, "sprint_massive") *
                    (1 - FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                        r.Attributes.Resistance, _cfg));
                return (r.Id, Perf: per);
            })
            .OrderByDescending(x => x.Perf)
            .Take(Math.Min(10, sprint.Points.Length))
            .ToList();

        for (int i = 0; i < contenders.Count; i++)
            state.ActionLog.Add($"[PCRM] Sprint {sprint.Km:0.0} km: {Name(state, contenders[i].Id)} +{sprint.Points[i]} ptos");
    }

    // ---------- Fase 3: llegada ----------

    private List<StageResultRider> ResolveMassSprint(RaceState state, RiderGroup peloton)
    {
        // Tras la caza, todos los activos (incluidos los ex-fugados) disputan la llegada.
        var contenders = state.RiderStates
            .Where(s => s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s.RiderId];
                double per = BlendFor(r, "sprint_massive") *
                    (1 - FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                        r.Attributes.Resistance, _cfg));
                double noise = 1 + (_rng.NextDouble() - _cfg.RngNoiseCenter) * 2 * _cfg.RngNoiseRange;
                return (r.Id, TeamId: r.TeamId, Rider: r, State: s, Perf: per * noise);
            })
            .OrderByDescending(x => x.Perf)
            .ToList();

        var results = new List<StageResultRider>();
        var done = new HashSet<int>();
        int n = Math.Min(_cfg.SprintContenders, contenders.Count);
        for (int i = 0; i < n; i++)
        {
            var c = contenders[i];
            results.Add(new StageResultRider(c.Rider.Id, c.TeamId,
                c.State.StageTimeSeconds + i * _cfg.SprintTimeIncrement,
                i < 16 ? PointsFor(i) : 0, 0, c.Rider.IsYoungFor(2026)));
            done.Add(c.Rider.Id);
        }
        foreach (var rs in state.RiderStates.Where(s => s.Status == RiderStatus.Active))
        {
            if (done.Contains(rs.RiderId)) continue;
            var r = state.Riders[rs.RiderId];
            results.Add(new StageResultRider(r.Id, r.TeamId,
                rs.StageTimeSeconds + _cfg.SprintTimeIncrement, 0, 0, r.IsYoungFor(2026)));
        }
        return results;
    }

    private List<StageResultRider> ResolveSurvivingBreakaway(RaceState state, RiderGroup away)
    {
        var finishers = away.MemberRiderIds
            .Select(id => (R: state.Riders[id],
                S: state.RiderStates.First(s => s.RiderId == id)))
            .OrderBy(x => x.S.StageTimeSeconds)
            .ToList();
        var results = new List<StageResultRider>();
        for (int i = 0; i < finishers.Count; i++)
        {
            results.Add(new StageResultRider(finishers[i].R.Id, finishers[i].R.TeamId,
                finishers[i].S.StageTimeSeconds + i * 2, i == 0 ? 50 : 0, 0,
                finishers[i].R.IsYoungFor(2026)));
        }
        return results;
    }

    // ---------- Finalización ----------

    private static void FinalizeTimes(RaceState state, List<StageResultRider> results)
    {
        foreach (var rs in state.RiderStates)
        {
            rs.Status = RiderStatus.Finished;
            rs.StageRiddenSeconds = rs.StageTimeSeconds;
        }
        foreach (var r in results)
        {
            var rs = state.RiderStates.First(s => s.RiderId == r.RiderId);
            rs.StageTimeSeconds = r.StageSeconds;
            rs.GcSeconds = r.StageSeconds;
        }
    }

    internal static string Name(RaceState state, int id)
    {
        if (state.Riders.TryGetValue(id, out var r) && !string.IsNullOrEmpty(r.Name)) return r.Name;
        return $"#{id}";
    }

    internal static int PointsFor(int position) => position switch
    {
        0 => 50, 1 => 30, 2 => 20, 3 => 18, 4 => 16, 5 => 14, 6 => 12,
        7 => 10, 8 => 8, 9 => 7, 10 => 6, 11 => 5, 12 => 4, 13 => 3,
        14 => 2, 15 => 1, _ => 0
    };
}