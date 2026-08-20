using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Simulador de etapas de montaña, media montaña y colina (PRD §11, §12):
/// Action Phase, ataques (MNT+ATT+ACC), Pace Check (MNT+STA), descensos (DHI),
/// KoM por categoría y llegada en grupo reducido. Determinista por seed.
/// </summary>
public sealed class MountainStageSimulator
{
    private readonly RulesConfig _cfg;
    private readonly SeededRandom _rng;
    private readonly Dictionary<int, int> _koMByRider = new();

    public MountainStageSimulator(RulesConfig cfg, ulong seed)
    {
        _cfg = cfg;
        _rng = new SeededRandom(seed);
    }

    public List<StageResultRider> Run(RaceState state)
    {
        var stage = state.Stage ?? throw new InvalidOperationException("Sin etapa.");
        if (stage.Sections.Count == 0) throw new InvalidOperationException("Sin secciones.");
        _rng.RestoreState(state.RngState);
        state.CurrentSectionIndex = 0;
        state.KmCovered = 0;
        state.ActionLog.Clear();

        var peloton = state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Peloton)
            ?? throw new InvalidOperationException("Sin pelotón.");

        // Grupo de cabeza (favoritos) que se fragua en las subidas.
        RiderGroup? front = null;

        double kmFront = 0;
        for (int i = 0; i < stage.Sections.Count; i++)
        {
            var sec = stage.Sections[i];
            state.CurrentSectionIndex = i;
            double secLen = sec.LengthKm;
            if (secLen <= 0) continue;
            kmFront = Math.Min(stage.DistanceKm, kmFront + secLen);
            state.KmCovered = kmFront;

            ApplySection(state, sec, secLen, kmFront, ref front);
            if (sec.ClimbId is not null)
                AwardKoM(state, sec, stage);
            if (kmFront >= stage.DistanceKm)
                break;
        }

