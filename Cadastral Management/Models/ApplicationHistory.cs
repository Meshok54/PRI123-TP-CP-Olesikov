using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class ApplicationHistory
    {
        [Key]
        [Column("history_id")]
        public int HistoryId { get; set; }

        [Column("application_id")]
        public int ApplicationId { get; set; }

        [Column("changed_by_employee_id")]
        public int ChangedByEmployeeId { get; set; }

        [Column("old_status")]
        public string? OldStatus { get; set; }

        [Required]
        [Column("new_status")]
        public string NewStatus { get; set; }

        [Required]
        [Column("change_date")]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        [Column("history_comment")]
        public string? HistoryComment { get; set; }


        public Application Application { get; set; }
        public Employee ChangedByEmployee { get; set; }
    }
}