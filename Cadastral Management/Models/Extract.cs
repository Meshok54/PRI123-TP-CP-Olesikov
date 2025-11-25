using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class Extract
    {
        [Key]
        [Column("extract_id")]
        public int ExtractId { get; set; }

        [Required]
        [Column("generation_date")]
        public DateTime GenerationDate { get; set; } = DateTime.Now;

        [Column("cadastral_object_id")]
        public int CadastralObjectId { get; set; }

        [Column("requested_by_id")]
        public int RequestedById { get; set; }

        [Required]
        [StringLength(500)]
        [Column("file_path")]
        public string FilePath { get; set; }

        [StringLength(64)]
        [Column("download_link_hash")]
        public string? DownloadLinkHash { get; set; }

        [Column("is_sent_via_email")]
        public bool IsSentViaEmail { get; set; } = false;

        public CadastralObject CadastralObject { get; set; }
        public Citizen RequestedBy { get; set; }
    }
}