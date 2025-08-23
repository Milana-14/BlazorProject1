namespace BlazorApp6.Components.Models;

public class Student : User
{
    public override Guid Id { get; set; }
    public override string FirstName { get; set; }
    public override string SecName { get; set; }
    public override int Age { get; set; }
    public override string Email { get; set; }
    public override string PhoneNumber { get; set; }
    public override string Username { get; }
    public override string Password { get; protected set; }

    public int Grade { get; set; }

    public HashSet<Subject> CanHelpWith { get; set; } = new();
    public HashSet<Subject> NeedsHelpWith { get; set; } = new();

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

    public bool IsMatching(Student otherStudent)
    {
        if (Grade == otherStudent.Grade && (HasValidSubjects() || otherStudent.HasValidSubjects()))
        {
            return CanHelpWith.Any(subject => otherStudent.NeedsHelpWith.Contains(subject)) &&
           NeedsHelpWith.Any(subject => otherStudent.CanHelpWith.Contains(subject));
        }
        return false;
    }
}
