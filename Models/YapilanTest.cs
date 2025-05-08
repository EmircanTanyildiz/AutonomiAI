using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomiAI.Models
{
    public class YapilanTest
    {
        [Key]
        public int TestId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;

        [Required]
        public int ModelId { get; set; }

        [ForeignKey(nameof(ModelId))]
        public AIModel AIModel { get; set; } = default!;

        [Required, StringLength(255)]
        public string TestEdilenDataPath { get; set; } = default!;

        [StringLength(255)]
        public string? Sonuc { get; set; }

        public DateTime Zaman { get; set; } = DateTime.Now;
    }
}
