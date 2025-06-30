using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KeyManagerTool.Dao.Models
{
    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [Column("Email")]
        [MaxLength(500)]
        public string Email { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}