using BlazorApp6.Components.Models;
using Npgsql;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class MatchManager // За управление на матчовете межд учениците
    {
        private readonly string connectionString;
        private List<Match> matches;
        private List<Match> history;
        public string? DbError { get; private set; }
        public MatchManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            try
            {
                matches = LoadMatchesFromDb();
                history = LoadHistoryMatchesFromDb();
                DbError = null;
            }
            catch (ApplicationException ex)
            {
                matches = new List<Match>();
                history = new List<Match>();
                DbError = ex.Message;
            }
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

            try
            {
                matches.Add(match);
                SaveMatchToDb(match);
                return match;
            }
            catch (Exception ex)
            {
                matches.Remove(match);
                DbError = ex.Message;
                return null;
            }
        }
        public bool ConfirmMatch(Match match)
        {
            try
            {
                match.Confirm();
                UpdateMatchInDb(match);
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }
        public bool RejectMatch(Match match)
        {
            try
            {
                match.Reject();
                matches.Remove(match);

                if (!history.Any(m => m.Id == match.Id)) history.Add(match);

                UpdateMatchInDb(match);
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }

        public bool CancelMatchRequest(Match match)
        {
            try
            {
                matches.Remove(match);
                DeleteMatchFromDb(match);
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }

        public bool UnpairStudents(Match match)
        {
            try
            {
                matches.Remove(match);
                match.Unpair();

                if (!history.Any(m => m.Id == match.Id)) history.Add(match);

                UpdateMatchInDb(match);
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }
        public List<Match> FindMatchesByStudent(Guid studentId)
        {
            return matches.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Match FindMatchById(Guid id)
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
        public List<Match> LoadMatchesFromDb()
        {
            List<Match> matches = new List<Match>();

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"SELECT * FROM ""Matches"" WHERE Status = 0 OR Status = 1";

            using var cmd = new NpgsqlCommand(sql, connection);
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

                matches.Add(match);
            }
            return matches;
        }
        public List<Match> LoadHistoryMatchesFromDb()
        {
            List<Match> history = new List<Match>();

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"SELECT * FROM ""Matches"" WHERE Status = 2 OR Status = 3";

            using var cmd = new NpgsqlCommand(sql, connection);
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

                history.Add(match);
            }
            return history;
        }
        public void SaveMatchToDb(Match match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"INSERT INTO ""Matches"" 
                           (Id, Student1Id, Student2Id, Status, DateRequested, DateConfirmed)
                           VALUES (@Id, @Student1Id, @Student2Id, @Status, @DateRequested, @DateConfirmed)";

            using var cmd = new NpgsqlCommand(sql, connection);

            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.Parameters.AddWithValue("@Student1Id", match.Student1Id);
            cmd.Parameters.AddWithValue("@Student2Id", match.Student2Id);
            cmd.Parameters.AddWithValue("@Status", (int)match.Status);
            cmd.Parameters.AddWithValue("@DateRequested", match.DateRequested);
            cmd.Parameters.AddWithValue("@DateConfirmed", (object?)match.DateConfirmed ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
        public void UpdateMatchInDb(Match match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"UPDATE ""Matches"" 
                            SET Student1Id=@Student1Id, Student2Id=@Student2Id, Status=@Status,
                                 DateRequested=@DateRequested, DateConfirmed=@DateConfirmed
                            WHERE Id=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);

            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.Parameters.AddWithValue("@Student1Id", match.Student1Id);
            cmd.Parameters.AddWithValue("@Student2Id", match.Student2Id);
            cmd.Parameters.AddWithValue("@Status", (int)match.Status);
            cmd.Parameters.AddWithValue("@DateRequested", match.DateRequested);
            cmd.Parameters.AddWithValue("@DateConfirmed", (object?)match.DateConfirmed ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
        public void DeleteMatchFromDb(Match match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"DELETE FROM ""Matches"" WHERE Id=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.ExecuteNonQuery();
        }
    }
}
