using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        public string Department { get; set; }

        // Навигационные свойства
        public User User { get; set; }
        public List<Application> Applications { get; set; } = new();
        public List<ApplicationHistory> ApplicationHistories { get; set; } = new();
        public List<CadastralObjectHistory> CadastralObjectHistories { get; set; } = new();
    }
}
