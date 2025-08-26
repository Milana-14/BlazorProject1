using BlazorApp6.Components.Models;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private readonly string connectionString;
        private List<Student> students;
        public string? DbError { get; private set; } = "";
        public StudentManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");

            students = new List<Student>();

            if (!LoadStudentsFromDb(out List<Student> studentsFromDb))
            {
                DbError = "Зареждането на данните за учениците не беше успешно.";
                return;
            }
        }

        public void AddStudent(Student newStudent)
        {
            if (students.Any(s => s.Username == newStudent.Username)) DbError = "Ученик с този юзърнейм вече съществува.";

            students.Add(newStudent);
             if (!SaveStudentToDb(newStudent)) DbError = "Не се е получило да се запише ученик в базата данни.";
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
                DbError = ex.Message;
                return false;
            }
        }
        public List<Student> GetAllStudents()
        {
            return students;
        }
        public Student? FindStudent(Func<Student, bool> predicate)
        {
            return students.FirstOrDefault(predicate);
        }




        // Работа с база данни
        public bool LoadStudentsFromDb(out List<Student> studentsFromDb)
        {
            studentsFromDb = new List<Student>();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                string sql = @"SELECT Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade FROM ""Students""";

                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
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
                return false;
            }
        } // bool за да се види дали имаше грешка
        public bool SaveStudentToDb(Student student)
        {
            try
            {
                using NpgsqlConnection connection = new NpgsqlConnection(connectionString);
                connection.Open();

                string sql = @"INSERT INTO ""Students"" 
                           (Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade)
                           VALUES (@Id, @FirstName, @SecName, @Age, @Email, @PhoneNumber, @Username, @Password, @Grade)";

                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
                SetStudentParameters(cmd, student);

                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }  // Същото и тук с bool
        public void UpdateStudentInDb(Student student)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                string sql = @"UPDATE ""Students""
                           SET FirstName=@FirstName, SecName=@SecName, Age=@Age, Email=@Email,
                               PhoneNumber=@PhoneNumber, Username=@Username, Password=@Password, Grade=@Grade
                           WHERE Id=@Id";

                using var cmd = new NpgsqlCommand(sql, connection);

                SetStudentParameters(cmd, student);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Грешка при обновяване на ученик в базата данни. " + ex.Message);
            }
        }

        private void SetStudentParameters(NpgsqlCommand cmd, Student student)
        {
            cmd.Parameters.AddWithValue("@Id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = student.Id;
            cmd.Parameters.AddWithValue("@FirstName", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.FirstName ?? "";
            cmd.Parameters.AddWithValue("@SecName", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.SecName ?? "";
            cmd.Parameters.Add("@Age", NpgsqlTypes.NpgsqlDbType.Smallint).Value = student.Age;
            cmd.Parameters.AddWithValue("@Email", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.Email ?? "";
            cmd.Parameters.AddWithValue("@PhoneNumber", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.PhoneNumber ?? "";
            cmd.Parameters.AddWithValue("@Username", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.Username;
            cmd.Parameters.AddWithValue("@Password", NpgsqlTypes.NpgsqlDbType.Varchar).Value = student.Password;
            cmd.Parameters.AddWithValue("@Grade", NpgsqlTypes.NpgsqlDbType.Smallint).Value = student.Grade;
        } // За да не пиша едно и също нещо 500 пъти
    }
}