using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Components.Models;

public class Student
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Името е задължително")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Фамилията е задължителна")]
    public string SecName { get; set; }

    [Required(ErrorMessage = "Възрастта е задължителна")]
    [Range(1, 120, ErrorMessage = "Невалидна възраст")]
    public int Age { get; set; }

    [Required(ErrorMessage = "Класът е задължителен")]
    [Range(1, 12, ErrorMessage = "Класът трябва да е от 1 до 12")]
    public int Grade { get; set; }

    [Required(ErrorMessage = "Имейлът е задължителен")]
    public string Email { get; set; }

    [MinLength(13, ErrorMessage = "Телефонният номер е невалиден")]
    public string PhoneNumber { get; set; }

    [Required(ErrorMessage = "Потребителското име е задължително")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Паролата е задължителна")]
    [MinLength(6, ErrorMessage = "Паролата трябва да е поне 6 символа")]
    public string Password { get; set; }

    public HashSet<Subject> CanHelpWith { get; set; } = new();
    public HashSet<Subject> NeedsHelpWith { get; set; } = new();

    public Student() 
    {
        Id = Guid.NewGuid();
    }
    public Student(string firstName, string secName, int age, int grade, string username, string password, string email, string phoneNumber = "")
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        SecName = secName;
        Age = age;
        Grade = grade;
        Username = username;
        Password = password;
        Email = email;
        PhoneNumber = phoneNumber;
    }

    public void ChangePassword(string password)
    {
        if (password.Length > 5)
            this.Password = password;
    }

    public void AddSubjects(Subject canHelpWith = Subject.NotSpecified, Subject needsHelpWith = Subject.NotSpecified)
    {
        if (canHelpWith != Subject.NotSpecified) CanHelpWith.Add(canHelpWith);
        if (needsHelpWith != Subject.NotSpecified) NeedsHelpWith.Add(needsHelpWith);
    }

    public bool HasValidSubjects()
    {
        return CanHelpWith.Any(s => s != Subject.NotSpecified) && NeedsHelpWith.Any(s => s != Subject.NotSpecified);
    }
}
