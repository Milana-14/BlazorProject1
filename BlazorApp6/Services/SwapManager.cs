using BlazorApp6.Models;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System.Threading.Tasks;

namespace BlazorApp6.Services
{
    public class SwapManager // За управление на матчовете межд учениците
    {
        private readonly string connectionString;
        public string? DbError { get; private set; }
        public SwapManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
        }

        public async Task<Swap?> RequestHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester, string? comment, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            try
            {
                const string checkSql = @"SELECT 1
                                          FROM ""Swaps""
                                          WHERE (""Student1Id"" = @s1 AND ""Student2Id"" = @s2)
                                          OR (""Student1Id"" = @s2 AND ""Student2Id"" = @s1)
                                          LIMIT 1;";

                await using (var checkCmd = new NpgsqlCommand(checkSql, connection, tx))
                {
                    checkCmd.Parameters.AddWithValue("@s1", requestingSt.Id);
                    checkCmd.Parameters.AddWithValue("@s2", helpingSt.Id);

                    if (await checkCmd.ExecuteScalarAsync(ct) != null)
                    {
                        await tx.RollbackAsync(ct);
                        return null;
                    }
                }

                var newSwap = new Swap
                {
                    Id = Guid.NewGuid(),
                    Student1Id = requestingSt.Id,
                    Student2Id = helpingSt.Id,
                    RequesterId = requester.Id,
                    SubjectForHelp = subject,
                    DateRequested = DateTime.UtcNow,
                    Status = SwapStatus.Pending,
                    Comment = comment
                };

                const string insertSql = @"INSERT INTO ""Swaps""
                                        (""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
                                        ""DateRequested"", ""SubjectForHelp"", ""RequesterId"", ""Comment"")
                                        VALUES (@Id, @Student1Id, @Student2Id, @Status,
                                        @DateRequested, @SubjectForHelp, @RequesterId, @Comment);";

                await using (var insertCmd = new NpgsqlCommand(insertSql, connection, tx))
                {
                    insertCmd.Parameters.AddWithValue("@Id", newSwap.Id);
                    insertCmd.Parameters.AddWithValue("@Student1Id", newSwap.Student1Id);
                    insertCmd.Parameters.AddWithValue("@Student2Id", newSwap.Student2Id);
                    insertCmd.Parameters.AddWithValue("@Status", (int)newSwap.Status);
                    insertCmd.Parameters.AddWithValue("@DateRequested", newSwap.DateRequested);
                    insertCmd.Parameters.AddWithValue("@SubjectForHelp", (int)newSwap.SubjectForHelp);
                    insertCmd.Parameters.AddWithValue("@RequesterId", newSwap.RequesterId);
                    insertCmd.Parameters.AddWithValue("@Comment", (object?)newSwap.Comment ?? DBNull.Value);

                    await insertCmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                return newSwap;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<Swap?> OfferHelp(Student requestingSt, Student helpingSt, SubjectEnum subject, Student requester, string? comment, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            try
            {
                const string checkSql = @"SELECT 1
                                          FROM ""Swaps""
                                          WHERE (""Student1Id"" = @s1 AND ""Student2Id"" = @s2)
                                          OR (""Student1Id"" = @s2 AND ""Student2Id"" = @s1)
                                          LIMIT 1;";
                await using (var checkCmd = new NpgsqlCommand(checkSql, connection, tx))
                {
                    checkCmd.Parameters.AddWithValue("@s1", requestingSt.Id);
                    checkCmd.Parameters.AddWithValue("@s2", helpingSt.Id);

                    if (await checkCmd.ExecuteScalarAsync(ct) != null)
                    {
                        await tx.RollbackAsync(ct);
                        return null;
                    }

                    var newSwap = new Swap
                    {
                        Id = Guid.NewGuid(),
                        Student1Id = requestingSt.Id,
                        Student2Id = helpingSt.Id,
                        RequesterId = requester.Id,
                        SubjectForHelp = subject,
                        DateRequested = DateTime.UtcNow,
                        Status = SwapStatus.Pending,
                        Comment = comment
                    };

                    var insertSql = @"INSERT INTO ""Swaps""
                                        (""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
                                        ""DateRequested"", ""SubjectForHelp"", ""RequesterId"", ""Comment"")
                                        VALUES (@Id, @Student1Id, @Student2Id, @Status,
                                        @DateRequested, @SubjectForHelp, @RequesterId, @Comment);";

                    await using (var insertCmd = new NpgsqlCommand(insertSql, connection, tx))
                    {
                        insertCmd.Parameters.AddWithValue("@Id", newSwap.Id);
                        insertCmd.Parameters.AddWithValue("@Student1Id", newSwap.Student1Id);
                        insertCmd.Parameters.AddWithValue("@Student2Id", newSwap.Student2Id);
                        insertCmd.Parameters.AddWithValue("@Status", (int)newSwap.Status);
                        insertCmd.Parameters.AddWithValue("@DateRequested", newSwap.DateRequested);
                        insertCmd.Parameters.AddWithValue("@SubjectForHelp", (int)newSwap.SubjectForHelp);
                        insertCmd.Parameters.AddWithValue("@RequesterId", newSwap.RequesterId);
                        insertCmd.Parameters.AddWithValue("@Comment", (object?)newSwap.Comment ?? DBNull.Value);

                        await insertCmd.ExecuteNonQueryAsync(ct);
                    }

                    await tx.CommitAsync(ct);
                    return newSwap;
                }
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public void ConfirmSwap(Swap swap)
        {
            swap.Confirm();
            UpdateSwapInDb(swap);
        }
        public void RejectSwap(Swap swap)
        {
            swap.Reject();
            UpdateSwapInDb(swap);
        }

        public void ProposeCompletingSwap(Swap swap, Guid proposerId)
        {
            swap.ProposeCompletion(proposerId);
            UpdateSwapInDb(swap);
        }
        public void AcceptCompletion(Swap swap)
        {
            swap.AcceptCompletion(); // Тук свапът не се завършва докрай -> CompletedNotRated
            UpdateSwapInDb(swap);
        }
        public void RejectCompletion(Swap swap)
        {
            swap.RejectCompletion();
            UpdateSwapInDb(swap);
        }
        public void CompleteSwap(Swap swap) // Този метод се повиква в RateHelpManager след оценяване на помощта
        {
            swap.CompleteSwap();
            UpdateSwapInDb(swap);
        }


        public async Task<List<Swap>> FindSwapsByStudentId(Guid studentId, CancellationToken ct = default)
        {
            var foundSwaps = new List<Swap>();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE (""Student1Id"" = @studentId OR ""Student2Id"" = @studentId) AND ""Status"" NOT IN (2,5)
        ORDER BY ""DateRequested"" DESC;";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@studentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = studentId;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    var swap = new Swap
                    {
                        Id = reader.GetGuid(0),
                        Student1Id = reader.GetGuid(1),
                        Student2Id = reader.GetGuid(2),
                        Status = (SwapStatus)reader.GetInt32(3),
                        DateRequested = reader.GetDateTime(4),
                        DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                        RequesterId = reader.GetGuid(7),
                        CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                        DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                    };
                    foundSwaps.Add(swap);
                }
            }
            return foundSwaps;
        }

        public async Task<Swap?> FindSwapByStudentsId(Guid student1Id, Guid student2Id, CancellationToken ct = default)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE ((""Student1Id"" = @studentId AND ""Student2Id"" = @student2Id)
            OR (""Student1Id"" = @student2Id AND ""Student2Id"" = @studentId))
            AND ""Status"" NOT IN (2,5)";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@studentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = student1Id;
                cmd.Parameters.Add("@student2Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = student2Id;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                    return null;

                var swap = new Swap
                {
                    Id = reader.GetGuid(0),
                    Student1Id = reader.GetGuid(1),
                    Student2Id = reader.GetGuid(2),
                    Status = (SwapStatus)reader.GetInt32(3),
                    DateRequested = reader.GetDateTime(4),
                    DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                    RequesterId = reader.GetGuid(7),
                    CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
                return swap;
            }
        }

        public async Task<List<Swap>> FindHistorySwapsByStudentId(Guid studentId, CancellationToken ct = default)
        {
            var foundSwaps = new List<Swap>();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE  (""Student1Id"" = @studentId OR ""Student2Id"" = @studentId) AND ""Status"" IN (2,5)
        ORDER BY ""DateRequested"" DESC;";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@studentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = studentId;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    var swap = new Swap
                    {
                        Id = reader.GetGuid(0),
                        Student1Id = reader.GetGuid(1),
                        Student2Id = reader.GetGuid(2),
                        Status = (SwapStatus)reader.GetInt32(3),
                        DateRequested = reader.GetDateTime(4),
                        DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                        RequesterId = reader.GetGuid(7),
                        CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                        DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                    };
                    foundSwaps.Add(swap);
                }
            }
            return foundSwaps;
        }

