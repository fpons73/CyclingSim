# Pro Cycling Replay Manager — Plan de desarrollo

**Versión del plan:** 1.0
**Fecha:** 2026-08-19
**Base:** PRD — PRO CYCLING REPLAY MANAGER (V2).md
**Stack (confirmado por el usuario):** Godot 4.3+ · C# · .NET SDK 8 · SQLite · Windows 11
**Idioma principal UI:** Español (estructura i18n para EN/FR)
**Reglas del juego base:** Sin reglamento original disponible → fórmulas propias parametrizables (tunables vía JSON, sin recompilar).

---

## 0. Decisiones confirmadas

1. **Stack: Godot 4.3 Mono/.NET + C# estricto** (sin stack web a pesar de preferencia pnpm).
2. **Fórmulas de simulación diseñadas por nosotros**, configurables desde `RulesConfig.json` (el reglamento de mesa no está disponible).
3. **Alcance: todo el roadmap**, entregado en entregas consecutivas funcionales (E0→E4).
4. **Idioma UI: Español** por defecto con estructura de traducción ES/EN/FR.
5. **Escala de atributos:** NO se normaliza. La escala declarada es 50–99 (dominio oficial del motor); se cargan los valores tal cual vienen del Excel. Datos actuales: rango real 50–85 (verificado; máx. MM/ATT/STA=85, MNT=84, SPR=84, FLA=83). El motor es agnóstico al valor real dentro del dominio 50–99.
6. **Instalación de herramientas:** permitida vía winget (.NET SDK 8 + Godot 4.3 Mono). Fallback Godot: última 4.x si 4.3 exacto no está en winget (cumple "4.3+"). **Instalado finalmente: Godot 4.7.2 Mono (winget) + .NET SDK 8.0.424 + VS Build Tools.**
7. **git init + commit por entrega** (solo tras confirmación explícita del usuario en cada commit).

