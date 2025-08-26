using MedConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace MedConnect.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<MedConnect.Models.RefreshToken> RefreshTokens { get; set; }
    // JWT revocation: store revoked JTIs
    // This could be a separate table for scalability, but for now, we'll use a simple string collection on User

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed some initial data
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = "AQAAAAIAAYagAAAAELYHFsQnDtNEWgJmVRQJNqtJLxp8qQzJQxLOniQYw9P6FzT+2UDpnIwOBcnGrYhCQQ==", // Password: Admin123!
                Role = Role.Admin
            },
            new User
            {
                Id = 2,
                Username = "pharmauser",
                Email = "pharma@example.com",
                PasswordHash = "AQAAAAIAAYagAAAAELYHFsQnDtNEWgJmVRQJNqtJLxp8qQzJQxLOniQYw9P6FzT+2UDpnIwOBcnGrYhCQQ==", // Password: Admin123!
                Role = Role.Pharma
            },
            new User
            {
                Id = 3,
                Username = "doctoruser",
                Email = "doctor@example.com",
                PasswordHash = "AQAAAAIAAYagAAAAELYHFsQnDtNEWgJmVRQJNqtJLxp8qQzJQxLOniQYw9P6FzT+2UDpnIwOBcnGrYhCQQ==", // Password: Admin123!
                Role = Role.Doctor
            }
        );
    }
}