        public async Task<Swap?> FindHistorySwapByStudentsId(Guid student1Id, Guid student2Id, CancellationToken ct = default)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE ((""Student1Id"" = @studentId AND ""Student2Id"" = @student2Id)
            OR (""Student1Id"" = @student2Id AND ""Student2Id"" = @studentId))
            AND ""Status"" IN (2,5)";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@studentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = student1Id;
                cmd.Parameters.Add("@student2Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = student2Id;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                    return null;

                var swap = new Swap
                {
                    Id = reader.GetGuid(0),
                    Student1Id = reader.GetGuid(1),
                    Student2Id = reader.GetGuid(2),
                    Status = (SwapStatus)reader.GetInt32(3),
                    DateRequested = reader.GetDateTime(4),
                    DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                    RequesterId = reader.GetGuid(7),
                    CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
                return swap;
            }
        }

        public async Task<Swap?> FindSwapById(Guid id, CancellationToken ct = default)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE ""Id"" = @id AND ""Status"" NOT IN (2,5)";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                if (!await reader.ReadAsync(ct))
                    return null;

                var swap = new Swap
                {
                    Id = reader.GetGuid(0),
                    Student1Id = reader.GetGuid(1),
                    Student2Id = reader.GetGuid(2),
                    Status = (SwapStatus)reader.GetInt32(3),
                    DateRequested = reader.GetDateTime(4),
                    DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                    RequesterId = reader.GetGuid(7),
                    CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
                return swap;
            }
        }

        public async Task<Swap?> FindHistorySwapById(Guid id, CancellationToken ct = default)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT ""Id"", ""Student1Id"", ""Student2Id"", ""Status"",
               ""DateRequested"", ""DateConfirmed"",
               ""SubjectForHelp"", ""RequesterId"",
               ""CompletionProposedByStudentId"",
               ""DateCompleted"", ""Comment""
        FROM ""Swaps""
        WHERE ""Id"" = @id AND ""Status"" IN (2,5)";

            await using (var cmd = new NpgsqlCommand(sql, connection))
            {
                cmd.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = id;
                await cmd.PrepareAsync(ct);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                var swap = new Swap
                {
                    Id = reader.GetGuid(0),
                    Student1Id = reader.GetGuid(1),
                    Student2Id = reader.GetGuid(2),
                    Status = (SwapStatus)reader.GetInt32(3),
                    DateRequested = reader.GetDateTime(4),
                    DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                    RequesterId = reader.GetGuid(7),
                    CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
                return swap;
            }
        }

        public async Task<int> GetNewSwapIncomesCount(Guid studentId, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"SELECT COUNT(*)
                                        FROM ""Swaps""
                                        WHERE ""Status"" = 0
                                        AND ""RequesterId"" != @studentId
                                        AND (""Student1Id"" = @studentId OR ""Student2Id"" = @studentId);";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@studentId", studentId);
            var result = await cmd.ExecuteScalarAsync(ct);

            return Convert.ToInt32(result);
        }

        public async Task<List<Swap>> LoadSwapsFromDb(CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            try
            {
                List<Swap> loadedSwapsFromDb = new();

                await using (var cmd = new NpgsqlCommand(@"SELECT * FROM ""Swaps"" WHERE ""Status"" IN (0, 1, 3, 4)", connection))
                {
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync())
                    {
                        var swap = new Swap
                        {
                            Id = reader.GetGuid(0),
                            Student1Id = reader.GetGuid(1),
                            Student2Id = reader.GetGuid(2),
                            Status = (SwapStatus)reader.GetInt32(3),
                            DateRequested = reader.GetDateTime(4),
                            DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                            RequesterId = reader.GetGuid(7),
                            CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                            DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                            Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                        };

                        loadedSwapsFromDb.Add(swap);
                    }
                }
                return loadedSwapsFromDb;
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<Swap>> LoadHistorySwapsFromDb(CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            List<Swap> historyFromDb = new();

            try
            {
                await using (var cmd = new NpgsqlCommand(@"SELECT * FROM ""Swaps"" WHERE ""Status"" IN (2, 5)", connection))
                {
                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync())
                    {
                        var swap = new Swap
                        {
                            Id = reader.GetGuid(0),
                            Student1Id = reader.GetGuid(1),
                            Student2Id = reader.GetGuid(2),
                            Status = (SwapStatus)reader.GetInt32(3),
                            DateRequested = reader.GetDateTime(4),
                            DateConfirmed = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            SubjectForHelp = (SubjectEnum)reader.GetInt32(6),
                            RequesterId = reader.GetGuid(7),
                            CompletionProposedByStudentId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                            DateCompleted = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                            Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
                        };

                        historyFromDb.Add(swap);
                    }
                }
                return historyFromDb;
            }
            catch
            {
                throw;
            }
        }

        public async Task SaveSwapToDb(Swap swap, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            try
            {
                string insertSql = @"INSERT INTO ""Swaps"" (""Id"", ""Student1Id"", ""Student2Id"", ""Status"", ""DateRequested"", ""DateConfirmed"", ""SubjectForHelp"", ""RequesterId"", ""CompletionProposedByStudentId"", ""DateCompleted"", ""Comment"") 
                                VALUES (@Id, @Student1Id, @Student2Id, @Status, @DateRequested, @DateConfirmed, @SubjectForHelp, @RequesterId, @CompletionProposedByStudentId, @DateCompleted, @Comment)";

                await using (var cmd = new NpgsqlCommand(insertSql, connection, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", swap.Id);
                    cmd.Parameters.AddWithValue("@Student1Id", swap.Student1Id);
                    cmd.Parameters.AddWithValue("@Student2Id", swap.Student2Id);
                    cmd.Parameters.AddWithValue("@Status", (int)swap.Status);
                    cmd.Parameters.AddWithValue("@DateRequested", swap.DateRequested);
                    cmd.Parameters.AddWithValue("@DateConfirmed", (object?)swap.DateConfirmed ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubjectForHelp", (int)swap.SubjectForHelp);
                    cmd.Parameters.AddWithValue("@RequesterId", swap.RequesterId);
                    cmd.Parameters.AddWithValue("@CompletionProposedByStudentId", (object?)swap.CompletionProposedByStudentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCompleted", (object?)swap.DateCompleted ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Comment", (object?)swap.Comment ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        public async Task UpdateSwapInDb(Swap swap, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            try
            {
                string sql = @"UPDATE ""Swaps""
                            SET ""Status"" = @Status, ""DateConfirmed"" = @DateConfirmed, ""CompletionProposedByStudentId"" = @CompletionProposedByStudentId, ""DateCompleted"" = @DateCompleted WHERE ""Id""=@Id";

                await using (var cmd = new NpgsqlCommand(sql, connection, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", swap.Id);
                    cmd.Parameters.AddWithValue("@Status", (int)swap.Status);
                    cmd.Parameters.AddWithValue("@DateConfirmed", (object?)swap.DateConfirmed ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompletionProposedByStudentId", (object?)swap.CompletionProposedByStudentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCompleted", (object?)swap.DateCompleted ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        public async Task DeleteSwapFromDb(Swap swap, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);

            try
            {
                await using (var cmd = new NpgsqlCommand(@"DELETE FROM ""Swaps"" WHERE ""Id""=@Id", connection, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", swap.Id);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }

    public class SwapHub : Hub
    {
    }
}