**Nota operativa GANADA EN E0 (2016-08-20) — ejecutar C# en este equipo:**
- `godot.exe` de WinGet es un **symlink** y el motor no encuentra `GodotSharp/` junto a él
  → colgaba en `.NET: Initializing module...`. **Siempre ejecutar el exe real**
  `Godot_v4.7.2-stable_mono_win64.exe` (en `WinGet\Packages\GodotEngine.GodotEngine.Mono_...\`, junto a `GodotSharp/`).
- La DLL del juego debe compilarse a `.godot/mono/temp/bin/Debug/` (donde la busca el motor):
  `dotnet build game.csproj -c Debug -o game\.godot\mono\temp\bin\Debug`.
- Verificado: probe mínimo C# y `game/` real (`[PCRM] ... 1d6=1 2d6=7`) con `--headless --quit`.

---

## 1. Datos fuente y limpieza (análisis realizado)

### Ciclistas_2026_1.xlsx (Hoja1, 3320 corredores)
- Columnas: `Nombre, F_Nac, Nacionalidad, Equipo, TeamID, SeasonID(=3), Especialidad, FLA, MNT, MM, HIL, TTR, PRL, COB, SPR, ACC, DHI, ATT, STA, RES, REC`
- Anomalías detectadas:
  - `Especialidad` **vacía en el 100%** de los registros → derivar roles automáticamente por estadísticas.
  - `TeamID` **no coincide** con `TeamId` del fichero de equipos → unión por **Nombre de equipo**.
  - Fechas inválidas (`00/00/2000`, 4 registros) → marcar edad desconocida.
  - Rango real de atributos 50–85 (ninguno llega a 86+).
- El dataset corresponde a la temporada 2026 (`SeasonID = 3`).

### Equipos_2026.xlsx (Hoja1, 209 equipos)
- Columnas: `Temporada(=3), TeamId, Nombre, Abreviatura, Pais, Categoría`
- Categorías: WorldTour 18 · ProTeam 16 · Continental 175.

---

## 2. Estructura del monorepo

```
C:\Proyectos\SimCycling\
├── plan.md
├── TODO.md
├── README.md
├── .gitignore
├── tools/
│   └── import_data.py          # xlsx -> SQLite + JSON de etapas
├── data/
│   ├── pcrm.sqlite             # BD generada (seeded)
│   ├── seed/situamos           # origen: tools/import_data.py
│   └── stages/*.json           # catálogo de etapas (flat/mountain/itt/...)
├── CoreLib/                    # Simulation Core PURA (.NET 8, sin deps Godot)
│   ├── CoreLib.csproj
│   └── ...
├── CoreLib.Tests/              # xUnit (dotnet test, headless)
│   └── ...
└── game/                       # Proyecto Godot 4.3 (C#)
    ├── project.godot
    ├── game.csproj             # Godot.NET.Sdk, referenciando CoreLib
    ├── src/                    # Código C# del juego (escenas, presentación)
    └── data/                   # copia de pcrm.sqlite + stages (embebidos en export)
```

**Regla arquitectónica clave (PRD §39/§40):** la Simulation Core es una biblioteca C# pura
(`net8.0`, sin referencias a Godot) referenciada por el proyecto Godot y por los tests xUnit.
Así toda la lógica es compilable/testable desde CLI (`dotnet test`). La presentación (Godot)
solo consume la API de la core. Nunca se escriben fórmulas en la capa de UI.

---

## 3. Pipeline de datos (Python → SQLite)

`tools/import_data.py` genera `data/pcrm.sqlite` con tablas:
`teams`, `seasons`, `riders`, `season_rosters` (unión rider↔equipo↔temporada),
y deja `data/stages/*.json` con el catálogo de etapas.

Limpieza:
- Unión equipos por **Nombre** (TeamId inconsistentes entre ficheros).
- Derivación de **roles/especializaciones** por reglas configurables sobre las 14 estadísticas
  (climber, sprinter, puncheur, rouleur, cobble, TTer, allrounder, prologue). Almacenadas en `rider_roles`.
- Fechas inválidas → `birth_date = NULL`, edad marcada como desconocida
  (para la clasificación de Jóvenes sub-25 se usa fecha conocida o se excluye).
- Valores de atributos **sin normalizar** (dominio 50–99 oficial).

Catálogo de etapas (plantilla 2026, para probar todo el mapa):
- `flat.json`, `flat_hilly.json`, `flat_cobbles.json`,
- `medium_mountain.json`, `mountain.json`,
- `itt.json`, `ttt.json`, `crosswind.json`, `prologue.json`.
- Tour Mode: `grande_boucle_2026.json` con 21 etapas (incluye prólogo + 2 CRI + TTT + pavés + viento + montaña).

Nota: el "Replay histórico (p.ej. Tour 2010)" queda como importador multi-temporada para una fase
posterior; no se inventan corredores históricos sin datos.

---

## 4. Simulation Core — diseño

### Modelos
- `Attributes` (14 ints 50–99): FLA MNT MM HIL TTR PRL COB SPR ACC DHI ATT STA RES REC.
- `Rider` (identidad, nacionalidad, fecha nac, equipo, rol/especialidades).
- `RiderState` (fatiga 0–100, tiempo acumulado, posición, grupo actual, estado:
  Activo/Questionable/Dropped/DNS/DNF/DSQ).
- `Team`, `RiderGroup` (miembros, roles dentro del grupo, km/h, GV, cohesion).
- `Stage`, `StageSection` (km, terreno[], pendiente, adoquines, viento, puerto, sprint intermedio, meta), `Climb`.
- `Classifications` (GC/Puntos/KoM/Jóvenes/Equipos).
- `RaceState` (etapa, corredores, grupos, RNG stream, clasificaciones, log).
- `RulesConfig` (todas las fórmulas/parámetros editables).

### RNG reproducible
- `SeededRandom` (xoshiro256** + splitmix64 para semilla) con stream secuencial.
- El estado del RNG se persiste en el guardado → misma seed + misma etapa + mismas entradas
  + misma configuración = reproductible exactamente (PRD §32).
- Dados virtuales: 2d6 (rojo/blanco) + 1d10 (azul) extraídos del mismo stream; la capa de
  presentación puede animarlos o mostrar resultado instantáneo (modo rápido, PRD §8).

### Calculadores (PRD §40), todos centralizados y parametrizables
`RiderPerformanceCalculator` · `FatigueCalculator` · `GroupValueCalculator` ·
`BreakawayCalculator` · `SprintCalculator` · `TimeTrialCalculator` ·
`MountainCalculator` · `CrosswindCalculator` · `CobblesCalculator` ·
`RecoveryCalculator` · `RaceDecisionEngine` (IA).

### Motor de etapa (PRD §7)
Secuencia por sección: cargar sección → identificar grupos → estado de grupo →
incidentes → decisiones tácticas → acciones → RNG/dados → rendimiento → fatiga →
tiempos → posiciones → clasificaciones → presentar → decisiones (si jugador) → siguiente.

### Principio fundamental (PRD §41)
Reglas de etapa → situación de carrera → atributos relevantes → fatiga/estado →
decisión táctica → RNG/dados → rendimiento efectivo → resultado.

### Fórmulas parametrizables (diseño propio, config en `RulesConfig.json`)
- **Rendimiento efectivo** = Σ(w_i × attr_i) × (1 − penalización fatiga modulada por STA/RES)
  × modificador posicional/táctico × ruido RNG determinista.
- **Fatiga**: acumulación por km, desnivel, ritmo/esfuerzo, ataques, persecución, pavés,
  viento; modulada por STA y RES (PRD §5).
- **GV (Group Value)**: solo cuentan los corredores que ruedan (en pelotón, la parte activa
  que da tempo, PRD §20). Cohesion Level determina splits.
- **Fuga**: ventana de fuga, selección por rol+CG, GV fuga vs pelotón, recalculo de GV (PRD §19).
- **Sprint**: masivo SPR+ACC(+FLA+fatiga+posición); reducido ACC+ATT+terreno; explosivo ACC+HIL+SPR (PRD §18).
- **Montaña**: Action Phase, ataques (MNT+ATT+ACC), Pace Check (MNT+STA), contraataques,
  descenso (DHI), Time Management, KoM por categoría (PRD §12).
- Media montaña (MM pral., STA/ATT/ACC/DHI sec.) y Colina (HIL pral., ACC/ATT/STA sec.) (PRD §11).
- **CRI** (TTR pral. + STA/RES/fatiga según distancia), **Prólogo** (PRL pral.; TTR no sustituye), **TTT** (TTR+FLA+STA+RES, el peor del grupo arrastra según regla configurable) (PRD §13–15).
- **Pavés** (COB pral. + FLA/STA/ACC/ATT/fatiga, incidentes propios) (PRD §17).
- **Viento cruzado** (check previo, echelons, Echelon Value, segundo echelon; FLA+STA pral.) (PRD §16).
- **Recuperación entre etapas**: fatiga_final → recup → fatiga_residual (Tour) (PRD §5).

### IA (PRD §21)
- Misma API y reglas que el jugador; sin estadísticas paralelas.
- `RaceDecisionEngine` evalúa utilidad sobre contextos (atributos, fatiga, posición, CG,
  diferencias, compañeros, fuga, km restantes, terreno, situación, objetivo de equipo, importancia).
- Decisiones: atacar, seguir ataque, perseguir, mantener ritmo, entrar en fuga, proteger líder,
  ahorrar energía, lanzar sprint, disputar KoM, controlar pelotón, contraatacar, arriesgar en descenso.
- En modo jugador: las decisiones de su equipo pausan y muestran opciones (PRD §22).
- Director Mode: auto-ejecución de todas las decisiones (PRD §23).

### Clasificaciones (PRD §25)
GC (tiempos), Puntos (metas/sprints/bonus configurables), Montaña (puertos por categoría),
Jóvenes (sub-25), Equipos (suma 3 mejores por etapa).

---

## 5. Presentation Layer (Godot)

Escenas:
- `MainMenu` (modos: Etapa individual/Sandbox, Tour Mode, Director Mode, Replay, opciones).
- `PreStage`: perfil+recorrido, distancia, favoritos, DNS/Questionable, fatiga heredada,
  estrategia del equipo (PRD §30).
- `RaceScreen`: perfil de etapa (dibujado con `_Draw`), km actual/restantes, grupos y gaps,
  clasificación, log de acciones, fatiga, ficha de corredor (14 atributos 50–99 claros,
  fatiga diferenciada visualmente, PRD §29), popup de decisiones, dados (animados/instantáneos),
  velocidad de simulación, pausa/avance sección.
- `PostStage`: ganador, clasificación completa, gaps, cambios CG, Puntos/KoM/Jóvenes/Equipos,
  incidentes, abandonos, fatiga final + recuperación aplicada, resumen táctico (PRD §31).
- `StageEditor/Sandbox`: etapa, corredores/equipos, seed, condiciones, reglas (PRD §34).
- `Settings`: tema claro/oscuro, idioma, velocidad, DPI, accesibilidad.

Requisitos UI: Windows 11 (ventana/pantalla completa/redimensionado/Snap Layouts/DPI),
múltiples resoluciones 1080p/1440p/4K, atajos de teclado, daltonismo (dados con pips numéricos),
localización ES/EN/FR (PRD §36–38).

---

## 6. Persistencia, guardado y exportación

- **SQLite (`Microsoft.Data.Sqlite`)**: datos maestros (teams/riders/rosters/stages/resultados).
  Fallback a snapshot JSON si la DLL nativa de SQLite diera problemas en Godot.
- **Guardado**: serialización JSON completa de `RaceState` (grupos, RNG, fatigas, clasificaciones,
  seed, etapa) en `user://saves/` → múltiples partidas, guardados intra-etapa/post-etapa/Tour,
  recuperación exacta (PRD §32).
- **Exportación**: CSV y HTML de resultados, clasificaciones, tiempos, corredores, estadísticas
  y eventos principales (PRD §33).
- Reproducibilidad: seed de simulación visible y editable en sandbox/replay.

---

## 7. Entregas y roadmap (PRD §42, §43)

### Entrega 0 — Esqueleto y datos
- Setup tooling (winget: .NET SDK 8, Godot 4.3 Mono).
- git init + estructura monorepo + .gitignore.
- Importador Python → SQLite + catálogo de etapas JSON.
- CoreLib: modelos + `SeededRandom` + `RulesConfig` + tests xUnit de RNG/reproducibilidad.
- Proyecto Godot mínimo importable (`--headless --import`) + `dotnet build` OK.

### Entrega 1 — MVP (Fase 1) ✅ (motor y sim verificados; pulido visual pendiente)
- Motor RNG integrado (`RaceSetup`), BD cargada desde `game/data` (3320 corredores, 221 equipos, 9 etapas).
- `FlatStageSimulator` (CoreLib): secciones → velocidad por GV → fuga temprana → persecución con gap
  objetivo (seno, pico mid-stage, caza ~7 km antes de meta) → sprint intermedio → sprint masivo
  (SPR+ACC+FLA+fatiga+ruido RNG) con fuga superviviente opcional (5%).
- IA básica: `RaceDecisionEngine` (DirectorMode Directed/Player/Assistant), persecución según
  sprinter fuerte en pelotón a <50 km del final.
- UI Godot: PreStage (etapa/equipos/seed + fichas RiderCard 14 atributos+fatiga), RaceScreen
  (log + grupos), PostStage (clasificaciones + guardado user://saves + export CSV/HTML).
- Verificación: `dotnet test` (39 tests) + `godot --headless --selftest` (carga datos, simula,
  log, exit 0) + `--quit-after` carga de escenas.
- Métrica objetivo: etapa llana ≈ subsegun do en simulación headless (los 10-15 min serían con
  animación/dados en E4; en E1 la etapa se resuelve al instante).

### Entrega 2 — Montaña y Tour (Fase 2)
- Media montaña, colina, montaña, descensos, KoM.
- Recuperación/Resistencia entre etapas.
- Clasificaciones completas (GC/Puntos/KoM/Jóvenes/Equipos).
- Tour Mode completo (21 etapas), fatiga heredada.

### Entrega 3 — Especialidades (Fase 3)
- CRI, Prólogo, TTT, Pavés, Viento cruzado.
- IA avanzada (12 decisiones del PRD).

### Entrega 4 — Pulido (Fase 4)
- Dados 3D + animaciones, modo espectador avanzado (pausa/velocidad/avance), Stage Editor,
  importación histórica multi-temporada, localización EN/FR, optimización Windows 11.

---

## 8. Verificación

- `dotnet test` (CoreLib.Tests) en cada entrega: reproducibilidad por seed, rangos válidos
  (fatiga 0–100, atributos 50–99), diferenciación de corredores, coherencia de clasificaciones.
- `dotnet build` del csproj Godot + `godot --headless --import` para validar que el proyecto
  carga sin errores de recursos/compilación.
- Verificación visual de la UI: la realiza el usuario abriendo Godot 4.3; se itera sobre pantallas.

---

## 9. Métricas de éxito (PRD §43)

- 100 % de reglas implementadas (parámetros configurables donde el reglamento no especifica).
- Resultados reproducibles por seed.
- Etapas llanas ≈ 10–15 min; montaña ≈ 20–25 min.
- Tour completo sin errores de estado.
- Diferenciación estadística clara entre corredores.
- IA coherente; jugador puede entender por qué rinde cada corredor.