namespace LaboratorioConcurrencia.Models;

public class User
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FotoUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FileModel
{
    public int Id { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}