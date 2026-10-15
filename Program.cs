using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LaboratorioConcurrencia.Data;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- FORZAR A KESTREL A ESCUCHAR EN EL PUERTO 5030 EN DOCKER ---
builder.WebHost.UseUrls("http://0.0.0.0:5030");

// 1. Habilitar Controllers
builder.Services.AddControllers();

// 2. Conectar a PostgreSQL (Neon Tech)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DATABASE_URL"];
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
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 4. Migración de Base de Datos
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// 5. Mapeo de Controladores (AQUÍ se registran las rutas de Controllers como NotificationsController)
app.MapControllers();

app.Run();