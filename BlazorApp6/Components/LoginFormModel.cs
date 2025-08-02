using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Components
{
    public class LoginFormModel
    {
        [Required(ErrorMessage = "Потребителското име е задължително")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Паролата е задължителна")]
        public string Password { get; set; } = "";
    }
}
