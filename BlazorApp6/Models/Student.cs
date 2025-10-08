using System.ComponentModel.DataAnnotations;


namespace BlazorApp6.Models;

public class Student
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Името е задължително")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Фамилията е задължителна")]
    public string SecName { get; set; }

    [Required(ErrorMessage = "Класът е задължителен")]
    [Range(1, 12, ErrorMessage = "Класът трябва да е от 1 до 12")]
    public int Grade { get; set; }

    [Required(ErrorMessage = "Имейлът е задължителен")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Потребителското име е задължително")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Паролата е задължителна")]
    [MinLength(6, ErrorMessage = "Паролата трябва да е поне 6 символа")]
    public string Password { get; set; }

    public string? AvatarName { get; set; }

    public HashSet<SubjectEnum> CanHelpWith { get; set; } = new();
    public HashSet<SubjectEnum> NeedsHelpWith { get; set; } = new();

    public int HelpGivenCount { get; set; } = 0;
    public List<int> HelpRatings { get; set; } = new();

    public Student() 
    {
        Id = Guid.NewGuid();
    }
    public Student(string firstName, string secName, string email, string username, string password, int grade, string? avatarName = "default.png")
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        SecName = secName;
        Email = email;
        Username = username;
        Password = password;
        Grade = grade;
        AvatarName = avatarName;
    }

    public void ChangePassword(string password)
    {
        if (password.Length > 5)
            Password = password;
    }
}