        state.RngState = _rng.GetState();
        var results = ResolveFinish(state, front, peloton);
        state.Classifications.RegisterStage(results);
        FinalizeTimes(state, results);
        return results;
    }

    private void ApplySection(RaceState state, StageSection sec, double secLen, double kmFront,
        ref RiderGroup? front)
    {
        var terrain = sec.DominantTerrain;

        switch (terrain)
        {
            case Terrain.Climb:
                HandleClimb(state, sec, secLen, kmFront, ref front);
                break;
            case Terrain.Descent:
                HandleDescent(state, sec, secLen, kmFront, ref front);
                break;
            default:
                HandleFlatish(state, sec, secLen, kmFront, terrain, ref front);
                break;
        }
    }

    /// <summary>En la subida: Action Phase + ataques + Pace Check. Se abre hueco clave.</summary>
    private void HandleClimb(RaceState state, StageSection sec, double secLen, double kmFront,
        ref RiderGroup? front)
    {
        // Rendimiento de subida: MNT+ATT+ACC (ataque) o MNT+STA (pace).
        var climbs = state.RiderStates
            .Where(s => s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s.RiderId];
                double atk = BlendFor(r, "climb_attack") *
                    (1 - FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                        r.Attributes.Resistance, _cfg));
                double pace = BlendFor(r, "pace_check") *
                    (1 - FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                        r.Attributes.Resistance, _cfg));
                return (Id: r.Id, Rider: r, State: s, Attack: atk, Pace: pace);
            })
            .ToList();

        // Pace Check: cuántos pueden mantener el ritmo del grupo de cabeza.
        double frontPace = climbs.OrderByDescending(c => c.Pace).Take(8).Average(c => c.Pace);
        double sustainAbove = frontPace * 0.985;

        // Si no hay grupo de cabeza, los 6 mejores por ataque lo forman en el primer puerto.
        if (front is null)
        {
            var attackers = climbs.OrderByDescending(c => c.Attack).Take(6).ToList();
            front = NewGroup(state, GroupKind.SmallGroup, attackers.Select(c => c.Id).ToList(),
                gap: 0);
            foreach (var c in attackers)
                state.ActionLog.Add($"[PCRM] Ataque en el puerto: {Name(state, c.Id)}");
        }

        // Ataques recurrentes: un favorito que se va solo si su ataque >> pace del grupo.
        var atFront = front.MemberRiderIds.Select(id => climbs.First(c => c.Id == id)).ToList();
        double paceFront = atFront.Count > 0 ? atFront.Average(c => c.Pace) : frontPace;
        var soloCandidates = atFront.Where(c => c.Attack > paceFront * 1.04)
            .OrderByDescending(c => c.Attack)
            .Take(1)
            .ToList();
        if (soloCandidates.Count > 0 && atFront.Count > 1)
        {
            var solo = soloCandidates[0];
            front = NewGroup(state, GroupKind.LoneRider, new List<int> { solo.Id }, gap: 8);
            state.ActionLog.Add($"[PCRM] Va solo en cabeza: {Name(state, solo.Id)}");
        }

        // El pelotón responde: los que no aguantan "el ritmo de cabeza" pierden tiempo.
        double dt = SecondsForKm(secLen, ClimbSpeed(_cfg, paceFront));
        double dtDrop = SecondsForKm(secLen, ClimbSpeed(_cfg, sustainAbove));
        double dtSlow = SecondsForKm(secLen, ClimbSpeed(_cfg, paceFront * 0.9));

        foreach (var c in climbs)
        {
            double dtHere = c.Pace >= sustainAbove ? dt : dtSlow;
            if (front is not null && front.MemberRiderIds.Contains(c.Id))
                dtHere = dt;
            c.State.StageTimeSeconds += dtHere;
            c.State.StageElevationMeters += secLen * Math.Max(0, sec.GradientPct) * 10;
            c.State.Fatigue = FatigueCalculator.AddFatigue(c.State.Fatigue, secLen,
                secLen * Math.Max(0, sec.GradientPct) * 10,
                c.Rider.HasSpecialty(RiderSpecialty.Climber) ? 0.6 : 0.2,
                c.Rider.Attributes.Endurance, c.Rider.Attributes.Resistance, _cfg);
        }

        front.GapSeconds = 0;
        state.ActionLog.Add($"[PCRM] Subida: {sec.LengthKm:0.0} km al {sec.GradientPct:0.0}% " +
            $"(pace {paceFront:0.0}, grupo de cabeza {front.MemberRiderIds.Count})");
    }

    /// <summary>Descenso: DHI decide si se recobra o se pierde tiempo.</summary>
    private void HandleDescent(RaceState state, StageSection sec, double secLen, double kmFront,
        ref RiderGroup? front)
    {
        if (front is null) { HandleFlatish(state, sec, secLen, kmFront, Terrain.Flat, ref front); return; }

        double dt = SecondsForKm(secLen, DescentSpeed(_cfg));
        foreach (var rs in state.RiderStates)
        {
            if (rs.Status != RiderStatus.Active) continue;
            var r = state.Riders[rs.RiderId];
            double dhi = r.Attributes.Descent;
            double noise = 1 + (_rng.NextDouble() - _cfg.RngNoiseCenter) * 2 * _cfg.RngNoiseRange;
            double bonus = (dhi - _cfg.GvRef) * 0.02 * noise;
            rs.StageTimeSeconds += dt * (1 - bonus);
            rs.Fatigue = FatigueCalculator.AddFatigue(rs.Fatigue, secLen, 0, 0,
                r.Attributes.Endurance, r.Attributes.Resistance, _cfg);
        }
        front.GapSeconds = Math.Max(0, front.GapSeconds - 15);
        state.ActionLog.Add($"[PCRM] Descenso: DHI decide, hueco recortado.");
    }

    /// <summary>Tramo llano/ondulado: velocidad por GV como en llano.</summary>
    private void HandleFlatish(RaceState state, StageSection sec, double secLen, double kmFront,
        Terrain terrain, ref RiderGroup? front)
    {
        string key = terrain switch
        {
            Terrain.Rolling => "rolling", Terrain.Hill => "hill", _ => "flat"
        };
        double baseSpeed = _cfg.TerrainBaseSpeedKmh.TryGetValue(key, out var b) ? b : 40;
        double pSpeed = EstimateAvgSpeed(state, key);
        double dt = SecondsForKm(secLen, baseSpeed > 0 ? Math.Max(45, pSpeed) : pSpeed);

        foreach (var rs in state.RiderStates)
        {
            if (rs.Status != RiderStatus.Active) continue;
            var r = state.Riders[rs.RiderId];
            rs.StageTimeSeconds += dt;
            rs.StageElevationMeters += secLen * Math.Max(0, sec.GradientPct) * 10;
            rs.Fatigue = FatigueCalculator.AddFatigue(rs.Fatigue, secLen,
                secLen * Math.Max(0, sec.GradientPct) * 10, 0,
                r.Attributes.Endurance, r.Attributes.Resistance, _cfg);
        }
    }

    private double EstimateAvgSpeed(RaceState state, string key)
    {
        var values = state.RiderStates
            .Where(s => s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s.RiderId];
                double raw = BlendFor(r, "flat_tempo");
                double penalty = FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                    r.Attributes.Resistance, _cfg);
                return raw * (1 - penalty);
            })
            .OrderByDescending(v => v)
            .Take(8)
            .DefaultIfEmpty(_cfg.GvRef)
            .Average();
        double baseSpeed = _cfg.TerrainBaseSpeedKmh.TryGetValue(key, out var b) ? b : 40;
        return Math.Max(25, baseSpeed + (values - _cfg.GvRef) * _cfg.GvKmhPerPoint);
    }

    /// <summary>Puntos KoM por categoría (tabla configurable por categoría).</summary>
    private void AwardKoM(RaceState state, StageSection sec, Stage stage)
    {
        var climb = stage.Climbs.FirstOrDefault(c => c.Id == sec.ClimbId);
        if (climb is null) return;
        int[] table = climb.KoM_Points.Length > 0 ? climb.KoM_Points : CatTable(climb.Category);

        // Orden en la cumbre: los mejores de MNT cruzaron antes (tiempos acumulados).
        var order = state.RiderStates
            .Where(s => s.Status == RiderStatus.Active)
            .OrderBy(s => s.StageTimeSeconds)
            .Take(Math.Min(table.Length, 10))
            .ToList();

        for (int i = 0; i < order.Count; i++)
        {
            int pts = table[i];
            _koMByRider.TryGetValue(order[i].RiderId, out var acc);
            _koMByRider[order[i].RiderId] = acc + pts;
            state.ActionLog.Add($"[PCRM] KoM {climb.Name}: {Name(state, order[i].RiderId)} +{pts} ptos");
        }
    }

    private List<StageResultRider> ResolveFinish(RaceState state, RiderGroup? front, RiderGroup peloton)
    {
        // Llegada: grupo reducido (ACC+ATT+HIL o MNT según el final) sobre la cabeza.
        var pool = front is not null ? front.MemberRiderIds
            : peloton.MemberRiderIds.Take(30).ToList();

        var ranked = pool
            .Select(id => state.RiderStates.First(s => s.RiderId == id))
            .Where(s => s.Status == RiderStatus.Active)
            .Select(s =>
            {
                var r = state.Riders[s.RiderId];
                string profile = r.HasSpecialty(RiderSpecialty.Climber) ? "pace_check" : "sprint_reduced";
                double raw = BlendFor(r, profile);
                double per = raw * (1 - FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
                    r.Attributes.Resistance, _cfg)) *
                    (1 + (_rng.NextDouble() - _cfg.RngNoiseCenter) * 2 * _cfg.RngNoiseRange);
                return (Id: r.Id, TeamId: r.TeamId, Rider: r, State: s, Perf: per);
            })
            .OrderByDescending(x => x.Perf)
            .ToList();

        var results = new List<StageResultRider>();
        var done = new HashSet<int>();
        int n = Math.Min(30, ranked.Count);
        for (int i = 0; i < n; i++)
        {
            var c = ranked[i];
            _koMByRider.TryGetValue(c.Id, out var kom);
            results.Add(new StageResultRider(c.Id, c.TeamId,
                c.State.StageTimeSeconds + i * 0.9,
                i < 12 ? FlatStageSimulator.PointsFor(i) : 0, kom, c.Rider.IsYoungFor(2026)));
            done.Add(c.Id);
        }
        foreach (var s in state.RiderStates.Where(s => s.Status == RiderStatus.Active))
        {
            if (done.Contains(s.RiderId)) continue;
            _koMByRider.TryGetValue(s.RiderId, out var kom);
            results.Add(new StageResultRider(s.RiderId, state.Riders[s.RiderId].TeamId,
                s.StageTimeSeconds + 120 + (s.RiderId % 7) * 6, 0, kom,
                state.Riders[s.RiderId].IsYoungFor(2026)));
        }
        return results;
    }

    private RiderGroup NewGroup(RaceState state, GroupKind kind, List<int> ids, double gap)
    {
        int gid = state.Groups.Max(g => g.Id) + 1;
        var group = new RiderGroup { Id = gid, Kind = kind, MemberRiderIds = ids, GapSeconds = gap };
        foreach (var id in ids)
        {
            var rs = state.RiderStates.First(s => s.RiderId == id);
            rs.GroupId = gid;
            foreach (var existing in state.Groups.Where(g => g.MemberRiderIds.Contains(id)).ToList())
                existing.MemberRiderIds.Remove(id);
        }
        state.Groups.Add(group);
        return group;
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

    private static double ClimbSpeed(RulesConfig cfg, double pace) =>
        Math.Max(15, (cfg.TerrainBaseSpeedKmh.TryGetValue("climb", out var b) ? b : 29)
            + (pace - cfg.GvRef) * cfg.GvKmhPerPoint * 0.6);

    private static double DescentSpeed(RulesConfig cfg) =>
        cfg.TerrainBaseSpeedKmh.TryGetValue("descent", out var d) ? d : 52;

    internal static int[] CatTable(int category) => category switch
    {
        4 => new[] { 2, 1 },
        3 => new[] { 5, 3, 2, 1 },
        2 => new[] { 7, 5, 3, 2, 1 },
        1 => new[] { 10, 8, 6, 4, 2, 1 },
        _ => new[] { 20, 15, 12, 10, 8, 6, 4, 2 }
    };

    internal static string Name(RaceState state, int id) =>
        state.Riders.TryGetValue(id, out var r) && !string.IsNullOrEmpty(r.Name) ? r.Name : $"#{id}";

    private static double SecondsForKm(double km, double speedKmh) =>
        speedKmh <= 0 ? 0 : km / speedKmh * 3600;

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
}