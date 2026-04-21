using Chatbot.Services.Gym;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chatbot.BackgroundServices;

/// <summary>
/// BackgroundService de .NET 8 que ejecuta los triggers proactivos del chatbot de gimnasio.
/// Usa PeriodicTimer para evitar drift de intervalos (a diferencia de Task.Delay).
/// El intervalo es configurable vía constructor (default: 1 hora).
/// </summary>
public sealed class GymTriggerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GymTriggerBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public GymTriggerBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<GymTriggerBackgroundService> logger,
        TimeSpan? interval = null)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _interval     = interval ?? TimeSpan.FromHours(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GymTriggerBackgroundService iniciado. Intervalo: {Interval}.", _interval);

        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cycleStart = DateTime.UtcNow;
            _logger.LogInformation("Ciclo de triggers iniciado a las {Time} UTC.", cycleStart);

            try
            {
                // Crear scope por ciclo — GymTriggerService puede ser scoped
                await using var scope = _scopeFactory.CreateAsyncScope();
                var triggerService = scope.ServiceProvider.GetRequiredService<IGymTriggerService>();

                await triggerService.RunInactivityCheckAsync();
                await triggerService.RunPostFirstClassFollowUpAsync();
                await triggerService.RunMilestoneCheckAsync();
                await triggerService.RunAbandonedFormCheckAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Loggear pero no detener el servicio — el próximo ciclo reintentará
                _logger.LogError(ex, "Error en ciclo de triggers. El servicio continuará.");
            }

            var duration = DateTime.UtcNow - cycleStart;
            _logger.LogInformation(
                "Ciclo de triggers completado en {Duration:F1}s.", duration.TotalSeconds);
        }

        _logger.LogInformation("GymTriggerBackgroundService detenido.");
    }
}
