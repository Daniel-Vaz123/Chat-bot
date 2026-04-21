using Chatbot.Models.Gym;
using Microsoft.Extensions.Logging;

namespace Chatbot.Services.Gym;

/// <summary>
/// Punto de entrada único para todos los mensajes del chatbot de gimnasio.
/// Implementa el algoritmo RouteMessageAsync del diseño:
///   1. Validar userId
///   2. Si estado = None → mostrar WelcomeMessage
///   3. Si mensaje es "1"-"5" y estado = None → inicializar escenario
///   4. Delegar al GymStateEngine para estados activos
/// </summary>
public sealed class GymConversationRouter : IGymConversationRouter
{
    // Mapa de opciones del menú de bienvenida → ScenarioKey
    private static readonly Dictionary<string, ScenarioKey> MenuOptionMap = new()
    {
        ["1"] = ScenarioKey.PropositoAnoNuevo,
        ["2"] = ScenarioKey.AtletaEstancado,
        ["3"] = ScenarioKey.AtletaEstancado,   // Opción 3 también va a transformación
        ["4"] = ScenarioKey.HorariosMulticlub,
        ["5"] = ScenarioKey.CongelacionMembresia
    };

    private readonly IGymStateEngine _stateEngine;
    private readonly IGymUserProfileRepository _repository;
    private readonly INotificationResources _resources;
    private readonly ILogger<GymConversationRouter> _logger;

    public GymConversationRouter(
        IGymStateEngine stateEngine,
        IGymUserProfileRepository repository,
        INotificationResources resources,
        ILogger<GymConversationRouter> logger)
    {
        _stateEngine = stateEngine;
        _repository  = repository;
        _resources   = resources;
        _logger      = logger;
    }

    public async Task<BotResponse> RouteMessageAsync(string userId, string incomingMessage)
    {
        // Validación de entrada — retornar error sin acceder al repositorio
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("RouteMessageAsync llamado con userId nulo o vacío.");
            return BotResponse.Error("No se pudo identificar al usuario. Por favor intenta de nuevo.");
        }

        incomingMessage ??= string.Empty;

        try
        {
            var profile = await _repository.GetOrCreateProfileAsync(userId);
            var state   = await _stateEngine.GetCurrentStateAsync(userId);

            // Estado inicial o reseteado → mostrar menú de bienvenida
            if (state.ActiveScenario == ScenarioKey.None)
            {
                var trimmed = incomingMessage.Trim();

                // Si el usuario seleccionó una opción del menú, inicializar escenario
                if (MenuOptionMap.TryGetValue(trimmed, out var selectedScenario))
                {
                    await _stateEngine.InitiateScenarioAsync(userId, selectedScenario);
                    state = await _stateEngine.GetCurrentStateAsync(userId);

                    _logger.LogInformation(
                        "Usuario {UserId} seleccionó opción {Option} → escenario {Scenario}.",
                        userId, trimmed, selectedScenario);

                    // Procesar el primer mensaje del escenario recién iniciado
                    return await _stateEngine.ProcessMessageAsync(profile, state, trimmed);
                }

                // Sin selección válida → mostrar menú
                return BotResponse.Ok(_resources.GetWelcomeMessage());
            }

            // Escenario activo → delegar al motor de estados
            return await _stateEngine.ProcessMessageAsync(profile, state, incomingMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no manejado en RouteMessageAsync para usuario {UserId}.", userId);
            return BotResponse.Error(
                "Ocurrió un error inesperado. Por favor escribe *menú* para volver al inicio. 😊");
        }
    }
}
