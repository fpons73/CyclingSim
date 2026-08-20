using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

public enum DirectorMode
{
    Player,     // las decisiones del equipo del jugador pausan y muestran opciones
    Assistant,  // sugerencias, requerimiento táctico es manual
    Directed    // auto-ejecución de todas las decisiones (PRD §23)
}

public enum ChaseIntensity
{
    None,   // no se persigue (la fuga puede crecer / aguantar)
    Light,  // ritmo normal
    Strong  // equipos de sprinter/GC persiguen con dureza
}

/// <summary>Las 12 decisiones tácticas de la IA (PRD §21).</summary>
public enum TacticalDecisionKind
{
    Attack,          // atacar
    FollowAttack,    // seguir un ataque
    Chase,           // perseguir
    MaintainPace,    // mantener ritmo
    JoinBreakaway,   // entrar en fuga
    ProtectLeader,   // proteger líder
    SaveEnergy,      // ahorrar energía
    LaunchSprint,    // lanzar sprint
    ContestKoM,      // disputar KoM
    ControlPack,     // controlar pelotón
    Counterattack,   // realizar contraataque
    RiskDescent      // asumir riesgos en descenso
}

/// <summary>Decisión táctica emitida por la IA: qué se decide, por qué equipo y por qué.</summary>
public sealed record TacticalDecision(
    TacticalDecisionKind Kind,
    int TeamId,
    string Reason)
{
    /// <summary>El corredor del equipo implicado en la decisión (o null si es táctica de equipo).</summary>
    public int? RiderId { get; init; }
}

/// <summary>
/// IA táctica del pelotón (PRD §21, §23): decide las 12 acciones del PRD usando
/// exactamente los mismos atributos y reglas que el jugador (no existe un sistema
/// de estadísticas paralelo). La calidad de la decisión depende de la lógica de IA,
/// nunca de modificar atributos artificialmente.
///
/// Las decisiones se emiten por equipo según su objetivo (sprint, GC, fuga, KoM, etc.)
/// y la situación de carrera (terreno, km restantes, fuga, fatiga, clasificación).
/// Los simuladores (FlatStageSimulator, MountainStageSimulator) consumen estas
/// decisiones para ejecutar la carrera con Director Mode automático (PRD §23).
/// </summary>
public sealed class RaceDecisionEngine
{
    private readonly RulesConfig _cfg;
    private readonly RaceState _state;
    private readonly Dictionary<int, Team> _teams;
    private readonly Dictionary<int, Rider> _riders;
    private readonly Dictionary<int, RiderState> _states;

    public DirectorMode Mode { get; }

    // --- Objetivos por equipo (derivados de la plantilla + etapa, no de estadísticas paralelas) ---
    private readonly Dictionary<int, TeamTactic> _tactics;

    /// <summary>Perfil táctico derivado de la plantilla del equipo para la etapa.</summary>
    public sealed class TeamTactic
    {
        public int TeamId { get; init; }
        /// <summary>Esprinter principal del equipo (si existe).</summary>
        public int? SprinterId { get; init; }
        /// <summary>Líder de GC / escalador principal del equipo.</summary>
        public int? GCLeaderId { get; init; }
        /// <summary>Escalador de referencia (puede cazar KoM aunque no sea de nivel de GC).</summary>
        public int? ClimberId { get; init; }
        /// <summary>Puncheur/lanzador (medios puertos, llegadas reducidas).</summary>
        public int? PuncheurId { get; init; }
        /// <summary>Rol dominante del equipo en esta etapa.</summary>
        public TeamRole PrimaryRole { get; init; }
        /// <summary>Si el equipo tiene algo que disputar hoy (sprint, GC, etapa).</summary>
        public bool HasAGoal { get; init; }
    }

    public enum TeamRole
    {
        Sprinter,       // busca la llegada masiva
        Gc,             // controla el pelotón y defiende/gana tiempo en la general
        Puncheur,       // llega bien en reducido / disputa la etapa en final duro
        Breakaway,      // sin carta: busca la fuga del día
        KOMHunter,      // sin carta clara: caza los puntos de montaña
        Neutral         // sin plan (suelo: solo ayudar)
    }

