using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProCycling.Core.Models;

namespace ProCycling.Core.Data;

/// <summary>Carga de datos maestros desde SQLite (generado por tools/import_data.py).</summary>
public static class SqliteStore
{
    public static (List<Team> Teams, List<Rider> Riders) LoadSeason(string dbPath, int seasonId)
    {
        var teams = new List<Team>();
        var riders = new List<Rider>();

        using var con = new SqliteConnection($"Data Source={dbPath}");
        con.Open();

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText =
                "SELECT id, name, abbr, country, category, season_id FROM teams WHERE season_id = $s";
            cmd.Parameters.AddWithValue("$s", seasonId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                teams.Add(new Team
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Abbr = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                    Country = r.IsDBNull(3) ? null : r.GetString(3),
                    Category = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    SeasonId = r.GetInt32(5)
                });
            }
        }

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, name, birth_date, nationality, team_id, season_id, number, roles,
                       fla, mnt, mm, hil, ttr, prl, cob, spr, acc, dhi, att, sta, res, rec
                FROM riders WHERE season_id = $s";
            cmd.Parameters.AddWithValue("$s", seasonId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var rider = new Rider
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    BirthDate = r.IsDBNull(2) ? null : r.GetString(2),
                    Nationality = r.IsDBNull(3) ? null : r.GetString(3),
                    TeamId = r.GetInt32(4),
                    SeasonId = r.GetInt32(5),
                    Number = r.GetInt32(6)
                };
                string? rolesJson = r.IsDBNull(7) ? null : r.GetString(7);
                if (!string.IsNullOrEmpty(rolesJson))
                {
                    rider.Roles.UnionWith(JsonSerializer.Deserialize<string[]>(rolesJson) ?? Array.Empty<string>());
                }
                rider.Attributes = new Attributes
                {
                    Flat = r.GetInt32(8), Mountain = r.GetInt32(9), MediumMountain = r.GetInt32(10),
                    Hill = r.GetInt32(11), TimeTrial = r.GetInt32(12), Prologue = r.GetInt32(13),
                    Cobbles = r.GetInt32(14), Sprint = r.GetInt32(15), Acceleration = r.GetInt32(16),
                    Descent = r.GetInt32(17), Attack = r.GetInt32(18), Endurance = r.GetInt32(19),
                    Resistance = r.GetInt32(20), Recovery = r.GetInt32(21)
                };
                riders.Add(rider);
            }
        }

        return (teams, riders);
    }
}