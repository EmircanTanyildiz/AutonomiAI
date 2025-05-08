using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutonomiAI.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } = default!;

        [Required, StringLength(50)]
        public string Surname { get; set; } = default!;

        [Required, EmailAddress, StringLength(50)]
        public string Email { get; set; } = default!;

        [Required, MinLength(6)]
        public string Password { get; set; } = default!;

        [Required]
        public bool PrivacyAndTerms { get; set; }

        public string Role { get; set; } = "User";

        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        // Navigation Properties
        public List<Dataset> Datasets { get; set; } = new();
        public List<AIModel> AIModels { get; set; } = new();
        public List<YapilanTest> YapilanTestler { get; set; } = new();
    }
}
