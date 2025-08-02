using System.ComponentModel.DataAnnotations;
namespace BlazorApp6.Models;

public enum Subject
{
    НеУказан,
    Математика,
    Биология,
    Химия,
    Физика,
    История,
    География,
    Английский,
    Информатика
}
public abstract class User
{
    public abstract string Name { get; }
    public abstract string SecName { get; }
    public abstract int Age { get; }
    public abstract string PhoneNumber { get; }
    public abstract string Username { get; }
    public abstract string Password { get; }

    public abstract void PrintInfo();
}

public class Student : User
{
    public override string Name { get; }
    public override string SecName { get; }
    public override int Age { get; }
    public override string PhoneNumber { get; }
    public override string Username { get; }
    public override string Password { get; }

    public int Grade { get; private set; }

    public List<Subject> CanHelpWith { get; set; } = new();
    public List<Subject> NeedsHelpWith { get; set; } = new();

    public Student(string name, int age, int grade, string username, string password, string phoneNumber = "Не указан")
    {
        Name = name;
        Age = age;
        Grade = grade;
        Username = username;
        Password = password;
        PhoneNumber = !string.IsNullOrWhiteSpace(phoneNumber) ? phoneNumber : "Не указан";
    }

    public void AddSubjects(Subject canHelpWith = Subject.НеУказан, Subject needsHelpWith = Subject.НеУказан)
    {
        if (canHelpWith != Subject.НеУказан) this.CanHelpWith.Add(canHelpWith);
        if (needsHelpWith != Subject.НеУказан) this.NeedsHelpWith.Add(needsHelpWith);
    }

    public bool HasValidSubjects()
    {
        return CanHelpWith.Any(s => s != Subject.НеУказан) && NeedsHelpWith.Any(s => s != Subject.НеУказан);
    }

    public bool IsMatching(Student otherStudent)
    {
        if (Grade == otherStudent.Grade && (HasValidSubjects() || otherStudent.HasValidSubjects()))
        {
            return this.CanHelpWith.Any(subject => otherStudent.NeedsHelpWith.Contains(subject)) &&
           this.NeedsHelpWith.Any(subject => otherStudent.CanHelpWith.Contains(subject));
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