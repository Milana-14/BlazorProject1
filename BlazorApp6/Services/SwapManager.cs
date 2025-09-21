using BlazorApp6.Models;
using Npgsql;

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

        public Swap? RequestHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester)
        {
            if (swaps.FirstOrDefault(m =>
                (m.Student1Id == requestingSt.Id && m.Student2Id == helpingSt.Id) ||
                (m.Student1Id == helpingSt.Id && m.Student2Id == requestingSt.Id)) != null) return null;

            Swap swap = new Swap
            {
                Student1Id = requestingSt.Id,
                Student2Id = helpingSt.Id,
                RequesterId = requester.Id,
                SubjectForHelp = subject,
                DateRequested = DateTime.UtcNow,
                Status = SwapStatus.Pending
            };

            swaps.Add(swap);
            SaveSwapToDb(swap);
            return swap;
        }
        public Swap? OfferHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester)
        {
            if (swaps.FirstOrDefault(m =>
                (m.Student1Id == requestingSt.Id && m.Student2Id == helpingSt.Id) ||
                (m.Student1Id == helpingSt.Id && m.Student2Id == requestingSt.Id)) != null) return null;

            Swap swap = new Swap
            {
                Student1Id = requestingSt.Id,
                Student2Id = helpingSt.Id,
                RequesterId = requester.Id,
                SubjectForHelp = subject,
                DateRequested = DateTime.UtcNow,
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
        public List<Swap> FindSwapsByStudentId(Guid studentId)
        {
            return swaps.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Swap? FindSwapByStudentsId(Guid student1Id, Guid student2Id)
        {
            return swaps.Where(m => (m.Student1Id == student1Id && m.Student2Id == student2Id) || (m.Student1Id == student2Id && m.Student2Id == student1Id)).FirstOrDefault();
        }
        public Swap? FindSwapById(Guid id)
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
        public bool LoadSwapsFromDb(out List<Swap> loadedSwapsFromDb)
        {
            loadedSwapsFromDb = new List<Swap>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Swaps"" WHERE ""Status"" = 0 OR ""Status"" = 1 OR ""Status"" = 3 OR ""Status"" = 4", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {


                    Swap swap = new Swap();
                    swap.Id = reader.GetGuid(0);
                    swap.Student1Id = reader.GetGuid(1);
                    swap.Student2Id = reader.GetGuid(2);
                    swap.Status = (SwapStatus)reader.GetInt32(3);
                    swap.DateRequested = reader.GetDateTime(4);
                    swap.DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    swap.SubjectForHelp = (SubjectEnum)reader.GetInt32(6);
                    swap.RequesterId = reader.GetGuid(7);

                    loadedSwapsFromDb.Add(swap);
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

                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Swaps"" WHERE ""Status"" = 2 OR ""Status"" = 5", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Swap swap = new Swap();
                    swap.Id = reader.GetGuid(0);
                    swap.Student1Id = reader.GetGuid(1);
                    swap.Student2Id = reader.GetGuid(2);
                    swap.Status = (SwapStatus)reader.GetInt32(3);
                    swap.DateRequested = reader.GetDateTime(4);
                    swap.DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    swap.SubjectForHelp = (SubjectEnum)reader.GetInt32(6);
                    swap.RequesterId = reader.GetGuid(7);


                    historyFromDb.Add(swap);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public void SaveSwapToDb(Swap swap)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO ""Swaps"" (""Id"", ""Student1Id"", ""Student2Id"", ""Status"", ""DateRequested"", ""DateConfirmed"", ""SubjectForHelp"", ""RequesterId"") 
                                VALUES (@Id, @Student1Id, @Student2Id, @Status, @DateRequested, @DateConfirmed, @SubjectForHelp, @RequesterId)";


                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", swap.Id);
                cmd.Parameters.AddWithValue("@Student1Id", swap.Student1Id);
                cmd.Parameters.AddWithValue("@Student2Id", swap.Student2Id);
                cmd.Parameters.AddWithValue("@Status", (int)swap.Status);
                cmd.Parameters.AddWithValue("@DateRequested", swap.DateRequested);
                cmd.Parameters.AddWithValue("@DateConfirmed", (object?)swap.DateConfirmed ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SubjectForHelp", (int)swap.SubjectForHelp);
                cmd.Parameters.AddWithValue("@RequesterId", swap.RequesterId);

                cmd.ExecuteNonQuery();
            }
        }
        public void UpdateSwapInDb(Swap swap)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"UPDATE ""Swaps""
                            SET ""Status"" = @Status, ""DateConfirmed"" = @DateConfirmed WHERE ""Id""=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);

            cmd.Parameters.AddWithValue("@Id", swap.Id);
            cmd.Parameters.AddWithValue("@Status", (int)swap.Status);
            cmd.Parameters.AddWithValue("@DateConfirmed", (object?)swap.DateConfirmed ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
        public void DeleteSwapFromDb(Swap swap)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"DELETE FROM ""Swaps"" WHERE ""Id""=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", swap.Id);
            cmd.ExecuteNonQuery();
        }
    }
}
