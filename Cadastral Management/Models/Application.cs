using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class Application
    {
        [Key]
        public int ApplicationId { get; set; }

        [Required]
        public DateTime ApplicationDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        public string ApplicationStatus { get; set; } = "Принят к проверке";

        [Required]
        [StringLength(20)]
        public string ApplicationType { get; set; }

        public string? CitizenComment { get; set; }
        public string? DecisionComment { get; set; }

        public int ApplicantId { get; set; }
        public int? AssignedEmployeeId { get; set; }
        public int? CadastralObjectId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Citizen Applicant { get; set; }
        public Employee? AssignedEmployee { get; set; }
        public CadastralObject? CadastralObject { get; set; }
        public List<ApplicationHistory> ApplicationHistories { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
    }
}