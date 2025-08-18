using BlazorApp6.Components.Models;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        private List<Student> students;
        public string? FileLoadError { get; private set; }
        public StudentManager()
        {
            try
            {
                students = StudentFileManager.LoadFromFile();
                FileLoadError = null;
            }
            catch (ApplicationException ex)
            {
                students = new List<Student>();
                FileLoadError = ex.Message;
            }
        }

        public void AddStudent(Student student)
        {
            students.Add(student);
            StudentFileManager.SaveToFile(students);
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
                StudentFileManager.SaveToFile(students);
                return true;
            }
            else
            {
                return false;
            }
        }
        public List<Student> GetAllStudents()
        {
            return students;
        }


        // класс для чтения и записи данных в файл 
        private static class StudentFileManager
        {
            public static void SaveToFile(List<Student> students)
            {
                try
                {
                    string json = JsonSerializer.Serialize(students);
                    File.WriteAllText(AppConstants.StudentsFilePath, json);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Запазването на данните не бе успешно. Проверете правата на достъпа и наличието на свободното място на диска.", ex);
                }
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
