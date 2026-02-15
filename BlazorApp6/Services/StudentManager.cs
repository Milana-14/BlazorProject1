using BlazorApp6.Models;
using Npgsql;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private readonly string connectionString;
        private readonly SubjectsManager subjectsManager;

        public string? DbError { get; private set; } = string.Empty;

        public StudentManager(IConfiguration config, SubjectsManager subjectsManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            this.subjectsManager = subjectsManager;
        }

        public async Task<bool> AddStudent(Student newStudent)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using (var checkCmd = new NpgsqlCommand(
                    @"SELECT 1 FROM ""Students"" WHERE ""Username""=@Username LIMIT 1", connection))
                {
                    checkCmd.Parameters.AddWithValue("@Username", newStudent.Username);
                    if (await checkCmd.ExecuteScalarAsync() != null)
                    {
                        DbError = "Ученик с този юзърнейм вече съществува.";
                        return false;
                    }
                }

                string sql = @"INSERT INTO ""Students"" 
                           (""Id"", ""FirstName"", ""SecName"", ""Email"", ""Username"", ""Password"", ""Grade"", ""AvatarName"", ""HelpGivenCount"", ""Coins"", ""LastOnline"")
                           VALUES (@Id, @FirstName, @SecName, @Email, @Username, @Password, @Grade, @AvatarName, @HelpGivenCount, @Coins, @LastOnline)";

                await using var cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", newStudent.Id);
                cmd.Parameters.AddWithValue("@FirstName", newStudent.FirstName ?? "");
                cmd.Parameters.AddWithValue("@SecName", newStudent.SecName ?? "");
                cmd.Parameters.AddWithValue("@Email", newStudent.Email ?? "");
                cmd.Parameters.AddWithValue("@Username", newStudent.Username);
                cmd.Parameters.AddWithValue("@Password", newStudent.Password);
                cmd.Parameters.AddWithValue("@Grade", newStudent.Grade);
                cmd.Parameters.AddWithValue("@AvatarName", (object?)newStudent.AvatarName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HelpGivenCount", newStudent.HelpGivenCount);
                cmd.Parameters.AddWithValue("@Coins", newStudent.Coins);
                cmd.Parameters.AddWithValue("@LastOnline", newStudent.LastOnline ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Не се е получило да се запише ученик: " + ex.Message;
                return false;
            }
        }

        public async Task<bool> UpdateStudent(Student student)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                string sql = @"UPDATE ""Students""
                               SET ""FirstName""=@FirstName, ""SecName""=@SecName, ""Email""=@Email,
                                   ""Username""=@Username, ""Password""=@Password, ""Grade""=@Grade,
                                   ""AvatarName""=@AvatarName, ""HelpGivenCount""=@HelpGivenCount, ""Coins""=@Coins, ""LastOnline""
                               WHERE ""Id""=@Id";

                await using var cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", student.Id);
                cmd.Parameters.AddWithValue("@FirstName", student.FirstName ?? "");
                cmd.Parameters.AddWithValue("@SecName", student.SecName ?? "");
                cmd.Parameters.AddWithValue("@Email", student.Email ?? "");
                cmd.Parameters.AddWithValue("@Username", student.Username);
                cmd.Parameters.AddWithValue("@Password", student.Password);
                cmd.Parameters.AddWithValue("@Grade", student.Grade);
                cmd.Parameters.AddWithValue("@AvatarName", (object?)student.AvatarName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HelpGivenCount", student.HelpGivenCount);
                cmd.Parameters.AddWithValue("@Coins", student.Coins);
                cmd.Parameters.AddWithValue("@LastOnline", student.LastOnline ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Обновяването не беше успешно: " + ex.Message;
                return false;
            }
        }

        public async Task<List<Student>> GetAllStudents()
        {
            var students = new List<Student>();

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Students""", connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                students.Add(MapStudent(reader));
            }

            await reader.CloseAsync();

            foreach (var student in students)
            {
                var subjects = subjectsManager.GetSubjectsByStudent(student);

                student.CanHelpWith = subjects.Where(s => s.CanHelp).Select(s => s.Subject).ToHashSet();
                student.NeedsHelpWith = subjects.Where(s => !s.CanHelp).Select(s => s.Subject).ToHashSet();
            }

            return students;
        }

        public async Task<Student?> FindStudent(Func<Student, bool> predicate)
        {
            var all = await GetAllStudents();
            return all.FirstOrDefault(predicate);
        }

        public async Task<Student?> FindStudentById(Guid id)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = """
        SELECT "Id", "FirstName", "SecName", "Email", "Username", "Password", "Grade", "AvatarName", "HelpGivenCount", "Coins", "LastOnline"
        FROM "Students"
        WHERE "Id" = @Id
    """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return MapStudent(reader);
        }

        public async Task<Student?> FindStudentByUsername(string username)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = """
        SELECT "Id", "FirstName", "SecName", "Email", "Username", "Password", "Grade", "AvatarName", "HelpGivenCount", "Coins", "LastOnline"
        FROM "Students"
        WHERE "Username" = @Username
    """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", username);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return MapStudent(reader);
        }

        public async Task UpdateLastSeenAsync(Guid studentId)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var cmd = new NpgsqlCommand(
                    @"UPDATE ""Students"" SET ""LastOnline""=@LastOnline WHERE ""Id""=@Id", connection);

                cmd.Parameters.AddWithValue("@Id", studentId);
                cmd.Parameters.AddWithValue("@LastOnline", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                DbError = $"Обновяването на последно виждане не беше успешно: {ex}";
            }
        }

        public  static Student MapStudent(NpgsqlDataReader reader)
        {
            var student = new Student(
    firstName: reader.IsDBNull(1) ? "" : reader.GetString(1),
    secName: reader.IsDBNull(2) ? "" : reader.GetString(2),
    email: reader.IsDBNull(3) ? "" : reader.GetString(3),
    username: reader.IsDBNull(4) ? "" : reader.GetString(4),
    password: reader.IsDBNull(5) ? "" : reader.GetString(5),
    grade: reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
    avatarName: reader.IsDBNull(7) ? null : reader.GetString(7)
);

            student.Id = reader.GetGuid(0);
            student.HelpGivenCount = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
            student.Coins = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
            student.LastOnline = reader.IsDBNull(10) ? null : reader.GetDateTime(10);

            return student;
        }
    }
}