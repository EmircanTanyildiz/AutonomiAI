using System.ComponentModel.DataAnnotations;

namespace AutonomiAI.Models
{
    public class LoginModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required, MinLength(6)]
        public string Password { get; set; } = default!;
    }
}
