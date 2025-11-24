using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cadastral_Management.Models
{
    public class CadastralObject
    {
        [Key]
        public int CadastralObjectId { get; set; }

        [Required]
        [StringLength(14)]
        public string CadastralNumber { get; set; }

        [Required]
        [StringLength(500)]
        public string Address { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Area { get; set; }

        [Required]
        [StringLength(20)]
        public string CadastralObjectType { get; set; }

        public int OwnerId { get; set; }

        [Required]
        public DateTime RegistrationDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Citizen Owner { get; set; }
        public List<Application> Applications { get; set; } = new();
        public List<Extract> Extracts { get; set; } = new();
        public List<CadastralObjectHistory> CadastralObjectHistories { get; set; } = new();
    }
}