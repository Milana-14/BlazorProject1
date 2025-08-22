using BlazorApp6.Components.Models;
using Microsoft.Data.Sqlite;

namespace BlazorApp6.Services
{
    public class SqliteDataAccess // TODO: Сделать эти методы дженериковыми
    {
        public readonly string connectionString;

        public SqliteDataAccess(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
        }

        public List<Student> LoadStudents()
        {
            List<Student> students = new List<Student>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string sql = "SELECT Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade FROM Student";

                using (var cmd = new SqliteCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
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
                        student.Id = Guid.Parse(reader.GetString(0));

                        students.Add(student);
                    }
                }
            }
            return students;
        }
        public void SaveStudent(Student student)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO Student 
                               (Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade) 
                               VALUES (@Id, @FirstName, @SecName, @Age, @Email, @PhoneNumber, @Username, @Password, @Grade)";

                using (var cmd = new SqliteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", student.Id.ToString());
                    cmd.Parameters.AddWithValue("@FirstName", student.FirstName);
                    cmd.Parameters.AddWithValue("@SecName", student.SecName ?? "");
                    cmd.Parameters.AddWithValue("@Age", student.Age);
                    cmd.Parameters.AddWithValue("@Email", student.Email);
                    cmd.Parameters.AddWithValue("@PhoneNumber", student.PhoneNumber);
                    cmd.Parameters.AddWithValue("@Username", student.Username);
                    cmd.Parameters.AddWithValue("@Password", student.Password);
                    cmd.Parameters.AddWithValue("@Grade", student.Grade);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