    public RaceDecisionEngine(RulesConfig cfg, RaceState state, DirectorMode mode = DirectorMode.Directed)
    {
        _cfg = cfg;
        Mode = mode;
        _state = state;
        _teams = state.Teams;
        _riders = state.Riders;
        _states = state.RiderStates.ToDictionary(s => s.RiderId);
        _tactics = BuildTactics();
    }

    // ---------- Perfil de equipo ----------

    private Dictionary<int, TeamTactic> BuildTactics()
    {
        var result = new Dictionary<int, TeamTactic>();
        var stage = _state.Stage;
        var byTeam = _riders.Values.GroupBy(r => r.TeamId);

        foreach (var g in byTeam)
        {
            int teamId = g.Key;
            var members = g.ToList();

            int? spr = BestId(members, "sprint_massive", .70, min: 72);
            int? gc = BestId(members, "climb_attack", .70, min: 70);
            int? punc = BestId(members, "sprint_explosive", .60, min: 70);
            int? climber = BestId(members, "climb_attack", .40, min: 58);

            TeamRole role;
            if (stage is null)
                role = TeamRole.Neutral;
            else if (stage.Type is StageType.Flat or StageType.FlatCobbles or StageType.Crosswind)
                role = spr is not null ? TeamRole.Sprinter
                     : gc is not null ? TeamRole.Gc
                     : TeamRole.Breakaway;
            else if (stage.Type is StageType.Mountain)
                role = gc is not null ? TeamRole.Gc
                     : TeamRole.KOMHunter;
            else if (stage.Type is StageType.MediumMountain)
                role = gc is not null ? TeamRole.Gc
                     : punc is not null ? TeamRole.Puncheur
                     : TeamRole.KOMHunter;
            else if (stage.Type is StageType.IndividualTimeTrial or StageType.TeamTimeTrial or StageType.Prologue)
                role = TeamRole.Neutral;   // en TTT/CRI no hay táctica colectiva real
            else
                role = spr is not null ? TeamRole.Sprinter : TeamRole.Breakaway;

            result[teamId] = new TeamTactic
            {
                TeamId = teamId,
                SprinterId = spr,
                GCLeaderId = gc,
                ClimberId = climber,
                PuncheurId = punc,
                PrimaryRole = role,
                HasAGoal = spr is not null || gc is not null || punc is not null || climber is not null
            };
        }
        return result;
    }

    private int? BestId(List<Rider> members, string profile, double minWeight, int min)
    {
        Rider? best = null;
        double bestScore = 0;
        foreach (var r in members)
        {
            double score = BlendFor(r, profile);
            if (score > bestScore && score >= min)
            {
                bestScore = score;
                best = r;
            }
        }
        return best?.Id;
    }

    public TeamTactic? TacticFor(int teamId) =>
        _tactics.TryGetValue(teamId, out var t) ? t : null;

    /// <summary>Los objetivos de todos los equipos (para debug/UI).</summary>
    public IReadOnlyList<TeamTactic> Tactics => _tactics.Values.ToList();

    public Rider? RiderOf(int id) => _riders.TryGetValue(id, out var r) ? r : null;
    public RiderState? StateOf(int id) => _states.TryGetValue(id, out var s) ? s : null;

    // ---------- Evaluación de la situación ----------

    /// <summary>
    /// Evalúa la situación y emite todas las decisiones tácticas relevantes.
    /// Se llama en cada sección; los simuladores aplican las que les corresponden
    /// según terreno y fase (fuga, subida, descenso, llegada).
    /// </summary>
    public IReadOnlyList<TacticalDecision> Evaluate(RaceState state)
    {
        var stage = state.Stage!;
        var section = stage.Sections[state.CurrentSectionIndex];
        double kmFront = state.KmCovered;
        double kmToFinish = Math.Max(0, stage.DistanceKm - kmFront);
        var breakaway = state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Breakaway);

