using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Login { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [StringLength(10)]
        public string UserType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}