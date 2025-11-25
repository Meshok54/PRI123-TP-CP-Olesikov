using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class Attachment
    {
        [Key]
        [Column("attachment_id")]
        public int AttachmentId { get; set; }

        [Column("application_id")]
        public int ApplicationId { get; set; }

        [Required]
        [StringLength(255)]
        [Column("file_name")]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        [Column("file_path")]
        public string FilePath { get; set; }

        [Required]
        [Column("upload_date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;


        public Application Application { get; set; }
    }
}