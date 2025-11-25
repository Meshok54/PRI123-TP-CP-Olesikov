using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class CadastralObjectHistory
    {
        [Key]
        [Column("history_id")]
        public int HistoryId { get; set; }

        [Column("cadastral_object_id")]
        public int CadastralObjectId { get; set; }

        [Required]
        [StringLength(50)]
        [Column("changed_field")]
        public string ChangedField { get; set; }

        [Column("old_value")]
        public string? OldValue { get; set; }

        [Column("new_value")]
        public string? NewValue { get; set; }

        [Required]
        [Column("change_date")]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        [Column("changed_by_employee_id")]
        public int ChangedByEmployeeId { get; set; }


        public CadastralObject CadastralObject { get; set; }
        public Employee ChangedByEmployee { get; set; }
    }
}