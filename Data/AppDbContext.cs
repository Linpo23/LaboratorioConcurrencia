using Microsoft.EntityFrameworkCore;
using LaboratorioConcurrencia.Models;

namespace LaboratorioConcurrencia.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext() { }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) {
            optionsBuilder.UseNpgsql("Host=ep-ancient-hall-b5xex7o1.c-7.us-east-2.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_TaB53FHgrnEU;SSL Mode=Require;Trust Server Certificate=true");
        }
    }
    public DbSet<User> Users { get; set; }
    public DbSet<FileModel> Files { get; set; }
}