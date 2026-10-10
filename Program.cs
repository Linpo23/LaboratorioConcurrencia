using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LaboratorioConcurrencia.Data;

var builder = WebApplication.CreateBuilder(args);

// --- FORZAR A KESTREL A ESCUCHAR EN EL PUERTO 5030 EN DOCKER ---
builder.WebHost.UseUrls("http://0.0.0.0:5030");

// 1. Habilitar Controllers
builder.Services.AddControllers();

// 2. Conectar a PostgreSQL (Neon Tech)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// 3. Configuración de JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ClaveSuperSecretaDeMasDe32Caracteres!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 4. Crear la BD y asegurar la carpeta Data al iniciar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    string dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
    if (!Directory.Exists(dataDir))
    {
        Directory.CreateDirectory(dataDir);
    }

    db.Database.EnsureCreated();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();