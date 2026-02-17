using BlazorApp6.Models;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace BlazorApp6.Services
{
    public class SwapManager // Зареждане, запис и управление на данните за сваповете между учениците.
    {
        private readonly string connectionString;

        private readonly List<Swap> swaps = new();
        private readonly List<Swap> history = new();

        private readonly Dictionary<Guid, Swap> swapsById = new();
        private readonly Dictionary<Guid, Swap> historyById = new();

        private readonly Dictionary<(Guid, Guid), Swap> swapsByPair = new();
        private readonly Dictionary<Guid, List<Swap>> swapsByStudent = new();
        private readonly Dictionary<Guid, List<Swap>> historyByStudent = new();

        public string? DbError { get; private set; }

        public SwapManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");

            if (!LoadSwapsFromDb(out var loaded))
            {
                DbError = "Зареждането на данните за сваповете не беше успешно";
                return;
            }

            foreach (var s in loaded)
                AddSwapToCache(s);

            if (!LoadHistorySwapsFromDb(out var historyLoaded))
            {
                DbError = "Зареждането на историята не беше успешно";
                return;
            }

            foreach (var s in historyLoaded)
                AddHistoryToCache(s);
        }

        public Swap? RequestHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester, string? comment)
            => CreateSwapIfNotExists(requestingSt, helpingSt, subject, requester, comment);

        public Swap? OfferHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester, string? comment)
            => CreateSwapIfNotExists(requestingSt, helpingSt, subject, requester, comment);

        private Swap? CreateSwapIfNotExists(Student s1, Student s2, SubjectEnum subject, Student requester, string? comment)
        {
            var key = NormalizePair(s1.Id, s2.Id);

            if (swapsByPair.ContainsKey(key))
                return null;

            var swap = new Swap
            {
                Student1Id = s1.Id,
                Student2Id = s2.Id,
                RequesterId = requester.Id,
                SubjectForHelp = subject,
                DateRequested = DateTime.Now,
                Status = SwapStatus.Pending,
                Comment = comment
            };

            SaveSwapToDb(swap);
            AddSwapToCache(swap);

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
            MoveToHistory(swap);
            UpdateSwapInDb(swap);
        }

        public void CancelMyRequest(Swap swap)
        {
            RemoveSwapFromCache(swap);
            DeleteSwapFromDb(swap);
        }

        public void ProposeCompletingSwap(Swap swap, Guid proposerId)
        {
            swap.ProposeCompletion(proposerId);
            UpdateSwapInDb(swap);
        }

        public void AcceptCompletion(Swap swap)
        {
            swap.AcceptCompletion();
            UpdateSwapInDb(swap);
        }

        public void RejectCompletion(Swap swap)
        {
            swap.RejectCompletion();
            MoveToHistory(swap);
            UpdateSwapInDb(swap);
        }

        public void CompleteSwap(Swap swap)
        {
            RemoveSwapFromCache(swap);
            swap.CompleteSwap();
            AddHistoryToCache(swap);
            UpdateSwapInDb(swap);
        }


        public List<Swap> FindSwapsByStudentId(Guid studentId)
            => swapsByStudent.TryGetValue(studentId, out var list) ? list : new List<Swap>();

        public Swap? FindSwapByStudentsId(Guid s1, Guid s2)
        {
            swapsByPair.TryGetValue(NormalizePair(s1, s2), out var swap);
            return swap;
        }

        public List<Swap> FindHistorySwapsByStudentId(Guid studentId)
            => historyByStudent.TryGetValue(studentId, out var list) ? list : new List<Swap>();

        public Swap? FindHistorySwapByStudentsId(Guid s1, Guid s2)
        {
            foreach (var swap in FindHistorySwapsByStudentId(s1))
                if (IsSamePair(swap, s1, s2))
                    return swap;

            return null;
        }

        public Swap? FindSwapById(Guid id)
            => swapsById.TryGetValue(id, out var swap) ? swap : null;

        public Swap? FindHistorySwapById(Guid id)
            => historyById.TryGetValue(id, out var swap) ? swap : null;

        public List<Swap> GetAllSwaps() => swaps;
        public List<Swap> GetAllHistory() => history;

        public int GetNewSwapIncomesCount(Guid studentId)
        {
            if (!swapsByStudent.TryGetValue(studentId, out var list))
                return 0;

            int count = 0;

            foreach (var s in list)
                if (s.Status == SwapStatus.Pending && s.RequesterId != studentId)
                    count++;

            return count;
        }



        private void AddSwapToCache(Swap s)
        {
            swaps.Add(s);
            swapsById[s.Id] = s;

            swapsByPair[NormalizePair(s.Student1Id, s.Student2Id)] = s;

            AddToStudentIndex(swapsByStudent, s.Student1Id, s);
            AddToStudentIndex(swapsByStudent, s.Student2Id, s);
        }

        private void RemoveSwapFromCache(Swap s)
        {
            swaps.Remove(s);
            swapsById.Remove(s.Id);
            swapsByPair.Remove(NormalizePair(s.Student1Id, s.Student2Id));

            RemoveFromStudentIndex(swapsByStudent, s.Student1Id, s);
            RemoveFromStudentIndex(swapsByStudent, s.Student2Id, s);
        }

        private void MoveToHistory(Swap s)
        {
            RemoveSwapFromCache(s);
            AddHistoryToCache(s);
        }

        private void AddHistoryToCache(Swap s)
        {
            history.Add(s);
            historyById[s.Id] = s;

            AddToStudentIndex(historyByStudent, s.Student1Id, s);
            AddToStudentIndex(historyByStudent, s.Student2Id, s);
        }

        private static void AddToStudentIndex(Dictionary<Guid, List<Swap>> dict, Guid id, Swap swap)
        {
            if (!dict.TryGetValue(id, out var list))
                dict[id] = list = new List<Swap>();

            list.Add(swap);
        }

        private static void RemoveFromStudentIndex(Dictionary<Guid, List<Swap>> dict, Guid id, Swap swap)
        {
            if (dict.TryGetValue(id, out var list))
                list.Remove(swap);
        }



        public bool LoadSwapsFromDb(out List<Swap> result)
            => LoadByStatuses(out result, 0, 1, 3, 4);

        public bool LoadHistorySwapsFromDb(out List<Swap> result)
            => LoadByStatuses(out result, 2, 5);

        private bool LoadByStatuses(out List<Swap> result, params int[] statuses)
        {
            result = new List<Swap>();

            try
            {
                using var connection = CreateConnection();

                string sql = $@"SELECT * FROM ""Swaps"" WHERE ""Status"" = ANY(@statuses)";
                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@statuses", statuses);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(ReadSwap(reader));

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SaveSwapToDb(Swap swap)
        {
            using var connection = CreateConnection();

            const string sql = @"INSERT INTO ""Swaps""
            (""Id"",""Student1Id"",""Student2Id"",""Status"",""DateRequested"",
             ""DateConfirmed"",""SubjectForHelp"",""RequesterId"",
             ""CompletionProposedByStudentId"",""DateCompleted"",""Comment"")
            VALUES (@Id,@Student1Id,@Student2Id,@Status,@DateRequested,
                    @DateConfirmed,@SubjectForHelp,@RequesterId,
                    @CompletionProposedByStudentId,@DateCompleted,@Comment)";

            using var cmd = new NpgsqlCommand(sql, connection);
            FillParams(cmd, swap);
            cmd.ExecuteNonQuery();
        }

        public void UpdateSwapInDb(Swap swap)
        {
            using var connection = CreateConnection();

            const string sql = @"UPDATE ""Swaps""
                SET ""Status""=@Status,
                    ""DateConfirmed""=@DateConfirmed,
                    ""CompletionProposedByStudentId""=@CompletionProposedByStudentId,
                    ""DateCompleted""=@DateCompleted
                WHERE ""Id""=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);
            FillParams(cmd, swap);
            cmd.ExecuteNonQuery();
        }

        public void DeleteSwapFromDb(Swap swap)
        {
            using var connection = CreateConnection();
            using var cmd = new NpgsqlCommand(@"DELETE FROM ""Swaps"" WHERE ""Id""=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", swap.Id);
            cmd.ExecuteNonQuery();
        }

        private static Swap ReadSwap(NpgsqlDataReader r) => new Swap
        {
            Id = r.GetGuid(0),
            Student1Id = r.GetGuid(1),
            Student2Id = r.GetGuid(2),
            Status = (SwapStatus)r.GetInt32(3),
            DateRequested = r.GetDateTime(4),
            DateConfirmed = r.IsDBNull(5) ? null : r.GetDateTime(5),
            SubjectForHelp = (SubjectEnum)r.GetInt32(6),
            RequesterId = r.GetGuid(7),
            CompletionProposedByStudentId = r.IsDBNull(8) ? null : r.GetGuid(8),
            DateCompleted = r.IsDBNull(9) ? null : r.GetDateTime(9),
            Comment = r.IsDBNull(10) ? null : r.GetString(10)
        };

        private static void FillParams(NpgsqlCommand cmd, Swap s)
        {
            cmd.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.Id;
            cmd.Parameters.Add("@Student1Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.Student1Id;
            cmd.Parameters.Add("@Student2Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.Student2Id;
            cmd.Parameters.Add("@Status", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)s.Status;
            cmd.Parameters.Add("@DateRequested", NpgsqlTypes.NpgsqlDbType.Timestamp).Value = s.DateRequested;
            cmd.Parameters.Add("@DateConfirmed", NpgsqlTypes.NpgsqlDbType.Timestamp).Value = (object?)s.DateConfirmed ?? DBNull.Value;
            cmd.Parameters.Add("@SubjectForHelp", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)s.SubjectForHelp;
            cmd.Parameters.Add("@RequesterId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.RequesterId;
            cmd.Parameters.Add("@CompletionProposedByStudentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = (object?)s.CompletionProposedByStudentId ?? DBNull.Value;
            cmd.Parameters.Add("@DateCompleted", NpgsqlTypes.NpgsqlDbType.Timestamp).Value = (object?)s.DateCompleted ?? DBNull.Value;
            cmd.Parameters.Add("@Comment", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)s.Comment ?? DBNull.Value;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        private NpgsqlConnection CreateConnection()
        {
            var c = new NpgsqlConnection(connectionString);
            c.Open();
            return c;
        }


        private static (Guid, Guid) NormalizePair(Guid a, Guid b)
            => a.CompareTo(b) < 0 ? (a, b) : (b, a);

        private static bool IsSamePair(Swap s, Guid a, Guid b)
            => (s.Student1Id == a && s.Student2Id == b) || (s.Student1Id == b && s.Student2Id == a);
    }

    public class SwapHub : Hub { }
}