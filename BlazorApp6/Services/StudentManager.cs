using BlazorApp6.Components.Models;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private List<Student> students;
        public StudentManager()
        {
            students = StudentFileManager.LoadFromFile();
        }

        public List<Student> AddStudent(Student student)
        {
            try
            {
                // List<Student> students = StudentFileManager.LoadFromFile();
                students.Add(student);
                StudentFileManager.SaveToFile(students);
                return students;
            }
            catch(ApplicationException ex)
            {
                string errorMessage = ex.Message;
                throw new ApplicationException("Добавянето на ученик не бе успешно. Опитайте отново по-късно.", ex);
            }
        }
        public Student? FindStudent(Func<Student, bool> predicate)
        {
            try
            {
                // List<Student> students = StudentFileManager.LoadFromFile();
                return students.FirstOrDefault(predicate);
                // dbContext.Students.FirstOrDefault(predicate) - при переходе на бд
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException("Зареждането на данните не бе успешно. Опитайте отново по-късно.", ex);
            }
        }
        public void UpdateStudent(Student updatedStudent)
        {
            // var students = StudentFileManager.LoadFromFile();
            int index = students.FindIndex(s => s.Username == updatedStudent.Username);

            if (index >= 0)
            {
                students[index] = updatedStudent;
                StudentFileManager.SaveToFile(students);
            }
            else
            {
                throw new Exception("Ученик не найден");
            }
        }
        public List<Student> GetAllStudents()
        {
            // return StudentFileManager.LoadFromFile();
            return students;
        }


        // класс для чтения и записи данных в файл 
        private static class StudentFileManager
        {
            public static void SaveToFile(List<Student> students)
            {
                string line = JsonSerializer.Serialize(students);
                File.WriteAllText(AppConstants.StudentsFilePath, line);
            }
            public static List<Student> LoadFromFile()
            {
                if (!File.Exists(AppConstants.StudentsFilePath))
                {
                    return new List<Student>();
                }

                try
                {
                    string lines = File.ReadAllText(AppConstants.StudentsFilePath);
                    List<Student> students = JsonSerializer.Deserialize<List<Student>>(lines) ?? new List<Student>();
                    return students;
                }
                catch (Exception ex)
                {
                    // Можно логировать ошибку (если есть логгер), а пользователю показать:
                    throw new ApplicationException("Зареждането на данните не беше успешно. Опитайте отново по-късно.");
                }
            }
        }
    }
}
