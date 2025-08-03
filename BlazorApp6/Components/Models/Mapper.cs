using BlazorApp6.Components.Models.FormModels;
using System.ComponentModel.DataAnnotations;
namespace BlazorApp6.Components.Models;

public static class FormMapper // convert the user from RegisterStudentFormModel type to Student type
{
    public static Student ToStudent(this RegisterStudentFormModel form)
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
