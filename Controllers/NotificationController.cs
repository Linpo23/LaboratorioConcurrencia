using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.PubSub.V1;
using System.Text.Json;

namespace LaboratorioConcurrencia.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Genera automáticamente la ruta: /api/notifications
    public class NotificationsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(IConfiguration config, ILogger<NotificationsController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost("send")] // Se une a la ruta base -> /api/notifications/send
        [AllowAnonymous]
        public async Task<IActionResult> SendNotification([FromBody] NotificationRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Subject) ||
                string.IsNullOrWhiteSpace(req.Message))
            {
                return BadRequest(new { message = "Todos los campos son obligatorios." });
            }

            try
            {
                string projectId = _config["GCP_PROJECT_ID"] ?? "laboratorio-concurrencia";
                string topicId = _config["PUBSUB_TOPIC_ID"] ?? "notification-email-topic-v2";

                TopicName topicName = TopicName.FromProjectTopic(projectId, topicId);
                PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

                string messageJson = JsonSerializer.Serialize(req);
                string messageId = await publisher.PublishAsync(messageJson);

                _logger.LogInformation("Mensaje publicado en Pub/Sub con ID: {MessageId}", messageId);

                return Ok(new { message = "Mensaje enviado a la cola de procesamiento", messageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al publicar mensaje en Pub/Sub");
                return StatusCode(500, "Error interno al procesar la notificación.");
            }
        }
    }

    public record NotificationRequest(string Email, string Subject, string Message);
}