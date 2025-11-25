using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cadastral_Management.Models
{
    public class Application
    {
        [Key]
        [Column("application_id")]
        public int ApplicationId { get; set; }

        [Required]
        [Column("application_date")]
        public DateTime ApplicationDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        [Column("application_status")]
        public string ApplicationStatus { get; set; } = "Принят к проверке";

        [Required]
        [StringLength(20)]
        [Column("application_type")]
        public string ApplicationType { get; set; }

        [Column("citizen_comment")]
        public string? CitizenComment { get; set; }

        [Column("decision_comment")]
        public string? DecisionComment { get; set; }

        [Column("applicant_id")]
        public int ApplicantId { get; set; }

        [Column("assigned_employee_id")]
        public int? AssignedEmployeeId { get; set; }

        [Column("cadastral_object_id")]
        public int? CadastralObjectId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;


        public Citizen Applicant { get; set; }
        public Employee? AssignedEmployee { get; set; }
        public CadastralObject? CadastralObject { get; set; }
        public List<ApplicationHistory> ApplicationHistories { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
    }
}