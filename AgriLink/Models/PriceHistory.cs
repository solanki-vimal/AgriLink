using System.ComponentModel.DataAnnotations.Schema;

namespace AgriLink.Models
{
    public class PriceHistory
    {
        public int PriceHistoryId { get; set; }

        // FK: Produce
        public int ProduceId { get; set; }

        [ForeignKey("ProduceId")]
        public Produce Produce { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal OldPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal NewPrice { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}
