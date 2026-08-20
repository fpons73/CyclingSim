# -*- coding: utf-8 -*-
"""Importador de datos: XLSX -> SQLite (data/pcrm.sqlite) + catálogo de etapas JSON (data/stages/).

Pro Cycling Replay Manager · E0
- Une corredores y equipos por NOMBRE (los TeamID difieren entre ficheros).
- Deriva roles/especializaciones de ciclista a partir de las 14 estadísticas
  (la columna 'Especialidad' viene vacía en el 100% de los registros).
- Normaliza fechas de nacimiento (los '00/00/2000' quedan como NULL).
- Genera el catálogo de etapas (9 perfiles individuales + Grande Boucle 2026, 21 etapas).
"""
import json
import os
import sqlite3
from datetime import datetime

import pandas as pd

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
STAGES = os.path.join(DATA, "stages")
XLSX_RIDERS = os.path.join(ROOT, "Ciclistas_2026_1.xlsx")
XLSX_TEAMS = os.path.join(ROOT, "Equipos_2026.xlsx")
DB_PATH = os.path.join(DATA, "pcrm.sqlite")

ATTRS = ["FLA", "MNT", "MM", "HIL", "TTR", "PRL", "COB", "SPR", "ACC", "DHI", "ATT", "STA", "RES", "REC"]

# Reglas de derivación de roles: (columna de estadística, umbral, ¿exige también ACC>=72?)
ROLE_RULES = {
    "sprinter": ("SPR", 74, False),
    "climber": ("MNT", 71, False),
    "puncheur": ("HIL", 70, True),
    "rouleur": ("FLA", 69, False),
    "time_trialist": ("TTR", 72, False),
    "prologue_specialist": ("PRL", 73, False),
    "paveur": ("COB", 74, False),
    "allrounder": (None, 0, False),  # regla especial, avg>=66 y MNT>=68 y FLA>=66
}
TOP_N = 4


def derive_roles(row: dict) -> list:
    scores = {c: int(row[c]) for c in ATTRS}
    sorted_top4 = set(sorted(scores, key=lambda c: scores[c], reverse=True)[:TOP_N])
    roles = []
    for role, (col, thresh, need_acc) in ROLE_RULES.items():
        if role == "allrounder":
            avg = sum(scores.values()) / len(scores)
            if avg >= 66 and scores["MNT"] >= 68 and scores["FLA"] >= 66:
                roles.append(role)
            continue
        if scores[col] >= thresh and (not need_acc or scores["ACC"] >= 72) and col in sorted_top4:
            roles.append(role)
    return roles


def parse_birth(raw):
    """'dd/mm/yyyy' -> ISO; '00/00/2000' u otros inválidos -> None."""
    if raw is None or not isinstance(raw, str):
        return None
    try:
        d = datetime.strptime(raw.strip(), "%d/%m/%Y")
        if d.day == 0 or d.month == 0:
            return None
        return d.date().isoformat()
    except ValueError:
        return None


def load_riders():
    df = pd.read_excel(XLSX_RIDERS, dtype={"TeamID": "Int64", "SeasonID": "Int64"})
    rows = []
    for _, r in df.iterrows():
        rec = {c: int(r[c]) for c in ATTRS}
        rows.append({
            "name": str(r["Nombre"]).strip(),
            "birth_date": parse_birth(r["F_Nac"]),
            "nationality": str(r["Nacionalidad"]).strip() if pd.notna(r["Nacionalidad"]) else None,
            "team_name": str(r["Equipo"]).strip(),
            "season_id": int(r["SeasonID"]) if pd.notna(r["SeasonID"]) else 3,
        } | rec)
    return rows


def load_teams():
    df = pd.read_excel(XLSX_TEAMS)
    teams = {}
    for _, r in df.iterrows():
        name = str(r["Nombre"]).strip()
        teams[name] = {
            "abbr": str(r["Abreviatura"]).strip(),
            "country": str(r["Pais"]).strip(),
            "category": str(r["Categoría"]).strip() if pd.notna(r["Categoría"]) else "Unknown",
        }
    return teams


