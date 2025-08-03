using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Components.Models.FormModels
{
    public class LoginStudentFormModel // this one is for the login page
    {
        [Required(ErrorMessage = "Потребителското име е задължително")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Паролата е задължителна")]
        public string Password { get; set; }
    }
}
