using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaboratorioConcurrencia.Data; // Ajusta este namespace si tu DbContext está en otra carpeta
using LaboratorioConcurrencia.Models;

namespace LaboratorioConcurrencia.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // 1. READ ALL (GET: api/users)
    //[AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users.ToListAsync();
        return Ok(users);
    }

    // 2. READ ONE (GET: api/users/{id})
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _context.Users.FindAsync(id);
        return user == null ? NotFound(new { message = "Usuario no encontrado" }) : Ok(user);
    }

    // 3. CREATE (POST: api/users)
    [HttpPost]
    //[AllowAnonymous] // Permite registro inicial sin hay token 
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var nuevoUsuario = new User
        {
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Email = dto.Email,
            // Guardamos el password directo en PasswordHash para coincidir con AuthController
            PasswordHash = dto.Password,
            FotoUrl = string.IsNullOrEmpty(dto.FotoUrl) ? "/uploads/def.png" : dto.FotoUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUserById), new { id = nuevoUsuario.Id }, nuevoUsuario);
    }

    // 4. UPDATE (PUT: api/users/{id})
    //[AllowAnonymous]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] CreateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado" });

        user.Nombre = dto.Nombre;
        user.Apellido = dto.Apellido;
        user.Email = dto.Email;
        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = dto.Password;
        }
        if (!string.IsNullOrEmpty(dto.FotoUrl))
        {
            user.FotoUrl = dto.FotoUrl;
        }

        await _context.SaveChangesAsync();
        return Ok(user);
    }

    // 5. DELETE (DELETE: api/users/{id})
   // [AllowAnonymous]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Usuario eliminado exitosamente" });
    }
}