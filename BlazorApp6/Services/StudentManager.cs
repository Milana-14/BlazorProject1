using BlazorApp6.Components.Models;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        public static List<Student> AddStudent(Student student, string filePath) // Добавить проверку на повторения юзернейма
        {
            try
            {
                List<Student> students = StudentFileManager.LoadFromFile(filePath);
                students.Add(student);
                StudentFileManager.SaveToFile(students, filePath);
                return students;
            }
            catch(ApplicationException ex)
            {
                string errorMessage = ex.Message;
                throw new ApplicationException("Добавянето на ученик не бе успешно. Опитайте отново по-късно.", ex);
            }
        }
        public static Student? FindStudent(string Username, string filePath)
        {
            try
            {
                List<Student> students = StudentFileManager.LoadFromFile(filePath);
                return students.FirstOrDefault(s => s.Username == Username);
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException("Зареждането на данните не бе успешно. Опитайте отново по-късно.", ex);
            }
        }
    }
}
