using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class ApplicationHistory
    {
        [Key]
        public int HistoryId { get; set; }

        // Внешние ключи
        public int ApplicationId { get; set; }
        public int ChangedByEmployeeId { get; set; }

        public string? OldStatus { get; set; }

        [Required]
        public string NewStatus { get; set; }

        [Required]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        public string? HistoryComment { get; set; }

        // Навигационные свойства
        public Application Application { get; set; }
        public Employee ChangedByEmployee { get; set; }
    }
}