        var decisions = new List<TacticalDecision>();
        bool onClimb = section.DominantTerrain == Terrain.Climb;
        bool onDescent = section.DominantTerrain == Terrain.Descent;
        bool finishing = kmToFinish <= 15;

        // Fase de fuga (km inicial): equipos sin carta buscan entrar en la fuga.
        foreach (var t in _tactics.Values)
        {
            if (t.PrimaryRole != TeamRole.Breakaway && t.PrimaryRole != TeamRole.KOMHunter) continue;
            Add(decisions, TacticalDecisionKind.JoinBreakaway, t.TeamId,
                $"sin carta para el final de hoy; buscan la fuga del día.");
        }

        // Fase media: quien persigue la fuga.
        foreach (var t in _tactics.Values)
        {
            if (!t.HasAGoal) continue;
            bool stageNeedsCatch = stage.Type is StageType.Flat or StageType.FlatCobbles or StageType.Crosswind;
            if (t.PrimaryRole == TeamRole.Sprinter && stageNeedsCatch && kmToFinish <= 50)
                Add(decisions, TacticalDecisionKind.Chase, t.TeamId,
                    $"equipo de sprint: la fuga amenaza el sprint masivo.");
            else if (t.PrimaryRole == TeamRole.Gc && breakaway is not null
                     && breakaway.MemberRiderIds.Any(id => DangerousForGc(id)))
                Add(decisions, TacticalDecisionKind.Chase, t.TeamId,
                    $"equipo de GC: (c)haya un rival de la general en la fuga.");
        }

        // En subidas: ataques y defensa de la GC.
        if (onClimb)
        {
            var gcTeams = _tactics.Values
                .Where(t => t.PrimaryRole == TeamRole.Gc && t.GCLeaderId is not null)
                .ToList();
            var challengers = gcTeams.Where(t => !IsGcLeader(t.TeamId)).ToList();
            var leadingGc = gcTeams.FirstOrDefault(t => IsGcLeader(t.TeamId));

            // Sólo el retador más fuerte ataca; el resto de la GC sigue y el líder contraataca.
            var attacker = challengers.Count > 0 && kmToFinish > 40 && BreakawayGapSafe()
                ? challengers.OrderByDescending(t => BlendForRider(t.GCLeaderId!.Value, "climb_attack")).First()
                : null;

            foreach (var t in gcTeams)
            {
                if (attacker is not null && t.TeamId == attacker.TeamId)
                    Add(decisions, TacticalDecisionKind.Attack, t.TeamId,
                        $"líder de GC ({Name(t.TeamId)}) ataca en el puerto.", t.GCLeaderId!.Value);
                else if (leadingGc is not null && t.TeamId == leadingGc.TeamId && attacker is not null)
                    Add(decisions, TacticalDecisionKind.Counterattack, t.TeamId,
                        $"el líder de la general contraataca al favorito.", t.GCLeaderId!.Value);
                else if (attacker is not null)
                    Add(decisions, TacticalDecisionKind.FollowAttack, t.TeamId,
                        $"no dejar marchar al favorito; seguir el ataque.", t.GCLeaderId!.Value);
                else
                    Add(decisions, TacticalDecisionKind.ProtectLeader, t.TeamId,
                        $"proteger líder de GC en la subida.", t.GCLeaderId!.Value);
            }

            // Cazadores de montaña (sin aspiraciones reales de GC).
            foreach (var t in _tactics.Values)
                if (t.PrimaryRole == TeamRole.KOMHunter && (t.GCLeaderId ?? t.ClimberId) is int kid)
                    Add(decisions, TacticalDecisionKind.ContestKoM, t.TeamId,
                        $"cazar los puntos de montaña de la etapa.", kid);
        }

        // En descensos: riesgo calculado de los escaladores que quieren volver al grupo de cabeza.
        if (onDescent)
            foreach (var t in _tactics.Values)
                if (t.PrimaryRole == TeamRole.Gc && t.GCLeaderId is not null && !IsGcLeader(t.TeamId))
                    Add(decisions, TacticalDecisionKind.RiskDescent, t.TeamId,
                        $"asumir riesgo en el descenso para volver al grupo de cabeza.", t.GCLeaderId.Value);

