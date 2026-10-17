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
// Tambien limpia comillas y prefijos pegados por error (p. ej. psql 'postgresql://...' o DATABASE_URL=...).
static string? NormalizePostgres(string? cs)
{
    if (string.IsNullOrWhiteSpace(cs)) return cs;

    cs = cs.Trim().Trim('"', '\'', ' ', '\r', '\n', '\t', '\uFEFF');

    var m = System.Text.RegularExpressions.Regex.Match(
        cs, @"postgres(?:ql)?://[^\s'""]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    if (m.Success)
    {
        var uri = new Uri(m.Value);
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

    // Formato Host=...;... pero con basura delante (ej. DATABASE_URL=Host=...)
    var hostIdx = cs.IndexOf("Host=", StringComparison.OrdinalIgnoreCase);
    if (hostIdx > 0) cs = cs[hostIdx..].Trim('"', '\'');

    if (!cs.Contains('='))
    {
        throw new InvalidOperationException(
            $"La cadena de conexion a la base de datos no tiene un formato valido (longitud {cs.Length}, contiene '://': {cs.Contains("://")}). " +
            "Usa 'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require' o la URI postgresql://usuario:clave@host/db");
    }
    return cs;
}
