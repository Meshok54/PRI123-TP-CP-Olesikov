using System;
using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class CadastralObjectHistory
    {
        [Key]
        public int HistoryId { get; set; }

        public int CadastralObjectId { get; set; }
        public int ChangedByEmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        public string ChangedField { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        [Required]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        public CadastralObject CadastralObject { get; set; }
        public Employee ChangedByEmployee { get; set; }
    }
}