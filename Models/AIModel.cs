using AutonomiAI.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace AutonomiAI.Models 
{
    public class AIModel
    {
        [Key]
        public int ModelId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;

        [Required, StringLength(100)]
        public string ModelAdi { get; set; } = default!;

        [Required, StringLength(80)]
        public string AlgoritmaTuru { get; set; } = default!;

        [Required]
        public int DatasetID { get; set; }

        public float? Dogruluk { get; set; }
        public float? Kayip { get; set; }

        [Column(TypeName = "text")]
        public string? Rocegrisi { get; set; }

        public float? Auc { get; set; }
        public float? F1Skoru { get; set; }
        public float? EgitimSuresi { get; set; }

        [Required]
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        // Navigation Properties
        public List<YapilanTest> YapilanTestler { get; set; } = new();
    }
}