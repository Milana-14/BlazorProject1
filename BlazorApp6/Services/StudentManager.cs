using BlazorApp6.Models;
using Npgsql;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private readonly string connectionString;
        private List<Student> students = new List<Student>();

        private readonly SubjectsManager subjectsManager;
        public string? DbError { get; private set; } = string.Empty;
        public StudentManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            this.subjectsManager = subjectsManager;

            if (!LoadStudentsFromDb(out List<Student> studentsFromDb))
            {
                DbError += "Зареждането на данните за учениците не беше успешно.";
                return;
            }

            students = studentsFromDb;
        }

        public bool AddStudent(Student newStudent)
        {
            if (students.Any(s => s.Username == newStudent.Username))
            {
                DbError = "Ученик с този юзърнейм вече съществува.";
                return false;
            }

             students.Add(newStudent);
            if (!SaveStudentToDb(newStudent))
            {
                DbError += "Не се е получило да се запише ученик в базата данни.";
                return false;
            }
            return true;
        }
        public bool UpdateStudent(Student updatedStudent)
        {
            Student student = students.FirstOrDefault(s => s.Id == updatedStudent.Id);
            if (student == null)
            {
                DbError = "Ученикът не е намерен.";
                return false;
            }

            try
            {
                int index = students.IndexOf(student);
                students[index] = updatedStudent;
                UpdateStudentInDb(updatedStudent);
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Обновяването на данните за ученик не беше успешно.";
                return false;
            }
        }

        public List<Student> GetAllStudents()
        {
            foreach (var student in students)
            {
                var subjects = subjectsManager.GetSubjectsByStudent(student);
                student.CanHelpWith = subjects.Where(s => s.CanHelp).Select(s => s.Subject).ToHashSet();
                student.NeedsHelpWith = subjects.Where(s => !s.CanHelp).Select(s => s.Subject).ToHashSet();
            }
            return students;
        }
        public Student? FindStudent(Func<Student, bool> predicate)
        {
            Student? student = students.FirstOrDefault(predicate);

            if (student == null) return null;

            var subjects = subjectsManager.GetSubjectsByStudent(student);
            student.CanHelpWith = subjects.Where(s => s.CanHelp).Select(s => s.Subject).ToHashSet();
            student.NeedsHelpWith = subjects.Where(s => !s.CanHelp).Select(s => s.Subject).ToHashSet();

            return student;
        }

        public string GetAvatarUrl(Student student)
        {
            // Вернуть URL аватарки, пока заглушка
            return "_content/YourAppNamespace/images/default-avatar.png";
        }





        // Работа с база данни
        public bool LoadStudentsFromDb(out List<Student> studentsFromDb)
        {
            studentsFromDb = new List<Student>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using NpgsqlCommand cmd = new NpgsqlCommand(@"SELECT ""Id"", ""FirstName"", ""SecName"", ""Age"", ""Email"", ""PhoneNumber"", ""Username"", ""Password"", ""Grade"" FROM ""Students""", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Student student = new Student(
                        firstName: reader.IsDBNull(1) ? "" : reader.GetString(1),
                        secName: reader.IsDBNull(2) ? "" : reader.GetString(2),
                        age: reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        email: reader.GetString(4),
                        phoneNumber: reader.IsDBNull(5) ? "" : reader.GetString(5),
                        username: reader.GetString(6),
                        password: reader.GetString(7),
                        grade: reader.GetInt32(8)
                    );
                    student.Id = reader.GetGuid(0);

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
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"INSERT INTO ""Students"" 
                           (""Id"", ""FirstName"", ""SecName"", ""Age"", ""Email"", ""PhoneNumber"", ""Username"", ""Password"", ""Grade"")
                           VALUES (@Id, @FirstName, @SecName, @Age, @Email, @PhoneNumber, @Username, @Password, @Grade)";

                    using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Id", student.Id);
                    cmd.Parameters.AddWithValue("@FirstName", student.FirstName ?? "");
                    cmd.Parameters.AddWithValue("@SecName", student.SecName ?? "");
                    cmd.Parameters.AddWithValue("@Age", student.Age);
                    cmd.Parameters.AddWithValue("@Email", student.Email ?? "");
                    cmd.Parameters.AddWithValue("@PhoneNumber", student.PhoneNumber ?? "");
                    cmd.Parameters.AddWithValue("@Username", student.Username);
                    cmd.Parameters.AddWithValue("@Password", student.Password);
                    cmd.Parameters.AddWithValue("@Grade", student.Grade);

                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Записването на нов ученик в базата не беше успешно." + ex;
                return false;
            }
        } 
        public void UpdateStudentInDb(Student student)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                string sql = @"UPDATE ""Students""
                               SET ""FirstName""=@FirstName, ""SecName""=@SecName, ""Age""=@Age, ""Email""=@Email,
                                   ""PhoneNumber""=@PhoneNumber, ""Username""=@Username, ""Password""=@Password, ""Grade""=@Grade
                               WHERE ""Id""=@Id";


                using var cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", student.Id);
                cmd.Parameters.AddWithValue("@FirstName", student.FirstName ?? "");
                cmd.Parameters.AddWithValue("@SecName", student.SecName ?? "");
                cmd.Parameters.Add("@Age", NpgsqlTypes.NpgsqlDbType.Integer).Value = student.Age;
                cmd.Parameters.AddWithValue("@Email", student.Email ?? "");
                cmd.Parameters.AddWithValue("@PhoneNumber", student.PhoneNumber ?? "");
                cmd.Parameters.AddWithValue("@Username", student.Username);
                cmd.Parameters.AddWithValue("@Password", student.Password);
                cmd.Parameters.AddWithValue("@Grade", student.Grade);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DbError = $"Обновяването на данните за ученик в базата не беше успешно: {ex}";
            }
        }
    }
}