using System;
using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class Attachment
    {
        [Key]
        public int AttachmentId { get; set; }

        public int ApplicationId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        [Required]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        public Application Application { get; set; }
    }
}