using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LaboratorioConcurrencia.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Google.Cloud.PubSub.V1;

var builder = WebApplication.CreateBuilder(args);

// --- FORZAR A KESTREL A ESCUCHAR EN EL PUERTO 5030 EN DOCKER ---
builder.WebHost.UseUrls("http://0.0.0.0:5030");

// 1. Habilitar Controllers
builder.Services.AddControllers();

// 2. Conectar a PostgreSQL (Neon Tech)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DATABASE_URL"];
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// 3. Configuración de JWT (Se especifica System.Text.Encoding para evitar la ambigüedad)
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

// 4. Crear la BD y asegurar la carpeta Data al iniciar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    /*string dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
    if (!Directory.Exists(dataDir))
    {
        Directory.CreateDirectory(dataDir);
    }*/
    db.Database.Migrate();
}

// 5. Crear notificación de correo (Endpoint Pub/Sub)
app.MapPost("/api/notifications/send", async (NotificationRequest req, IConfiguration config, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) ||
        string.IsNullOrWhiteSpace(req.Subject) ||
        string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { message = "Todos los campos son obligatorios." });
    }

    try
    {
        string projectId = config["GCP_PROJECT_ID"] ?? "laboratorio-concurrencia";
        string topicId = config["PUBSUB_TOPIC_ID"] ?? "notification-email-topic";

        TopicName topicName = TopicName.FromProjectTopic(projectId, topicId);
        PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

        string messageJson = JsonSerializer.Serialize(req);
        string messageId = await publisher.PublishAsync(messageJson);

        logger.LogInformation("Mensaje publicado en Pub/Sub con ID: {MessageId}", messageId);

        return Results.Ok(new { message = "Mensaje enviado a la cola de procesamiento", messageId });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al publicar mensaje en Pub/Sub");
        return Results.Problem("Error interno al procesar la notificación.");
    }
    // Lógica para publicar en Pub/Sub
    return Results.Ok();
}).AllowAnonymous(); // <--- Agregado para evitar problemas de JWT durante las pruebas

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// DTO para recibir la solicitud del correo
public record NotificationRequest(string Email, string Subject, string Message);