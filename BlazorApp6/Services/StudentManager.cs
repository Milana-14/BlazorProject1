using BlazorApp6.Components.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private readonly SqliteStudentDataAccess db;
        private List<Student> students;
        public string? FileLoadError { get; private set; }
        public StudentManager(IConfiguration config)
        {
            try
            {
                db = new SqliteStudentDataAccess(config);
                students = db.LoadStudents();
                FileLoadError = null;
            }
            catch (ApplicationException ex)
            {
                students = new List<Student>();
                FileLoadError = ex.Message;
            }

            //try
            //{
            //    students = StudentFileManager.LoadFromFile();
            //    FileLoadError = null;
            //}
            //catch (ApplicationException ex)
            //{
            //    students = new List<Student>();
            //    FileLoadError = ex.Message;
            //}
        }

        public void AddStudent(Student student)
        {
            students.Add(student);
            db.SaveStudent(student);

            //students.Add(student);
            //StudentFileManager.SaveToFile(students);
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
                db.UpdateStudent(updatedStudent);
                // StudentFileManager.SaveToFile(students);
                return true;
            }
            return false;
        }
        public List<Student> GetAllStudents()
        {
            return students;
        }


        // класс для чтения и записи данных в базу данных
        private class SqliteStudentDataAccess
        {
            public readonly string connectionString;
            public SqliteStudentDataAccess(IConfiguration config) => connectionString = config.GetConnectionString("DefaultConnection");

            public List<Student> LoadStudents()
            {
                List<Student> students = new List<Student>();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                string sql = "SELECT Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade FROM Student";

                using var cmd = new SqliteCommand(sql, connection);
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
                    student.Id = Guid.Parse(reader.GetString(0));

                    students.Add(student);
                }
                return students;
            }
            public void SaveStudent(Student student)
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                string sql = @"INSERT INTO Student 
                               (Id, FirstName, SecName, Age, Email, PhoneNumber, Username, Password, Grade) 
                               VALUES (@Id, @FirstName, @SecName, @Age, @Email, @PhoneNumber, @Username, @Password, @Grade)";

                using var cmd = new SqliteCommand(sql, connection);
                student.Id = (student.Id == Guid.Empty) ? Guid.NewGuid() : student.Id;
                cmd.Parameters.AddWithValue("@Id", student.Id.ToString());
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
            public void UpdateStudent(Student student)
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                string sql = @"UPDATE STUDENT 
                                    SET FirstName=@FirstName, SecName=@SecName, Age=@Age, Email=@Email, 
                                        PhoneNumber=@PhoneNumber, Username=@Username, Password=@Password, Grade=@Grade
                                    WHERE Id=@Id";

                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", student.Id.ToString());
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











        //private static class StudentFileManager
        //{
        //    public static void SaveToFile(List<Student> students)
        //    {
        //        try
        //        {
        //            string json = JsonSerializer.Serialize(students);
        //            File.WriteAllText(AppConstants.StudentsFilePath, json);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new ApplicationException("Запазването на данните не бе успешно. Проверете правата на достъпа и наличието на свободното място на диска.", ex);
        //        }
        //    }
        //    public static List<Student> LoadFromFile()
        //    {
        //        if (!File.Exists(AppConstants.StudentsFilePath))
        //        {
        //            return new List<Student>();
        //        }

        //        try
        //        {
        //            string lines = File.ReadAllText(AppConstants.StudentsFilePath);
        //            List<Student> students = JsonSerializer.Deserialize<List<Student>>(lines) ?? new List<Student>();
        //            return students;
        //        }
        //        catch (Exception ex)
        //        {
        //            // Можно логировать ошибку (если есть логгер), а пользователю показать:
        //            throw new ApplicationException("Зареждането на данните не беше успешно. Опитайте отново по-късно.");
        //        }
        //    }
        //}
    }
}
