namespace BlazorApp6.Components.Models;

public class Student : User
{
    public override string Name { get; set; }
    public override string SecName { get; set; }
    public override int Age { get; set; }
    public override string PhoneNumber { get; set; }
    public override string Username { get; }
    public override string Password { get; protected set; }

    public int Grade { get; set; }

    public List<Subject> CanHelpWith { get; set; } = new();
    public List<Subject> NeedsHelpWith { get; set; } = new();

    public Student(string name, string secName, int age, int grade, string username, string password, string phoneNumber = "Не указан")
    {
        Name = name;
        SecName = secName;
        Age = age;
        Grade = grade;
        Username = username;
        Password = password;
        PhoneNumber = !string.IsNullOrWhiteSpace(phoneNumber) ? phoneNumber : "Не посочен";
    }

    public void ChangePassword(string password)
    {
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

    public bool IsMatching(Student otherStudent)
    {
        if (Grade == otherStudent.Grade && (HasValidSubjects() || otherStudent.HasValidSubjects()))
        {
            return CanHelpWith.Any(subject => otherStudent.NeedsHelpWith.Contains(subject)) &&
           NeedsHelpWith.Any(subject => otherStudent.CanHelpWith.Contains(subject));
        }
        return false;
    }


    public override void PrintInfo()
    {
        Console.WriteLine($"Имя: {Name}");
        Console.WriteLine($"Возраст: {Age}");
        Console.WriteLine($"Класс: {Grade}");
        Console.WriteLine($"Номер телефона: {PhoneNumber}");
        Console.Write("Может помочь по: ");
        Console.WriteLine(string.Join(", ", CanHelpWith));
        Console.Write("Нуждается в помощи по: ");
        Console.WriteLine(string.Join(", ", NeedsHelpWith));
    }
}
