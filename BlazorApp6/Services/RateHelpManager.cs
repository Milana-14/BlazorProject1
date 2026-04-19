using BlazorApp6.Models;
using Npgsql;
using System.Data;

namespace BlazorApp6.Services
{
    public class RateHelpManager // Записване (в бд) и управление на ревютата и оценките, които студентите дават един на друг след завършване на суап.
    {
        private readonly string connectionString;
        private readonly SwapManager swapManager;

        public string? DbError { get; private set; }

        public RateHelpManager(IConfiguration config, SwapManager swapManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");

            this.swapManager = swapManager;
        }


        public void RateSwap(Swap swap, string comment, int rating)
        {
            if (swap == null) return;

            var review = new Review
            {
                Id = Guid.NewGuid(),
                Comment = comment,
                Rating = rating,
                SenderStudentId = swap.Student1Id,
                ReceiverStudentId = swap.Student2Id,
                CreatedAt = DateTime.UtcNow,
                SwapId = swap.Id
            };

            if (SaveReviewToDb(review))
            {
                swapManager.CompleteSwap(swap);
            }
        }

        public List<Review> LoadReviewsForStudentFromDb(Guid receiverStudentId)
        {
            var reviews = new List<Review>(8);

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(@"
                    SELECT ""Id"", ""Comment"", ""Rating"", ""SenderStudentId"", ""ReceiverStudentId"", ""CreatedAt"", ""SwapId""
                    FROM ""Reviews""
                    WHERE ""ReceiverStudentId"" = @receiverStudentId", connection);

                command.Parameters.Add("@receiverStudentId", NpgsqlTypes.NpgsqlDbType.Uuid)
                                  .Value = receiverStudentId;

                command.Prepare();

                using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);

                while (reader.Read())
                {
                    reviews.Add(new Review
                    {
                        Id = reader.GetGuid(0),
                        Comment = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Rating = reader.GetInt32(2),
                        SenderStudentId = reader.GetGuid(3),
                        ReceiverStudentId = reader.GetGuid(4),
                        CreatedAt = reader.GetDateTime(5),
                        SwapId = reader.GetGuid(6)
                    });
                }
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждане на ревютата: " + ex.Message;
            }

            return reviews;
        }

        public Review LoadReviewForSwapFromDb(Guid swapId)
        {
            Review review = new Review();
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();
                using var command = new NpgsqlCommand(@"
                    SELECT ""Id"", ""Comment"", ""Rating"", ""SenderStudentId"", ""ReceiverStudentId"", ""CreatedAt"", ""SwapId""
                    FROM ""Reviews""
                    WHERE ""SwapId"" = @swapId", connection);
                command.Parameters.Add("@swapId", NpgsqlTypes.NpgsqlDbType.Uuid)
                                  .Value = swapId;
                command.Prepare();
                using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    review.Id = reader.GetGuid(0);
                    review.Comment = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    review.Rating = reader.GetInt32(2);
                    review.SenderStudentId = reader.GetGuid(3);
                    review.ReceiverStudentId = reader.GetGuid(4);
                    review.CreatedAt = reader.GetDateTime(5);
                    review.SwapId = reader.GetGuid(6);
                }
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждане на ревютата: " + ex.Message;
            }
            return review;
        }


        private bool SaveReviewToDb(Review review)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new NpgsqlCommand(@"
                    INSERT INTO ""Reviews""
                    (""Id"", ""Comment"", ""Rating"", ""SenderStudentId"", ""ReceiverStudentId"", ""CreatedAt"", ""SwapId"")
                    VALUES
                    (@Id, @Comment, @Rating, @SenderStudentId, @ReceiverStudentId, @CreatedAt, @SwapId)", connection);

                command.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = review.Id;
                command.Parameters.Add("@Comment", NpgsqlTypes.NpgsqlDbType.Text).Value = review.Comment ?? string.Empty;
                command.Parameters.Add("@Rating", NpgsqlTypes.NpgsqlDbType.Integer).Value = review.Rating;
                command.Parameters.Add("@SenderStudentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = review.SenderStudentId;
                command.Parameters.Add("@ReceiverStudentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = review.ReceiverStudentId;
                command.Parameters.Add("@CreatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = review.CreatedAt;
                command.Parameters.Add("@SwapId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = review.SwapId;

                command.Prepare();

                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при добавяне на ревю: " + ex.Message;
                return false;
            }
        }
    }
}