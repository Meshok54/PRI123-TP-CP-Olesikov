using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class CadastralObject
    {
        [Key]
        [Column("cadastral_object_id")]
        public int CadastralObjectId { get; set; }

        [Required]
        [StringLength(14)]
        [Column("cadastral_number")]
        public string CadastralNumber { get; set; }

        [Required]
        [StringLength(500)]
        [Column("address")]
        public string Address { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        [Column("area")]
        public decimal Area { get; set; }

        [Required]
        [StringLength(20)]
        [Column("cadastralObject_type")]
        public string CadastralObjectType { get; set; }

        [Column("owner_id")]
        public int OwnerId { get; set; }

        [Required]
        [Column("registration_date")]
        public DateTime RegistrationDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;


        public Citizen Owner { get; set; }
        public List<Application> Applications { get; set; } = new();
        public List<Extract> Extracts { get; set; } = new();
        public List<CadastralObjectHistory> CadastralObjectHistories { get; set; } = new();
    }
}