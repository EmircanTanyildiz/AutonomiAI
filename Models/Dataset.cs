// Models/Dataset.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomiAI.Models
{
    public class Dataset
    {
        [Key]
        public int VerisetiId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;

        [Required, StringLength(50)]
        public string VerisetiAdi { get; set; } = default!;

        [StringLength(50)]
        public string? VeriSetiYolu { get; set; }

        [StringLength(50)]
        public string? VeriTipi { get; set; }

        public DateTime YuklemeTarihi { get; set; } = DateTime.Now;
    }
}
