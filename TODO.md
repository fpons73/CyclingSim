# Pro Cycling Replay Manager — ToDo

Estado actualizado en cada avance. Última actualización: 2026-08-20.

## En curso

- [ ] **E1 — MVP (Fase 1)**

## Entregas

### Entrega 0 — Esqueleto y datos ✅ (verificado 2026-08-20)
- [x] E0: Instalar .NET SDK 8 (8.0.424) y Godot 4.7.2 Mono (winget) + VS Build Tools
- [x] E0: git init + estructura monorepo (CoreLib, CoreLib.Tests, game/, tools/)
- [x] E0: tools/import_data.py -> data/pcrm.sqlite (3320 corredores, 221 equipos) + data/stages/*.json (13 + Grande Boucle 21)
- [x] E0: CoreLib: modelos + SeededRandom (xoshiro256**) + RulesConfig + calculadores + **27 tests xUnit verdes**
- [x] E0: proyecto Godot game/ (Godot.NET.Sdk 4.7.2) compila y ARRANCA con runtime C# (`[PCRM] ... 1d6=1 2d6=7`)
- [x] E0: **BLOQUEO runtime Godot+C# RESUELTO** — causas: (1) `godot.exe` de WinGet es symlink → ejecutar siempre el exe real junto a `GodotSharp/`; (2) DLL debe compilarse a `.godot/mono/temp/bin/Debug`. Detalle en plan.md §0.

### Entrega 1 — MVP (Fase 1) ✅ motor/sim/UI Core
- [x] E1: motor RNG integrado (RaceSetup + SeededRandom), BD cargada desde game/ (3320 corredores, 221 equipos)
- [x] E1: FlatStageSimulator: fuga -> persecución (gap objetivo) -> sprint masivo; 39 tests xUnit verdes
- [x] E1: IA básica + Director Mode (RaceDecisionEngine, 4 tests) y Player Mode (decisión pendiente registrada)
- [x] E1: ficha de corredor (RiderCard: 14 atributos + fatiga), pantallas Pre/Etapa/Post Godot
- [x] E1: selftest headless `godot --selftest` (carga BD + simula + log) y salvado/export CSV/HTML
- [ ] E1: sprint/render visual pulido de pantallas (verificación visual por el usuario en editor)

### Entrega 2 — Montaña y Tour (Fase 2) ✅
- [x] E2: media montaña, colina, montaña, descensos, KoM (MountainStageSimulator)
- [x] E2: Recuperación/Resistencia entre etapas (RecoveryCalculator en TourSimulator)
- [x] E2: clasificaciones completas (GC/Puntos/KoM/Jóvenes por equipos en Classifications)
- [x] E2: Tour Mode completo (21 etapas de la Grande Boucle), fatiga heredada
- [x] E2: CRI/Prólogo/TTT (TimeTrialStageSimulator) — anexo como parte de la integración Tour
- [x] E2: TourLoader (manifiesto stage_refs + índice), selftest headless `--selftest-tour` (exit 0)
- [x] E2: 49 tests xUnit verdes

### Entrega 3 — Especialidades (Fase 3)
- [x] E3: CRI, Prólogo, TTT (ver E2: TimeTrialStageSimulator cubre IndividualTimeTrial y TeamTimeTrial)
- [x] E3: Pavés (FlatCobbles) y Viento cruzado (Crosswind) — tipos de etapa soportados
- [ ] E3: IA avanzada (12 decisiones PRD §21)

### Entrega 4 — Pulido (Fase 4)
- [ ] E4: dados 3D/animaciones, modo espectador avanzado, Stage Editor
- [ ] E4: importación histórica multi-temporada; i18n ES/EN/FR; optimización Windows 11