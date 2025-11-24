using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class Extract
    {
        [Key]
        public int ExtractId { get; set; }

        [Required]
        public DateTime GenerationDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        [StringLength(64)]
        public string? DownloadLinkHash { get; set; }

        public bool IsSentViaEmail { get; set; } = false;

        public int CadastralObjectId { get; set; }
        public int RequestedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public CadastralObject CadastralObject { get; set; }
        public Citizen RequestedBy { get; set; }
    }
}