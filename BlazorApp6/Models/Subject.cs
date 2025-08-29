using BlazorApp6.Models;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorApp6.Models
{
    public enum SubjectEnum
    {
        [Display(Name = "Не посочено")]
        NotSpecified,

        [Display(Name = "Математика")]
        Math,

        [Display(Name = "Биология")]
        Biology,

        [Display(Name = "Химия")]
        Chemistry,

        [Display(Name = "Физика")]
        Physics,

        [Display(Name = "История")]
        Hystory,

        [Display(Name = "География")]
        Geograpty,

        [Display(Name = "Английский")]
        English,

        [Display(Name = "Литература")]
        Literature,

        [Display(Name = "Български език")]
        BulgarianLanguage,

        [Display(Name = "Информатика")]
        Informatics
    }
}
public static class EnumExtensions
{
    public static string GetDisplayName(this SubjectEnum value)
    {
        return value.GetType().GetMember(value.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
    }
}

public class StudentSubject
{
    public Guid StudentId { get; set; }
    public SubjectEnum Subject { get; set; }
    public bool CanHelp { get; set; }

    public StudentSubject(Guid StudentId, SubjectEnum Subject, bool CanHelp)
    {
        this.StudentId = StudentId;
        this.Subject = Subject;
        this.CanHelp = CanHelp;
    }
}
