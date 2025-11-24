using Cadastral_Management.Models;
using Microsoft.EntityFrameworkCore;

namespace Cadastral_Management.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Citizen> Citizens { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<CadastralObject> CadastralObjects { get; set; }
        public DbSet<Extract> Extracts { get; set; }
        public DbSet<ApplicationHistory> ApplicationHistories { get; set; }
        public DbSet<CadastralObjectHistory> CadastralObjectHistories { get; set; }
        public DbSet<Attachment> Attachments { get; set; }

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