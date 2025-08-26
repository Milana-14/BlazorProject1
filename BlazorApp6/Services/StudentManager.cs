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
        public string? DbError { get; private set; }
        public StudentManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            try
            {
                students = LoadStudents();
                DbError = null;
            }
            catch (ApplicationException ex)
            {
                students = new List<Student>();
                DbError = ex.Message;
            }
        }

        public void AddStudent(Student student)
        {
            students.Add(student);
            SaveStudent(student);
        }
        public Student? FindStudent(Func<Student, bool> predicate)
        {
            return students.FirstOrDefault(predicate);
        }
        public bool UpdateStudent(Student updatedStudent)
        {
            int index = students.FindIndex(s => s.Username == updatedStudent.Username);
            if (index >= 0)
            {
                students[index] = updatedStudent;
                UpdateStudentInDb(updatedStudent);
                return true;
            }
            return false;
        }
        public List<Student> GetAllStudents()
        {
            return students;
        }



        // Работа с база данни
        public List<Student> LoadStudents()
        {
            List<Student> students = new List<Student>();

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"SELECT Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade FROM ""Students""";

            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Student student = new Student(
                    firstName: reader.IsDBNull(1) ? "" : reader.GetString(1),
                    secName: reader.IsDBNull(2) ? "" : reader.GetString(2),
                    age: reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    email: reader.IsDBNull(4) ? "" : reader.GetString(4),
                    phoneNumber: reader.IsDBNull(5) ? "" : reader.GetString(5),
                    username: reader.GetString(6),
                    password: reader.GetString(7),
                    grade: reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                );
                student.Id = reader.GetGuid(0);

                students.Add(student);
            }
            return students;
        }
        public void SaveStudent(Student student)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"INSERT INTO ""Students"" 
                           (Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade)
                           VALUES (@Id, @FirstName, @SecName, @Age, @Email, @PhoneNumber, @Username, @Password, @Grade)";

            using var cmd = new NpgsqlCommand(sql, connection);
            student.Id = (student.Id == Guid.Empty) ? Guid.NewGuid() : student.Id;
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
        public void UpdateStudentInDb(Student student)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string sql = @"UPDATE ""Students""
                           SET FirstName=@FirstName, SecName=@SecName, Age=@Age, Email=@Email,
                               PhoneNumber=@PhoneNumber, Username=@Username, Password=@Password, Grade=@Grade
                           WHERE Id=@Id";

            using var cmd = new NpgsqlCommand(sql, connection);

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
    }
}