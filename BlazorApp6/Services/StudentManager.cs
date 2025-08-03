using BlazorApp6.Components.Models;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        public static List<Student> AddStudent(Student student) // Добавить проверку на повторения юзернейма
        {
            try
            {
                List<Student> students = StudentFileManager.LoadFromFile();
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
        public static Student? FindStudent(string Username)
        {
            try
            {
                List<Student> students = StudentFileManager.LoadFromFile();
                return students.FirstOrDefault(s => s.Username == Username);
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException("Зареждането на данните не бе успешно. Опитайте отново по-късно.", ex);
            }
        }
        public static void UpdateStudent(Student updatedStudent)
        {
            var students = StudentFileManager.LoadFromFile();
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
    }
}
