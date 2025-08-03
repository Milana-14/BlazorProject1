using BlazorApp6.Components.Models;
using System.Text.Json;

namespace BlazorApp6.Services
{
    class StudentFileManager
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
                throw new ApplicationException("Зареждането на данните не бе успешно. Опитайте отново по-късно.");
            }
        }
    }
}