        // Final de etapa llana: lanzar el sprint del esprinter.
        if (finishing && stage.Type is StageType.Flat or StageType.FlatCobbles or StageType.Crosswind)
            foreach (var t in _tactics.Values)
                if (t.PrimaryRole == TeamRole.Sprinter && t.SprinterId is not null)
                    Add(decisions, TacticalDecisionKind.LaunchSprint, t.TeamId,
                        $"lanzar el sprint de {Name(t.TeamId)} en la llegada.", t.SprinterId.Value);

        // Cierre: control del pelotón por equipos con objetivo (tempo en cabeza).
        foreach (var t in _tactics.Values)
        {
            if (!t.HasAGoal) continue;
            if (t.PrimaryRole == TeamRole.Gc)
                Add(decisions, TacticalDecisionKind.ControlPack, t.TeamId,
                    $"controlar el ritmo del pelotón.");
            else if (t.PrimaryRole == TeamRole.Puncheur && kmToFinish <= 25)
                Add(decisions, TacticalDecisionKind.Counterattack, t.TeamId,
                    $"contraatacar en el final duro.", t.PuncheurId);
        }

        // Ahorro de energía: equipos sin objetivo.
        foreach (var t in _tactics.Values)
            if (!t.HasAGoal)
                Add(decisions, TacticalDecisionKind.SaveEnergy, t.TeamId,
                    $"sin objetivo hoy: ahorrar energía en el pelotón.");

        // Ritmo constante: equipos neutrales o de GC mantienen el ritmo del grupo.
        foreach (var t in _tactics.Values)
            if (t.PrimaryRole is TeamRole.Neutral)
                Add(decisions, TacticalDecisionKind.MaintainPace, t.TeamId,
                    $"mantener el ritmo del pelotón sin sobresaltos.");

        if (Mode == DirectorMode.Player)
            state.ActionLog.Add($"[PCRM] [JUGADOR] {decisions.Count} decisiones tácticas en espera (Director Mode).");
        else if (Mode == DirectorMode.Assistant)
            state.ActionLog.Add($"[PCRM] [ASISTENTE] {decisions.Count} sugerencias tácticas registradas.");

