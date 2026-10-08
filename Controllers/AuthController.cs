using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LaboratorioConcurrencia.Data; // Asegúrate de ajustar este namespace si tu DbContext está en otra carpeta
using LaboratorioConcurrencia.Models;

namespace LaboratorioConcurrencia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;

    public AuthController(IConfiguration config, AppDbContext context)
    {
        _config = config;
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // 1. Permite pasar con el usuario por defecto de pruebas (quemado)
        if (dto.Email == "estudiante@itla.edu.do" && dto.Password == "123456")
        {
            var defaultToken = GenerarTokenJWT(dto.Email);
            return Ok(new { token = defaultToken });
        }

        // 2. Busca el usuario registrado dinámicamente en la base de datos SQLite
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

        if (user == null)
        {
            return Unauthorized(new { message = "Usuario no encontrado" });
        }

        // 3. Compara contraseña (acepta la propiedad PasswordHash o Password directa)
        if (user.PasswordHash != dto.Password)
        {
            return Unauthorized(new { message = "Contraseña incorrecta" });
        }

        // 4. Si la contraseña coincide, genera y retorna el JWT
        var token = GenerarTokenJWT(user.Email);
        return Ok(new { token });
    }

    private string GenerarTokenJWT(string email)
    {
        var secretKey = _config["Jwt:Key"] ?? "ClaveSuperSecretaDeMasDe32Caracteres!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.Email, email) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}