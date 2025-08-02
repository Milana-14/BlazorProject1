using BlazorApp6.Models;
using System.ComponentModel.DataAnnotations;
namespace BlazorApp6.Mappers;
public class StudentFormModel
{
    [Required(ErrorMessage = "Името е задължително")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Фамилията е задължителна")]
    public string SecName { get; set; }

    [Required(ErrorMessage = "Възрастта е задължителна")]
    [Range(1, 120, ErrorMessage = "Невалидна възраст")]
    public int Age { get; set; }

    [Required(ErrorMessage = "Класът е задължителен")]
    [Range(1, 12, ErrorMessage = "Класът трябва да е от 1 до 12")]
    public int Grade { get; set; }

    [Required(ErrorMessage = "Потребителското име е задължително")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Паролата е задължителна")]
    [MinLength(6, ErrorMessage = "Паролата не може да е по-кратка от 6 символа")]
    public string Password { get; set; }

    [MinLength(13, ErrorMessage = "Телефонният номер е невалиден")]
    public string PhoneNumber { get; set; }
}
public static class FormMapper
{
    public static Student ToStudent(this StudentFormModel form)
    {
        return new Student(
            form.Name,
            form.Age,
            form.Grade,
            form.Username,
            form.Password,
            form.PhoneNumber 
        );
    }
}
