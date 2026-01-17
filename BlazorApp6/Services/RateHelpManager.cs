using BlazorApp6.Models;

namespace BlazorApp6.Services
{
    public class RateHelpManager
    {
        private readonly string connectionString;
        private SwapManager swapManager;
        public string? DbError { get; private set; }
        public RateHelpManager(IConfiguration config, SwapManager swapManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            this.swapManager = swapManager;
        }
        public void RateSwap(Swap swap, string comment, int rating)
        {
            if (swap == null) return;

            Review review = new Review
            {
                Id = Guid.NewGuid(),
                Comment = comment,
                Rating = rating,
                SenderStudentId = swap.Student1Id,
                ReceiverStudentId = swap.Student2Id,
                CreatedAt = DateTime.UtcNow
            };
            SaveReviewToDb(review);

            swapManager.CompleteSwap(swap);
        }

        public List<Review> LoadReviewsForStudentFromDb(Guid receiverStudentId)
        {
            List<Review> reviews = new List<Review>();
            try
            {
                using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new Npgsql.NpgsqlCommand(@"SELECT * FROM ""Reviews"" WHERE ""ReceiverStudentId""=@receiverStudentId", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Review review = new Review
                                {
                                    Id = reader.GetGuid(0),
                                    Comment = reader.GetString(1),
                                    Rating = reader.GetInt32(2),
                                    SenderStudentId = reader.GetGuid(3),
                                    ReceiverStudentId = reader.GetGuid(4),
                                    CreatedAt = reader.GetDateTime(5)
                                };
                                reviews.Add(review);
                            }
                        }
                    }
                }
                return reviews;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждане на ревютата от базата данни: " + ex.Message;
                return reviews;
            }
        }
        private bool SaveReviewToDb(Review review)
        {
            try
            {
                using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO ""Reviews"" (""Id"", ""Comment"", ""Rating"", ""SenderStudentId"", ""ReceiverStudentId"", ""CreatedAt"")
                                     VALUES (@Id, @Comment, @Rating, @SenderStudentId, @ReceiverStudentId, @CreatedAt)";
                    using (var command = new Npgsql.NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", review.Id);
                        command.Parameters.AddWithValue("@Comment", review.Comment);
                        command.Parameters.AddWithValue("@Rating", review.Rating);
                        command.Parameters.AddWithValue("@SenderStudentId", review.SenderStudentId);
                        command.Parameters.AddWithValue("@ReceiverStudentId", review.ReceiverStudentId);
                        command.Parameters.AddWithValue("@CreatedAt", review.CreatedAt);
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при добавяне на ревю в базата данни: " + ex.Message;
                return false;
            }
        }
    }
}