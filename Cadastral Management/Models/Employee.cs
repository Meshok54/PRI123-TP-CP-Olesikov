using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class Employee
    {
        [Key]
        [Column("employee_id")]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("department")]
        public string Department { get; set; }


        public User User { get; set; }
        public List<Application> Applications { get; set; } = new();
        public List<ApplicationHistory> ApplicationHistories { get; set; } = new();
        public List<CadastralObjectHistory> CadastralObjectHistories { get; set; } = new();
    }
}