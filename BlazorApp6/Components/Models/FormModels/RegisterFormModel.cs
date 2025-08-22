using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Components.Models.FormModels
{
    public class RegisterStudentFormModel // this one is for the register page
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
        [MinLength(6, ErrorMessage = "Паролата трябва да е поне 6 символа")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Имейлът е задължителен")]
        public string Email { get; set; }

        [MinLength(13, ErrorMessage = "Телефонният номер е невалиден")]
        public string PhoneNumber { get; set; }
    }
}
