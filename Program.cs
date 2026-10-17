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
var connectionString = NormalizePostgres(builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DATABASE_URL"]);
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
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Fallo al aplicar las migraciones de la base de datos");
        throw;
    }
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// 5. Mapeo de Controladores (AQUÍ se registran las rutas de Controllers como NotificationsController)
app.MapControllers();

app.Run();

// Neon/Heroku entregan la cadena como URI (postgresql://usuario:clave@host/db?sslmode=require),
// pero Npgsql necesita el formato Host=...;Database=...;Username=...;Password=...
static string? NormalizePostgres(string? cs)
{
    if (string.IsNullOrWhiteSpace(cs)) return cs;
    cs = cs.Trim().Trim('"');
    if (!cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return cs;

    var uri = new Uri(cs);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = Npgsql.SslMode.Require,
        TrustServerCertificate = true
    };
    return csb.ConnectionString;
}
