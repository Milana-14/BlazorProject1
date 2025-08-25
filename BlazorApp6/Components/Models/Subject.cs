using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorApp6.Components.Models
{
    public enum Subject
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
    public static string GetDisplayName(this Enum value)
    {
        return value.GetType().GetMember(value.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
    }
}

public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class StudentSubject
{
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public bool CanHelp { get; set; }
}