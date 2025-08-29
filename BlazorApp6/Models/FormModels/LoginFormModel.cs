using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Models.FormModels
{
    public class LoginStudentFormModel // Временно запазва потребителското име и паролата, выведени от потребителя в Login.razor
    {
        [Required(ErrorMessage = "Потребителското име е задължително")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Паролата е задължителна")]
        public string Password { get; set; }
    }
}
