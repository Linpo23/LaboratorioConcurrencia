using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioConcurrencia.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env) => _env = env;

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Archivo vacío" });

        // Usa WebRootPath si existe; si no, recurre a ContentRootPath/wwwroot aqui es el tema de la imagenen el list
        string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        string uploadsFolder = Path.Combine(webRoot, "uploads");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        string filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        string relativeUrl = $"/uploads/{fileName}";

        return Ok(new
        {
            id = Random.Shared.Next(1, 1000),
            filename = fileName,
            url = relativeUrl
        });
    }
}