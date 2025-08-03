using BlazorApp6.Components.Models;

namespace BlazorApp6.Services
{
    public class StudentManager
    {
        public static List<Student> AddStudent(Student student, string filePath)
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
                throw new ApplicationException("Не удалось добавить ученика. Попробуйте позже.", ex);
            }
        }
        public static Student? FindStudent(string name, string filePath)
        {
            try
            {
                List<Student> students = StudentFileManager.LoadFromFile(filePath);
                return students.FirstOrDefault(s => s.Name == name);
            }
            catch(ApplicationException ex)
            {
                throw new ApplicationException("Не удалось загрузить данные. Попробуйте позже.", ex);
            }
        }
    }
}