        return decisions;
    }

    private void Add(List<TacticalDecision> list, TacticalDecisionKind kind, int teamId, string reason, int? riderId = null)
    {
        // Cada (decisión, equipo) se emite una sola vez por carrera (evita spam en el log).
        if (!_emitted.Add((kind, teamId))) return;
        list.Add(new TacticalDecision(kind, teamId, reason) { RiderId = riderId });
        _state.ActionLog.Add($"[PCRM] [IA] {TeamName(teamId)} decide '{KindName(kind)}': {reason}");
    }

    private readonly HashSet<(TacticalDecisionKind Kind, int TeamId)> _emitted = new();

    internal static string KindName(TacticalDecisionKind kind) => kind switch
    {
        TacticalDecisionKind.Attack => "atacar",
        TacticalDecisionKind.FollowAttack => "seguir ataque",
        TacticalDecisionKind.Chase => "perseguir",
        TacticalDecisionKind.MaintainPace => "mantener ritmo",
        TacticalDecisionKind.JoinBreakaway => "entrar en fuga",
        TacticalDecisionKind.ProtectLeader => "proteger líder",
        TacticalDecisionKind.SaveEnergy => "ahorrar energía",
        TacticalDecisionKind.LaunchSprint => "lanzar sprint",
        TacticalDecisionKind.ContestKoM => "disputar KoM",
        TacticalDecisionKind.ControlPack => "controlar pelotón",
        TacticalDecisionKind.Counterattack => "contraatacar",
        TacticalDecisionKind.RiskDescent => "riesgo en descenso",
        _ => kind.ToString()
    };

    private bool BreakawayGapSafe() =>
        _state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Breakaway)?.GapSeconds is not { } gap
            ? false : gap > 0;

    private bool IsGcLeader(int teamId)
    {
        var gc = _state.Classifications.GcStandings();
        if (gc.Count == 0) return false;
        var t = _tactics[teamId];
        if (t.GCLeaderId is not { } lid) return false;
        return gc[0].RiderId == lid || gc.Take(3).Any(c => c.RiderId == lid);
    }

    private bool DangerousForGc(int riderId)
    {
        var r = _riders.TryGetValue(riderId, out var rider) ? rider : null;
        if (r is null) return false;
        return BlendFor(r, "climb_attack") >= 72 || BlendFor(r, "tt_long") >= 72;
    }

    /// <summary>El mejor esprinter del pelotón que no está en la fuga.</summary>
    public Rider? StrongestSprintOutside(RiderGroup? breakaway)
    {
        var inBreak = breakaway?.MemberRiderIds.ToHashSet() ?? new HashSet<int>();
        Rider? best = null;
        int bestSprint = 0;
        foreach (var (id, rider) in _riders)
        {
            if (inBreak.Contains(id)) continue;
            if (!_states.TryGetValue(id, out var st) || st.Status != RiderStatus.Active) continue;
            if (rider.Attributes.Sprint > bestSprint)
            {
                bestSprint = rider.Attributes.Sprint;
                best = rider;
            }
        }
        return best;
    }

    // ---------- Aplicación de decisiones ----------

    /// <summary>Intensidad de persecución según el estado de carrera (compatibilidad E1).</summary>
    public ChaseIntensity DecideChase(RiderGroup? breakaway, double kmFront, double stageKm)
    {
        if (breakaway is null) return ChaseIntensity.None;
        if (breakaway.MemberRiderIds.Count == 0) return ChaseIntensity.None;

        double kmToFinish = Math.Max(0, stageKm - kmFront);
        double gap = breakaway.GapSeconds;

        bool hasSprinter = StrongestSprintOutside(breakaway) is not null;
        if (hasSprinter && kmToFinish <= 50)
            return ChaseIntensity.Strong;

        if (kmToFinish <= 15 || gap > _cfg.BreakawayMaxGapSeconds)
            return ChaseIntensity.Light;

        return ChaseIntensity.None;
    }

    /// <summary>
    /// Decide y registra la persecución. Devuelve el ajuste de velocidad (km/h).
    /// En modo Player devuelve 0 (la decisión queda para el jugador).
    /// </summary>
    public double ApplyChase(RaceState state, RiderGroup? breakaway, double kmFront, double stageKm)
    {
        var intensity = DecideChase(breakaway, kmFront, stageKm);
        if (Mode == DirectorMode.Player)
        {
            state.ActionLog.Add($"[PCRM] [JUGADOR] ¿Perseguir fuga ({intensity})? (Director Mode: decisión pendiente)");
            return 0;
        }

        double baseAdjust;
        string reason;
        switch (intensity)
        {
            case ChaseIntensity.Strong:
                baseAdjust = 3.5;
                reason = "equipos de sprint cazan la fuga.";
                break;
            case ChaseIntensity.Light:
                baseAdjust = 1.2;
                reason = "control ligero del pelotón.";
                break;
            default:
                baseAdjust = 0;
                reason = "la fuga no representa amenaza hoy.";
                break;
        }
        state.ActionLog.Add($"[PCRM] [IA] Persecución {intensity}: {reason}");
        return baseAdjust;
    }

    // ---------- Utilidades ----------

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

    private double BlendForRider(int riderId, string profile) =>
        _riders.TryGetValue(riderId, out var r) ? BlendFor(r, profile) : 0;

    private string Name(int teamId) => _teams.TryGetValue(teamId, out var t) ? t.Name : $"#{teamId}";
    private string TeamName(int teamId) => _teams.TryGetValue(teamId, out var t)
        ? (string.IsNullOrEmpty(t.Abbr) ? t.Name : t.Abbr) : $"#{teamId}";
}