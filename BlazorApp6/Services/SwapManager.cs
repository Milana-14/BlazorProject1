using BlazorApp6.Components.Models;
using Npgsql;
using System.Collections.Generic;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class SwapManager // За управление на матчовете межд учениците
    {
        private readonly string connectionString;
        private List<Swap> swaps = new List<Swap>(); // Само за свапове със статус Pending или Confirmed
        private List<Swap> history = new List<Swap>(); // Само за свапове със статус Rejected, Completed или Canceled (отменените свапове изобщо не се записват - няма смис)
        public string? DbError { get; private set; }
        public SwapManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");

            if (!LoadSwapsFromDb(out List<Swap> loadedMatchesFromDb))
            {
                DbError = "Зареждането на данните за сваповете не беше успешно";
                return;
            }
            swaps = loadedMatchesFromDb;

            if (!LoadHistorySwapsFromDb(out List<Swap> historyFromDb))
            {
                DbError = "Зареждането на данните за историята на сваповете не беше успешно";
                return;
            }
            history = historyFromDb;
        }

        public Swap? RequestHelp(Student requestingSt, Student helpingSt, Subject subject)
        {
            if (swaps.FirstOrDefault(m =>
                (m.Student1Id == requestingSt.Id && m.Student2Id == helpingSt.Id) ||
                (m.Student1Id == helpingSt.Id && m.Student2Id == requestingSt.Id)) != null) return null;

            Swap swap = new Swap
            {
                Student1Id = requestingSt.Id,
                Student2Id = helpingSt.Id,
                SubjectForHelp = subject,
                DateRequested = DateTime.Now,
                Status = SwapStatus.Pending
            };

            swaps.Add(swap);
            SaveSwapToDb(swap);
            return swap;
        }
        public Swap? OfferHelp(Student requestingSt, Student helpingSt, Subject subject)
        {
            if (swaps.FirstOrDefault(m =>
                (m.Student1Id == requestingSt.Id && m.Student2Id == helpingSt.Id) ||
                (m.Student1Id == helpingSt.Id && m.Student2Id == requestingSt.Id)) != null) return null;
            Swap swap = new Swap
            {
                Student1Id = requestingSt.Id,
                Student2Id = helpingSt.Id,
                SubjectForHelp = subject,
                DateRequested = DateTime.Now,
                Status = SwapStatus.Pending
            };
            swaps.Add(swap);
            SaveSwapToDb(swap);
            return swap;
        }
        public void ConfirmSwap(Swap swap)
        {
            swap.Confirm();
            UpdateSwapInDb(swap);
        }
        public void RejectSwap(Swap swap)
        {
            swap.Reject();
            swaps.Remove(swap);

            if (!history.Any(m => m.Id == swap.Id)) history.Add(swap);

            UpdateSwapInDb(swap);
        }

        public void CancelMyRequest(Swap swap)
        {
            swaps.Remove(swap);
            DeleteSwapFromDb(swap);
        }

        public void CompleteSwap(Swap swap)
        {
            swaps.Remove(swap);
            swap.CompleteSwap();

            if (!history.Any(m => m.Id == swap.Id)) history.Add(swap);

            UpdateSwapInDb(swap);
        }
        public List<Swap> FindSwapsByStudent(Guid studentId)
        {
            return swaps.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Swap? FindSwapsById(Guid id)
        {
            return swaps.FirstOrDefault(m => m.Id == id);
        }
        public List<Swap> GetAllSwaps()
        {
            return swaps;
        }
        public List<Swap> GetAllHistory()
        {
            return history;
        }



        // Работа с база данни
        public bool LoadSwapsFromDb(out List<Swap> loadedMatchesFromDb)
        {
            loadedMatchesFromDb = new List<Swap>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Matches"" WHERE ""Status"" = 0 OR ""Status"" = 1", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Swap match = new Swap();
                    match.Id = reader.GetGuid(0);
                    match.Student1Id = reader.GetGuid(1);
                    match.Student2Id = reader.GetGuid(2);
                    match.Status = (SwapStatus)reader.GetInt32(3);
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
        public bool LoadHistorySwapsFromDb(out List<Swap> historyFromDb)
        {
            historyFromDb = new List<Swap>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Matches"" WHERE ""Status"" = 2 OR ""Status"" = 3", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Swap match = new Swap();
                    match.Id = reader.GetGuid(0);
                    match.Student1Id = reader.GetGuid(1);
                    match.Student2Id = reader.GetGuid(2);
                    match.Status = (SwapStatus)reader.GetInt32(3);
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
        public void SaveSwapToDb(Swap match)
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
        public void UpdateSwapInDb(Swap match)
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
        public void DeleteSwapFromDb(Swap match)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"DELETE FROM ""Matches"" WHERE ""Id""=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", match.Id);
            cmd.ExecuteNonQuery();
        }
    }
}
