using Cadastral_Management.Models;
using Microsoft.EntityFrameworkCore;

namespace Cadastral_Management.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Citizen> Citizens { get; set; }
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Application> Applications { get; set; }
        public virtual DbSet<CadastralObject> CadastralObjects { get; set; }
        public virtual DbSet<Extract> Extracts { get; set; }
        public virtual DbSet<ApplicationHistory> ApplicationHistories { get; set; }
        public virtual DbSet<CadastralObjectHistory> CadastralObjectHistories { get; set; }
        public virtual DbSet<Attachment> Attachments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Citizen>()
                .HasOne(c => c.User)
                .WithOne()
                .HasForeignKey<Citizen>(c => c.CitizenId);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<Employee>(e => e.EmployeeId);

        }
    }
}