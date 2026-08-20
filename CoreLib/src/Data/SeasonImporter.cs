using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ProCycling.Core.Data;

/// <summary>Rider de importación (formato CSV/SQLite) sin depender de DataFrame.</summary>
public record ImportRider(
    string Name, string? BirthDate, string? Nationality, string TeamName,
    int SeasonId, int Number, IReadOnlyCollection<string> Roles,
    int pFlat, int pMountain, int pMediumMountain, int pHill, int pTimeTrial, int pPrologue,
    int pCobbles, int pSprint, int pAcceleration, int pDescent, int pAttack, int pEndurance,
    int pResistance, int pRecovery);

public record ImportTeam(string Name, string Abbr, string? Country, string Category, int SeasonId);

/// <summary>
/// Importador de temporadas (PRD §27, Fase 4 — "Importación histórica", §39 Data Layer: SQLite/CSV).
/// Escribe en data/pcrm.sqlite temporadas completas (equipos + corredores) desde datos planos,
/// de modo que el motor las cargue por season_id sin recompilar. Determinista y testeable.
/// </summary>
public sealed class SeasonImporter : IDisposable
{
    private readonly SqliteConnection _con;

    private SeasonImporter(SqliteConnection con)
    {
        _con = con;
        EnsureSchema();
    }

    /// <summary>Abre (o crea) la base de datos y prepara el esquema.</summary>
    public static SeasonImporter Open(string dbPath, bool overwrite)
    {
        if (overwrite && File.Exists(dbPath)) File.Delete(dbPath);
        var con = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        con.Open();
        return new SeasonImporter(con);
    }

    /// <summary>Añade (o actualiza) una temporada. Devuelve su season_id.</summary>
    public int UpsertSeason(int seasonId, string name, int year)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO seasons(id, name, year) VALUES ($id, $name, $year)
            ON CONFLICT(id) DO UPDATE SET name=$name, year=$year";
        cmd.Parameters.AddWithValue("$id", seasonId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$year", year);
        cmd.ExecuteNonQuery();
        return seasonId;
    }

    /// <summary>Importa equipos y corredores de una temporada (sustituye lo existente de la misma temporada).</summary>
    public void ImportSeason(IEnumerable<ImportTeam> teams, IEnumerable<ImportRider> riders)
    {
        using var tx = _con.BeginTransaction();
        int seasonId = teams.Select(t => t.SeasonId).Concat(riders.Select(r => r.SeasonId)).First();

        using (var delete = _con.CreateCommand())
        {
            delete.CommandText = "DELETE FROM riders WHERE season_id = $s; DELETE FROM teams WHERE season_id = $s;";
            delete.Parameters.AddWithValue("$s", seasonId);
            delete.ExecuteNonQuery();
        }

        foreach (var t in teams)
        {
            using var cmd = _con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO teams(id, name, abbr, country, category, season_id)
                VALUES (NULL, $name, $abbr, $country, $category, $s)";
            cmd.Parameters.AddWithValue("$name", t.Name);
            cmd.Parameters.AddWithValue("$abbr", t.Abbr);
            cmd.Parameters.AddWithValue("$country", (object?)t.Country ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$category", t.Category);
            cmd.Parameters.AddWithValue("$s", t.SeasonId);
            cmd.ExecuteNonQuery();
        }

        var teamByName = TeamsBySeason(seasonId);

        foreach (var r in riders)
        {
            if (!teamByName.TryGetValue(r.TeamName, out var tid))
                throw new InvalidOperationException($"Equipo desconocido en importación: '{r.TeamName}'.");
            using var cmd = _con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO riders(
                    id, name, birth_date, nationality, team_id, season_id, number, roles,
                    fla, mnt, mm, hil, ttr, prl, cob, spr, acc, dhi, att, sta, res, rec)
                VALUES (NULL, $name, $birth, $nat, $tid, $s, $num, $roles,
                    $fla, $mnt, $mm, $hil, $ttr, $prl, $cob, $spr, $acc, $dhi, $att,
                    $sta, $res, $rec)";
            cmd.Parameters.AddWithValue("$name", r.Name);
            cmd.Parameters.AddWithValue("$birth", (object?)r.BirthDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$nat", (object?)r.Nationality ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tid", tid);
            cmd.Parameters.AddWithValue("$s", r.SeasonId);
            cmd.Parameters.AddWithValue("$num", r.Number);
            cmd.Parameters.AddWithValue("$roles", JsonSerializer.Serialize(r.Roles));
            cmd.Parameters.AddWithValue("$fla", r.pFlat);
            cmd.Parameters.AddWithValue("$mnt", r.pMountain);
            cmd.Parameters.AddWithValue("$mm", r.pMediumMountain);
            cmd.Parameters.AddWithValue("$hil", r.pHill);
            cmd.Parameters.AddWithValue("$ttr", r.pTimeTrial);
            cmd.Parameters.AddWithValue("$prl", r.pPrologue);
            cmd.Parameters.AddWithValue("$cob", r.pCobbles);
            cmd.Parameters.AddWithValue("$spr", r.pSprint);
            cmd.Parameters.AddWithValue("$acc", r.pAcceleration);
            cmd.Parameters.AddWithValue("$dhi", r.pDescent);
            cmd.Parameters.AddWithValue("$att", r.pAttack);
            cmd.Parameters.AddWithValue("$sta", r.pEndurance);
            cmd.Parameters.AddWithValue("$res", r.pResistance);
            cmd.Parameters.AddWithValue("$rec", r.pRecovery);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private Dictionary<string, int> TeamsBySeason(int seasonId)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM teams WHERE season_id = $s";
        cmd.Parameters.AddWithValue("$s", seasonId);
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, int>();
        while (r.Read())
            map[r.GetString(1)] = r.GetInt32(0);
        return map;
    }

    private void EnsureSchema()
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS seasons (
                id INTEGER PRIMARY KEY,
                name TEXT,
                year INTEGER
            );
            CREATE TABLE IF NOT EXISTS teams (
                id INTEGER PRIMARY KEY,
                name TEXT,
                abbr TEXT,
                country TEXT,
                category TEXT,
                season_id INTEGER
            );
            CREATE TABLE IF NOT EXISTS riders (
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
            CREATE INDEX IF NOT EXISTS idx_riders_team ON riders(team_id);";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _con.Dispose();
}