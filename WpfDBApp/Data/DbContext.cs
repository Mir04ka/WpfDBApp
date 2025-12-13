using Microsoft.EntityFrameworkCore;
using WpfDBApp.Models;

namespace WpfDBApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Person> Persons { get; set; }
    
    private readonly string _connectionString; // Database connection string
    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString); // Configure SQL server as DB provider
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.SurName).HasMaxLength(50);
            entity.Property(e => e.City).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(50);
        });
    }
}