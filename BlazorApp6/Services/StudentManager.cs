using BlazorApp6.Models;
using Npgsql;
using System.Data;

namespace BlazorApp6.Services
{
    public class StudentManager // Зареждане, запис и управление на данните за студентите.
    {
        private readonly string connectionString;
        private readonly SubjectsManager subjectsManager;

        private readonly List<Student> students = new();

        private readonly Dictionary<Guid, Student> studentsById = new();
        private readonly Dictionary<string, Student> studentsByUsername = new(StringComparer.OrdinalIgnoreCase);

        public string? DbError { get; private set; } = string.Empty;

        public StudentManager(IConfiguration config, SubjectsManager subjectsManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            this.subjectsManager = subjectsManager;

            if (!LoadStudentsFromDb(out var studentsFromDb))
            {
                DbError += "Зареждането на данните за учениците не беше успешно.";
                return;
            }

            foreach (var s in studentsFromDb)
                AddToCache(s);
        }


        public bool AddStudent(Student newStudent)
        {
            if (studentsByUsername.ContainsKey(newStudent.Username))
            {
                DbError = "Ученик с този юзърнейм вече съществува.";
                return false;
            }

            if (!SaveStudentToDb(newStudent))
            {
                DbError += "Не се е получило да се запише ученик в базата данни.";
                return false;
            }

            AddToCache(newStudent);
            return true;
        }

        public bool UpdateStudent(Student updatedStudent)
        {
            if (!studentsById.TryGetValue(updatedStudent.Id, out var existing))
            {
                DbError = "Ученикът не е намерен.";
                return false;
            }

            try
            {
                UpdateStudentInDb(updatedStudent);
                RemoveFromCache(existing);
                AddToCache(updatedStudent);

                return true;
            }
            catch (Exception ex)
            {
                DbError = "Обновяването на данните за ученик не беше успешно: " + ex.Message;
                return false;
            }
        }

        public List<Student> GetAllStudents()
        {
            foreach (var student in students)
                PopulateSubjects(student);

            return students;
        }

        public Student? FindStudent(Func<Student, bool> predicate)
        {
            foreach (var student in students)
            {
                if (!predicate(student))
                    continue;

                PopulateSubjects(student);
                return student;
            }

            return null;
        }

        private void PopulateSubjects(Student student)
        {
            var subjects = subjectsManager.GetSubjectsByStudent(student);

            if (student.CanHelpWith == null)
                student.CanHelpWith = new HashSet<SubjectEnum>();
            else
                student.CanHelpWith.Clear();

            if (student.NeedsHelpWith == null)
                student.NeedsHelpWith = new HashSet<SubjectEnum>();
            else
                student.NeedsHelpWith.Clear();

            foreach (var s in subjects)
            {
                if (s.CanHelp)
                    student.CanHelpWith.Add(s.Subject);
                else
                    student.NeedsHelpWith.Add(s.Subject);
            }
        }

        private void AddToCache(Student s)
        {
            students.Add(s);
            studentsById[s.Id] = s;
            studentsByUsername[s.Username] = s;
        }

        private void RemoveFromCache(Student s)
        {
            students.Remove(s);
            studentsById.Remove(s.Id);
            studentsByUsername.Remove(s.Username);
        }

        public bool LoadStudentsFromDb(out List<Student> studentsFromDb)
        {
            studentsFromDb = new List<Student>();

            try
            {
                using var connection = CreateConnection();
                using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Students""", connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var student = new Student(
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetInt32(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7)
                    )
                    {
                        Id = reader.GetGuid(0),
                        HelpGivenCount = reader.GetInt32(8),
                        Coins = reader.GetInt32(9),
                        LastOnline = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                    };

                    studentsFromDb.Add(student);
                }

                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждането: " + ex.Message;
                return false;
            }
        }

        public bool SaveStudentToDb(Student student)
        {
            try
            {
                using var connection = CreateConnection();

                const string sql = @"INSERT INTO ""Students""
                (""Id"", ""FirstName"", ""SecName"", ""Email"", ""Username"", ""Password"",
                 ""Grade"", ""AvatarName"", ""HelpGivenCount"", ""Coins"", ""LastOnline"")
                VALUES (@Id,@FirstName,@SecName,@Email,@Username,@Password,
                        @Grade,@AvatarName,@HelpGivenCount,@Coins,@LastOnline)";

                using var cmd = new NpgsqlCommand(sql, connection);
                FillStudentParameters(cmd, student);

                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Записването на нов ученик не беше успешно. " + ex.Message;
                return false;
            }
        }

        public void UpdateStudentInDb(Student student)
        {
            try
            {
                using var connection = CreateConnection();

                const string sql = @"UPDATE ""Students""
                    SET ""FirstName""=@FirstName,
                        ""SecName""=@SecName,
                        ""Email""=@Email,
                        ""Username""=@Username,
                        ""Password""=@Password,
                        ""Grade""=@Grade,
                        ""AvatarName""=@AvatarName,
                        ""HelpGivenCount""=@HelpGivenCount,
                        ""Coins""=@Coins
                    WHERE ""Id""=@Id";

                using var cmd = new NpgsqlCommand(sql, connection);
                FillStudentParameters(cmd, student);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DbError = $"Обновяването не беше успешно: {ex.Message}";
            }
        }

        public async Task UpdateLastSeenAsync(Guid studentId)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                string sql = @"UPDATE ""Students""
                               SET ""LastOnline""=@LastOnline
                               WHERE ""Id""=@Id";
                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", studentId);
                cmd.Parameters.AddWithValue("@LastOnline", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                DbError = $"Обновяването на последно виждане на ученик не беше успешно: {ex}";
            }
        }

        private NpgsqlConnection CreateConnection()
        {
            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static void FillStudentParameters(NpgsqlCommand cmd, Student s)
        {
            cmd.Parameters.Add("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = s.Id;
            cmd.Parameters.Add("@FirstName", NpgsqlTypes.NpgsqlDbType.Text).Value = s.FirstName ?? "";
            cmd.Parameters.Add("@SecName", NpgsqlTypes.NpgsqlDbType.Text).Value = s.SecName ?? "";
            cmd.Parameters.Add("@Email", NpgsqlTypes.NpgsqlDbType.Text).Value = s.Email ?? "";
            cmd.Parameters.Add("@Username", NpgsqlTypes.NpgsqlDbType.Text).Value = s.Username;
            cmd.Parameters.Add("@Password", NpgsqlTypes.NpgsqlDbType.Text).Value = s.Password;
            cmd.Parameters.Add("@Grade", NpgsqlTypes.NpgsqlDbType.Integer).Value = s.Grade;
            cmd.Parameters.Add("@AvatarName", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)s.AvatarName ?? DBNull.Value;
            cmd.Parameters.Add("@HelpGivenCount", NpgsqlTypes.NpgsqlDbType.Integer).Value = s.HelpGivenCount;
            cmd.Parameters.Add("@Coins", NpgsqlTypes.NpgsqlDbType.Integer).Value = s.Coins;
            cmd.Parameters.Add("@LastOnline", NpgsqlTypes.NpgsqlDbType.Timestamp).Value = (object?)s.LastOnline ?? DBNull.Value;
        }
    }
}