using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class Citizen
    {
        [Key]
        public int CitizenId { get; set; }

        [Required]
        [StringLength(10)]
        public string PassportData { get; set; }

        public User User { get; set; }
        public List<CadastralObject> CadastralObjects { get; set; } = new();
        public List<Application> Applications { get; set; } = new();
        public List<Extract> Extracts { get; set; } = new();
    }
}
