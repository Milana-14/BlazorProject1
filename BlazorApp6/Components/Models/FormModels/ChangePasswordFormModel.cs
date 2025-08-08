using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Components.Models.FormModels
{
    public class ChangePasswordFormModel // this one is for the change password page
    {
        [Required(ErrorMessage = "Текущата парола е задължителна")]
        public string currentPassword { get; set; }

        [Required(ErrorMessage = "Новата парола е задължителна")]
        [MinLength(6, ErrorMessage = "Паролата трябва да е поне 6 символа")]
        public string newPassword { get; set; }

        [Required(ErrorMessage = "Подтвърждението на паролата е задължително")]
        [MinLength(6, ErrorMessage = "Паролата трябва да е поне 6 символа")]
        [Compare("newPassword", ErrorMessage = "Паролите не съвпадат")]
        public string confirmPassword { get; set; }
    }
}
