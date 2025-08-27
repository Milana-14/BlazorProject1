using BlazorApp6.Components.Models;
using Npgsql;
using System.Collections.Generic;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class MatchManager // За управление на матчовете межд учениците
    {
        private readonly string connectionString;
        private List<Match> matches = new List<Match>(); // Само за матчове със статус Pending или Confirmed
        private List<Match> history = new List<Match>(); // Само за матчове със статус Rejected или Unpaired (отменените матчове изобщо не се записват - няма смис)
        public string? DbError { get; private set; }
        public MatchManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");

            if (!LoadMatchesFromDb(out List<Match> loadedMatchesFromDb))
            {
                DbError = "Зареждането на данните за матчовете не беше успешно";
                return;
            }
            matches = loadedMatchesFromDb;

            if (!LoadHistoryMatchesFromDb(out List<Match> historyFromDb))
            {
                DbError = "Зареждането на данните за историята на матчовете не беше успешно";
                return;
            }
            history = historyFromDb;
        }

        public Match? RequestMatch(Student student1, Student student2)
        {
            if (matches.FirstOrDefault(m =>
                (m.Student1Id == student1.Id && m.Student2Id == student2.Id) ||
                (m.Student1Id == student2.Id && m.Student2Id == student1.Id)) != null) return null;

            Match match = new Match
            {
                Student1Id = student1.Id,
                Student2Id = student2.Id,
                DateRequested = DateTime.Now,
                Status = MatchStatus.Pending
            };

            matches.Add(match);
            SaveMatchToDb(match);
            return match;
        }
        public void ConfirmMatch(Match match)
        {
            match.Confirm();
            UpdateMatchInDb(match);
        }
        public void RejectMatch(Match match)
        {
            match.Reject();
            matches.Remove(match);

            if (!history.Any(m => m.Id == match.Id)) history.Add(match);

            UpdateMatchInDb(match);
        }

        public void CancelMyRequest(Match match)
        {
            matches.Remove(match);
            DeleteMatchFromDb(match);
        }

        public void UnpairStudents(Match match)
        {
            matches.Remove(match);
            match.Unpair();

            if (!history.Any(m => m.Id == match.Id)) history.Add(match);

            UpdateMatchInDb(match);
        }
        public List<Match> FindMatchesByStudent(Guid studentId)
        {
            return matches.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Match? FindMatchById(Guid id)
        {
            return matches.FirstOrDefault(m => m.Id == id);
        }
        public List<Match> GetAllMatches()
        {
            return matches;
        }
        public List<Match> GetAllHistory()
        {
            return history;
        }



        // Работа с база данни
        public bool LoadMatchesFromDb(out List<Match> loadedMatchesFromDb)
        {
            loadedMatchesFromDb = new List<Match>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Matches"" WHERE ""Status"" = 0 OR ""Status"" = 1", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Match match = new Match();
                    match.Id = reader.GetGuid(0);
                    match.Student1Id = reader.GetGuid(1);
                    match.Student2Id = reader.GetGuid(2);
                    match.Status = (MatchStatus)reader.GetInt32(3);
                    match.DateRequested = reader.GetDateTime(4);
                    match.DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5);

                    loadedMatchesFromDb.Add(match);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool LoadHistoryMatchesFromDb(out List<Match> historyFromDb)
        {
            historyFromDb = new List<Match>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Matches"" WHERE ""Status"" = 2 OR ""Status"" = 3", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Match match = new Match();
                    match.Id = reader.GetGuid(0);
                    match.Student1Id = reader.GetGuid(1);
                    match.Student2Id = reader.GetGuid(2);
                    match.Status = (MatchStatus)reader.GetInt32(3);
                    match.DateRequested = reader.GetDateTime(4);
                    match.DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5);

                    historyFromDb.Add(match);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public void SaveMatchToDb(Match match)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO ""Matches"" (""Id"", ""Student1Id"", ""Student2Id"", ""Status"", ""DateRequested"", ""DateConfirmed"") 
                                VALUES (@Id, @Student1Id, @Student2Id, @Status, @DateRequested, @DateConfirmed)";


                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", match.Id);
                cmd.Parameters.AddWithValue("@Student1Id", match.Student1Id);
                cmd.Parameters.AddWithValue("@Student2Id", match.Student2Id);
                cmd.Parameters.AddWithValue("@Status", (int)match.Status);
                cmd.Parameters.AddWithValue("@DateRequested", match.DateRequested);
                cmd.Parameters.AddWithValue("@DateConfirmed", (object?)match.DateConfirmed ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }
        public void UpdateMatchInDb(Match match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"UPDATE ""Matches""
                            SET ""Status"" = @Status, ""DateConfirmed"" = @DateConfirmed WHERE ""Id""=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);

            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.Parameters.AddWithValue("@Status", (int)match.Status);
            cmd.Parameters.AddWithValue("@DateConfirmed", (object?)match.DateConfirmed ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
        public void DeleteMatchFromDb(Match match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"DELETE FROM ""Matches"" WHERE ""Id""=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.ExecuteNonQuery();
        }
    }
}
