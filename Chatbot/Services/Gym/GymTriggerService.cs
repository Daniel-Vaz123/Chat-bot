using Chatbot.Models.Gym;
using Microsoft.Extensions.Logging;

namespace Chatbot.Services.Gym;

/// <summary>
/// Motor de eventos proactivos del chatbot de gimnasio.
/// Detecta condiciones en los perfiles (inactividad, hitos, post-clase)
/// y dispara mensajes automáticos sin intervención del usuario.
///
/// Invariante de deduplicación: nunca envía más de un mensaje del mismo
/// tipo al mismo usuario dentro de la ventana de tiempo configurada.
/// </summary>
public sealed class GymTriggerService : IGymTriggerService
{
    private const int InactivityThresholdDays = 15;
    private const int PostFirstClassMinHours  = 20;
    private const int PostFirstClassMaxHours  = 28;
    private static readonly TimeSpan InactivityDeduplicationWindow = TimeSpan.FromHours(24);

    private readonly IGymUserProfileRepository _repository;
    private readonly IGymStateEngine _stateEngine;
    private readonly INotificationResources _resources;
    private readonly IChannelAdapter _channel;
    private readonly ILogger<GymTriggerService> _logger;

    public GymTriggerService(
        IGymUserProfileRepository repository,
        IGymStateEngine stateEngine,
        INotificationResources resources,
        IChannelAdapter channel,
        ILogger<GymTriggerService> logger)
    {
        _repository = repository;
        _stateEngine = stateEngine;
        _resources   = resources;
        _channel     = channel;
        _logger      = logger;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Trigger 1: Inactividad > 15 días (Escenario Desertor)
    // ────────────────────────────────────────────────────────────────────────

    public async Task RunInactivityCheckAsync()
    {
        _logger.LogInformation("Iniciando verificación de inactividad (umbral: {Days} días).",
            InactivityThresholdDays);

        var inactiveUsers = await _repository.GetInactiveUsersAsync(InactivityThresholdDays);

        foreach (var user in inactiveUsers)
        {
            // Verificar ventana de deduplicación de 24h
            if (WasTriggerSentRecently(user, "LastInactivityTrigger", InactivityDeduplicationWindow))
            {
                _logger.LogDebug("Usuario {UserId} ya recibió trigger de inactividad en las últimas 24h. Omitiendo.",
                    user.UserId);
                continue;
            }

            var diasInactivo = user.FechaUltimoCheckIn.HasValue
                ? (int)(DateTime.UtcNow - user.FechaUltimoCheckIn.Value).TotalDays
                : InactivityThresholdDays;

            var message = _resources.GetProactiveMessage(TriggerType.Inactivity15Days,
                new Dictionary<string, string>
                {
                    ["nombre"] = string.IsNullOrWhiteSpace(user.Name) ? "amigo/a" : user.Name,
                    ["dias"]   = diasInactivo.ToString()
                });

            var sent = await TrySendMessageAsync(user.UserId, message);

            if (sent)
            {
                // Iniciar escenario Desertor y marcar trigger enviado
                await _stateEngine.InitiateScenarioAsync(user.UserId, ScenarioKey.Desertor);
                user.Metadata["LastInactivityTrigger"] = DateTime.UtcNow;
                await _repository.UpdateProfileAsync(user);

                _logger.LogInformation(
                    "Trigger de inactividad enviado a {UserId} ({Days} días inactivo).",
                    user.UserId, diasInactivo);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Trigger 2: Seguimiento 24h post-primera-clase
    // ────────────────────────────────────────────────────────────────────────

    public async Task RunPostFirstClassFollowUpAsync()
    {
        _logger.LogInformation("Iniciando seguimiento post-primera-clase.");

        var allProfiles = await _repository.GetInactiveUsersAsync(0); // Obtener todos

        var eligibleUsers = allProfiles.Where(p =>
            p.FechaPrimeraClase.HasValue &&
            !WasTriggerSentRecently(p, "LastPostFirstClassTrigger", TimeSpan.FromDays(7)));

        foreach (var user in eligibleUsers)
        {
            var hoursElapsed = (DateTime.UtcNow - user.FechaPrimeraClase!.Value).TotalHours;

            if (hoursElapsed < PostFirstClassMinHours || hoursElapsed > PostFirstClassMaxHours)
                continue;

            var message = _resources.GetProactiveMessage(TriggerType.PostFirstClass24h,
                new Dictionary<string, string>
                {
                    ["nombre"] = string.IsNullOrWhiteSpace(user.Name) ? "amigo/a" : user.Name
                });

            var sent = await TrySendMessageAsync(user.UserId, message);

            if (sent)
            {
                user.Metadata["LastPostFirstClassTrigger"] = DateTime.UtcNow;
                await _repository.UpdateProfileAsync(user);

                _logger.LogInformation(
                    "Seguimiento post-primera-clase enviado a {UserId}.", user.UserId);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Trigger 3: Hitos de gamificación
    // ────────────────────────────────────────────────────────────────────────

    public async Task RunMilestoneCheckAsync()
    {
        var milestones = new[] { MilestoneType.Class10, MilestoneType.Class50, MilestoneType.Class100 };

        foreach (var milestone in milestones)
        {
            var users = await _repository.GetUsersWithMilestoneAsync(milestone);

            foreach (var user in users)
            {
                var milestoneKey = $"Milestone_{milestone}_Sent";
                if (user.Metadata.ContainsKey(milestoneKey))
                    continue;

                var message = _resources.GetProactiveMessage(TriggerType.Milestone,
                    new Dictionary<string, string>
                    {
                        ["nombre"] = string.IsNullOrWhiteSpace(user.Name) ? "campeón/a" : user.Name,
                        ["numero"] = ((int)milestone).ToString()
                    });

                var sent = await TrySendMessageAsync(user.UserId, message);

                if (sent)
                {
                    user.Metadata[milestoneKey] = DateTime.UtcNow;
                    await _repository.UpdateProfileAsync(user);
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Trigger 4: Recuperación de abandono de formulario (stub)
    // ────────────────────────────────────────────────────────────────────────

    public Task RunAbandonedFormCheckAsync()
    {
        // Implementar cuando se integre el sistema de pagos/CRM
        _logger.LogDebug("RunAbandonedFormCheckAsync: pendiente de integración con CRM.");
        return Task.CompletedTask;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Trigger 5: Reporte mensual de progreso
    // ────────────────────────────────────────────────────────────────────────

    public Task RunMonthlyProgressReportAsync()
    {
        // Implementar cuando se integre el sistema de evaluación InBody
        _logger.LogDebug("RunMonthlyProgressReportAsync: pendiente de integración con InBody.");
        return Task.CompletedTask;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta enviar un mensaje por el canal. Retorna true si fue exitoso.
    /// Si falla, loggea Warning y retorna false (no interrumpe el batch).
    /// </summary>
    private async Task<bool> TrySendMessageAsync(string userId, string message)
    {
        try
        {
            await _channel.SendMessageAsync(userId, message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Fallo al enviar mensaje proactivo a {UserId}. Se reintentará en el próximo ciclo.",
                userId);
            return false;
        }
    }

    /// <summary>
    /// Verifica si un trigger fue enviado recientemente dentro de la ventana de tiempo.
    /// </summary>
    private static bool WasTriggerSentRecently(
        UserProfile user,
        string metadataKey,
        TimeSpan window)
    {
        if (!user.Metadata.TryGetValue(metadataKey, out var lastSentObj))
            return false;

        if (lastSentObj is DateTime lastSent)
            return (DateTime.UtcNow - lastSent) < window;

        return false;
    }
}
