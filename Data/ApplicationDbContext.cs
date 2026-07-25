//using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore;
//using OEEMonitoringSystem.Models;

//namespace OEEMonitoringSystem.Data
//{
//    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
//    {
//        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
//            : base(options)
//        {
//        }

//        public DbSet<Machine> Machines { get; set; }
//        public DbSet<MachineEvent> MachineEvents { get; set; }
//        public DbSet<OEECalculation> OEECalculations { get; set; }
//        public DbSet<UserMachine> UserMachines { get; set; }

//        protected override void OnModelCreating(ModelBuilder builder)
//        {
//            base.OnModelCreating(builder);

//            // Machine configuration
//            builder.Entity<Machine>(entity =>
//            {
//                entity.HasKey(e => e.Id);
//                entity.HasIndex(e => e.Name);
//                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.TargetCycleTime).HasPrecision(18, 2);
//            });

//            // MachineEvent configuration
//            builder.Entity<MachineEvent>(entity =>
//            {
//                entity.HasKey(e => e.Id);
//                entity.HasIndex(e => e.MachineId);
//                entity.HasIndex(e => e.Timestamp);
//                entity.Property(e => e.Duration).HasPrecision(18, 2);
//                entity.HasOne(e => e.Machine)
//                    .WithMany(m => m.Events)
//                    .HasForeignKey(e => e.MachineId)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });

//            // OEECalculation configuration
//            builder.Entity<OEECalculation>(entity =>
//            {
//                entity.HasKey(e => e.Id);
//                entity.HasIndex(e => e.MachineId);
//                entity.HasIndex(e => e.CalculationTime);
//                entity.Property(e => e.Availability).HasPrecision(5, 2);
//                entity.Property(e => e.Performance).HasPrecision(5, 2);
//                entity.Property(e => e.Quality).HasPrecision(5, 2);
//                entity.Property(e => e.OEE).HasPrecision(5, 2);
//                entity.HasOne(e => e.Machine)
//                    .WithMany(m => m.OEECalculations)
//                    .HasForeignKey(e => e.MachineId)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });

//            // UserMachine configuration (many-to-many)
//            builder.Entity<UserMachine>(entity =>
//            {
//                entity.HasKey(um => new { um.UserId, um.MachineId });
//                entity.HasOne(um => um.Machine)
//                    .WithMany(m => m.UserMachines)
//                    .HasForeignKey(um => um.MachineId)
//                    .OnDelete(DeleteBehavior.Cascade);
//                entity.HasOne(um => um.User)
//                    .WithMany()
//                    .HasForeignKey(um => um.UserId)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });
//        }
//    }
//}
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OEEMonitoringSystem.Models;

namespace OEEMonitoringSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<MachineEvent> MachineEvents { get; set; }
        public DbSet<OEECalculation> OEECalculations { get; set; }
        public DbSet<UserMachine> UserMachines { get; set; }
        public DbSet<Alert> Alerts { get; set; } // New table for alerts

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Machine configuration
            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TargetCycleTime).HasPrecision(18, 2);
            });

            // MachineEvent configuration
            modelBuilder.Entity<MachineEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.MachineId);
                entity.HasIndex(e => e.Timestamp);
                entity.Property(e => e.Duration).HasPrecision(18, 2);
                entity.HasOne(e => e.Machine)
                    .WithMany(m => m.Events)
                    .HasForeignKey(e => e.MachineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // OEECalculation configuration
            modelBuilder.Entity<OEECalculation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.MachineId);
                entity.HasIndex(e => e.CalculationTime);
                entity.Property(e => e.Availability).HasPrecision(5, 2);
                entity.Property(e => e.Performance).HasPrecision(5, 2);
                entity.Property(e => e.Quality).HasPrecision(5, 2);
                entity.Property(e => e.OEE).HasPrecision(5, 2);
                entity.HasOne(e => e.Machine)
                    .WithMany(m => m.OEECalculations)
                    .HasForeignKey(e => e.MachineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserMachine configuration (many-to-many)
            modelBuilder.Entity<UserMachine>(entity =>
            {
                entity.HasKey(um => new { um.UserId, um.MachineId });
                entity.HasOne(um => um.Machine)
                    .WithMany(m => m.UserMachines)
                    .HasForeignKey(um => um.MachineId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(um => um.User)
                    .WithMany()
                    .HasForeignKey(um => um.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Alert configuration
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.MachineId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.IsResolved);
                entity.Property(e => e.AlertType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Message).IsRequired();
                entity.Property(e => e.Threshold).HasPrecision(5, 2);
                entity.Property(e => e.CurrentValue).HasPrecision(5, 2);
                entity.HasOne(e => e.Machine)
                    .WithMany()
                    .HasForeignKey(e => e.MachineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

      
        }
    }
}