def build_db(riders, teams_info):
    if os.path.exists(DB_PATH):
        os.remove(DB_PATH)
    os.makedirs(STAGES, exist_ok=True)
    con = sqlite3.connect(DB_PATH)
    cur = con.cursor()
    cur.executescript("""
        CREATE TABLE seasons (
            id INTEGER PRIMARY KEY,
            name TEXT,
            year INTEGER
        );
        CREATE TABLE teams (
            id INTEGER PRIMARY KEY,
            name TEXT,
            abbr TEXT,
            country TEXT,
            category TEXT,
            season_id INTEGER
        );
        CREATE TABLE riders (
            id INTEGER PRIMARY KEY,
            name TEXT,
            birth_date TEXT,
            nationality TEXT,
            team_id INTEGER,
            season_id INTEGER,
            number INTEGER,
            roles TEXT,
            fla INTEGER, mnt INTEGER, mm INTEGER, hil INTEGER, ttr INTEGER, prl INTEGER,
            cob INTEGER, spr INTEGER, acc INTEGER, dhi INTEGER, att INTEGER, sta INTEGER,
            res INTEGER, rec INTEGER,
            FOREIGN KEY(team_id) REFERENCES teams(id)
        );
        CREATE INDEX idx_riders_team ON riders(team_id);
    """)

    season_ids = sorted({int(r["season_id"]) for r in riders})
    for sid in season_ids or [3]:
        cur.execute("INSERT INTO seasons(id, name, year) VALUES (?, ?, ?)",
                    (sid, f"Season {sid}", 2024 + sid))

    # equipos corporativos de los ficheros
    team_id = 0
    for name, info in teams_info.items():
        team_id += 1
        cur.execute("""INSERT INTO teams(id, name, abbr, country, category, season_id)
                       VALUES (?, ?, ?, ?, ?, ?)""",
                    (team_id, name, info["abbr"], info["country"], info["category"], 3))

    # + equipos sintéticos para plantillas sin ficha
    synthetic_used = set()
    for r in riders:
        name = r["team_name"]
        if name not in teams_info and name not in synthetic_used:
            synthetic_used.add(name)
            team_id += 1
            cur.execute("""INSERT INTO teams(id, name, abbr, country, category, season_id)
                           VALUES (?, ?, ?, ?, ?, ?)""",
                        (team_id, name, "", None, "Unknown", 3))
            teams_info[name] = {"abbr": "", "country": None, "category": "Unknown"}

    cur.execute("SELECT id, name FROM teams")
    name_to_id = {n: i for i, n in cur.fetchall()}

    # riders con dorsal por equipo (orden de fila en el fichero)
    counters = {}
    for r in riders:
        tid = name_to_id[r["team_name"]]
        counters[tid] = counters.get(tid, 0) + 1
        roles = derive_roles(r)
        cur.execute("""INSERT INTO riders(
                id, name, birth_date, nationality, team_id, season_id, number, roles,
                fla, mnt, mm, hil, ttr, prl, cob, spr, acc, dhi, att, sta, res, rec)
              VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                    (None, r["name"], r["birth_date"], r["nationality"], tid,
                     r["season_id"], counters[tid], json.dumps(roles),
                     r["FLA"], r["MNT"], r["MM"], r["HIL"], r["TTR"], r["PRL"],
                     r["COB"], r["SPR"], r["ACC"], r["DHI"], r["ATT"], r["STA"],
                     r["RES"], r["REC"]))
    con.commit()
    total = cur.execute("SELECT COUNT(*) FROM riders").fetchone()[0]
    con.close()
    print(f"  sintéticos añadidos: {len(synthetic_used)} team(s)")
    return total


# ---------------------------------------------------------------------------
# Catálogo de etapas

def sec(km_from, km_to, terrains, gradient=0, cobbles=False, wind=None,
        sprint=None, climb_id=None, finish=False):
    s = {"km_from": km_from, "km_to": km_to, "terrains": terrains,
         "gradient": gradient, "cobbles": cobbles, "wind": wind, "finish": finish}
    if sprint:
        s["intermediate_sprint"] = sprint
    if climb_id:
        s["climb_id"] = climb_id
    return s


def climb(cid, name, km_from, km_to, category, length_km, avg_gradient, koM_points, summit_offset=0):
    return {"id": cid, "name": name, "km_from": km_from, "km_to": km_to,
            "category": category, "length_km": length_km, "avg_gradient": avg_gradient,
            "summit_km": km_from + summit_offset,
            "koM_points": koM_points}


FLAT_SPRINT_POINTS = [50, 30, 20, 18, 16, 14, 12, 10, 8, 7, 6, 5, 4, 3, 2]
HILLY_SPRINT_POINTS = [30, 25, 22, 19, 17, 15, 13, 11, 9, 7, 6, 5, 4, 3, 2]
MTN_SPRINT_POINTS = [15, 12, 10, 8, 6, 5, 4, 3, 2, 1]
KOM_CAT_POINTS = {1: [20, 15, 12, 10, 8, 6, 4, 2], 2: [10, 8, 6, 4, 2], 3: [4, 2, 1], 4: [2, 1]}


def flat_template(stage_id, name, dist, cobbles=False, crosswind=False, has_mid_climb=False,
                  stype=None):
    n = 6
    sections = []
    seg = dist / n
    for k in range(n):
        f, t = round(k * seg, 1), round((k + 1) * seg, 1)
        terrains = ["flat", "hill"] if (has_mid_climb and k == 2) else ["flat"]
        wind = {"direction": "cross", "strength": 4} if (crosswind and k in (2, 3)) else \
            {"direction": "tail", "strength": 1}
        if k == n - 1:
            sp = {"km": t - 2.0, "points": FLAT_SPRINT_POINTS}
            sections.append(sec(f, t, terrains, gradient=0, cobbles=cobbles, wind=wind,
                                sprint=sp, finish=True))
        else:
            sections.append(sec(f, t, terrains, gradient=0, cobbles=cobbles, wind=wind))
    return stage(stage_id, name, stype or ("flat_cobbles" if cobbles else
                                            "crosswind" if crosswind else "flat"),
                 dist, sections)


def mountain_template(stage_id, name, dist, medium=False):
    cat = 2 if medium else 1
    grad = 4.5 if medium else 6.5
    c = climb(f"{stage_id}_c1", f"Col {name}", 70, 90, cat, 20.0, grad,
              KOM_CAT_POINTS[cat], summit_offset=18)
    sections = [
        sec(0, 30, ["flat"], gradient=0),
        sec(30, 70, ["flat", "hill"], gradient=1),
        sec(70, 90, ["climb"], gradient=grad, climb_id=c["id"]),
        sec(90, 110, ["descent"], gradient=0),
        sec(110, dist, ["flat", "hill"], gradient=1, finish=True,
            sprint={"km": dist - 1.0, "points": MTN_SPRINT_POINTS}),
    ]
    return stage(stage_id, name, "medium_mountain" if medium else "mountain",
                 dist, sections, [c])


def stage(stage_id, name, stype, distance, sections, climbs=None,
          time_factor=1.0, tempo_modifier=0.0):
    return {"id": stage_id, "name": name, "season_id": 3, "date": "2026-07-01",
            "type": stype, "distance_km": distance, "time_factor": time_factor,
            "tempo_modifier": tempo_modifier, "sections": sections, "climbs": climbs or []}


CATALOG = [
    flat_template("flat_01_2026", "Llana: Peloton Plains", 197.0),
    flat_template("flat_02_2026", "Llana: Sprinters Circuit", 182.0),
    flat_template("flat_03_2026", "Llana con repecho: Rolling Town", 168.0, has_mid_climb=True),
    flat_template("flat_04_2026", "Adoquines: Roubaix Manche", 214.0, cobbles=True),
    flat_template("flat_05_2026", "Viento cruzado: Coastal Echelon", 193.0, crosswind=True),
    mountain_template("medium_mountain_01_2026", "Massif Central", 176.0, medium=True),
    mountain_template("medium_mountain_02_2026", "Land of Hills", 184.0, medium=True),
    mountain_template("mountain_01_2026", "High Pyrenees", 208.0),
    mountain_template("mountain_02_2026", "Alpine Queen", 221.0),
    stage("itt_01_2026", "CRI: Race Against Clock", "itt", 37.0,
          [sec(0, 37.0, ["flat", "flat"], gradient=0, finish=True)]),
    stage("itt_02_2026", "CRI: Flat TT", "itt", 52.0,
          [sec(0, 52.0, ["flat", "flat"], gradient=0, finish=True)]),
    stage("ttt_01_2026", "CRE: Team Trial", "ttt", 35.0,
          [sec(0, 35.0, ["flat", "flat"], gradient=0, finish=True)]),
    stage("prologue_01_2026", "Prólogo: Short Blast", "prologue", 5.6,
          [sec(0, 5.6, ["flat"], gradient=0, finish=True)]),
]

GB2026_PLAN = [
    (1, "Prólogo: Downtown", "prologue", 5.6),
    (2, "Llana: North Plains", "flat", 192),
    (3, "Llana: Coastal Run", "flat", 178),
    (4, "Adoquines: Hell of the North", "cobbles", 214),
    (5, "Llana: Flat Fields", "flat", 199),
    (6, "CRI 1: Star TT", "itt", 37),
    (7, "Media montaña: Central Hills", "medium_mountain", 180),
    (8, "Llana: Valley Sprints", "flat", 187),
    (9, "Montaña: High Pass 1", "mountain", 209),
    (10, "Montaña: Summit Finish 1", "medium_mountain", 165),
    (11, "Llana: Recovery Flat", "flat", 188),
    (12, "Media montaña: Breakaway Hills", "medium_mountain", 174),
    (13, "Viento: Echelon Stage", "crosswind", 201),
    (14, "Montaña: Summit Finish 2", "mountain", 226),
    (15, "Llana: Flat Lowlands", "flat", 195),
    (16, "TTT: Team Trial", "ttt", 35),
    (17, "Llana: Sprint Final Prep", "flat", 181),
    (18, "Montaña: High Pass 2", "mountain", 220),
    (19, "Montaña: Alpine Queen", "mountain", 230),
    (20, "CRI 2: Final Clock", "itt", 39),
    (21, "Llana: Champs Final", "flat", 120),
]


def grande_boucle_2026():
    out = []
    for i, name, stype, dist in GB2026_PLAN:
        sid = f"gb2026_s{i:02d}"
        if stype == "prologue":
            s = stage(sid, name, "prologue", dist,
                      [sec(0, dist, ["flat"], gradient=0, finish=True)])
        elif stype == "itt":
            s = stage(sid, name, "itt", dist,
                      [sec(0, dist, ["flat", "flat"], gradient=0, finish=True)])
        elif stype == "ttt":
            s = stage(sid, name, "ttt", dist,
                      [sec(0, dist, ["flat", "flat"], gradient=0, finish=True)])
        elif stype == "cobbles":
            s = flat_template(sid, name, dist, cobbles=True)
        elif stype == "crosswind":
            s = flat_template(sid, name, dist, crosswind=True)
        elif stype == "medium_mountain":
            s = mountain_template(sid, name, dist, medium=True)
        elif stype == "mountain":
            s = mountain_template(sid, name, dist)
        else:
            s = flat_template(sid, name, dist)
        out.append(s)
    return out


def write_stage_json(s):
    path = os.path.join(STAGES, s["id"] + ".json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(s, f, ensure_ascii=False, indent=2)
    return path


def main():
    riders = load_riders()
    teams_info = load_teams()
    total = build_db(riders, teams_info)
    print(f"OK  riders={total}  teams={len(teams_info)}  db={DB_PATH}")

    os.makedirs(STAGES, exist_ok=True)
    for s in CATALOG:
        write_stage_json(s)
    tour_stages = grande_boucle_2026()
    for s in tour_stages:
        write_stage_json(s)
    with open(os.path.join(STAGES, "grande_boucle_2026.json"), "w", encoding="utf-8") as f:
        json.dump({"tour": "Grande Boucle 2026",
                   "stage_refs": [s["id"] for s in tour_stages]}, f,
                  ensure_ascii=False, indent=2)
    print(f"OK  stages individuales={len(CATALOG)}  tour={len(tour_stages)} en {STAGES}")


if __name__ == "__main__":
    main()