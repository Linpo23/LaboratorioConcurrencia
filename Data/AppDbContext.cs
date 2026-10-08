using Microsoft.EntityFrameworkCore;
using LaboratorioConcurrencia.Models;

namespace LaboratorioConcurrencia.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<FileModel> Files { get; set; }
}