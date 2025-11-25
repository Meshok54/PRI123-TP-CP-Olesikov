using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class Citizen
    {
        [Key]
        [Column("citizen_id")]
        public int CitizenId { get; set; }

        [Required]
        [StringLength(10)]
        [Column("passport_data")]
        public string PassportData { get; set; }

        public User User { get; set; }
    }
}