using Chatbot.Models.Gym;
using Chatbot.Services.Gym.Handlers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Chatbot.Services.Gym;

/// <summary>
/// Motor de estados del chatbot de gimnasio.
/// Usa un Dictionary&lt;(ScenarioKey, StepKey), IIntentHandler&gt; para resolver handlers
/// sin if/else anidados. El estado de conversación se cachea en IMemoryCache (TTL 30 min).
/// </summary>
public sealed class GymStateEngine : IGymStateEngine
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private const string CacheKeyPrefix = "gym_state_";

    private readonly IGymUserProfileRepository _repository;
    private readonly INotificationResources _resources;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GymStateEngine> _logger;

    // Diccionario principal: (ScenarioKey, StepKey) → IIntentHandler
    private readonly Dictionary<(ScenarioKey, StepKey), IIntentHandler> _handlers;

    public GymStateEngine(
        IGymUserProfileRepository repository,
        INotificationResources resources,
        IMemoryCache cache,
        ILogger<GymStateEngine> logger,
        IEnumerable<IIntentHandler> handlers)
    {
        _repository = repository;
        _resources  = resources;
        _cache      = cache;
        _logger     = logger;

        // Construir el diccionario de handlers desde los registros de DI
        _handlers = handlers.ToDictionary(h => (h.ScenarioKey, h.StepKey));
    }

    // ────────────────────────────────────────────────────────────────────────
    // IGymStateEngine
    // ────────────────────────────────────────────────────────────────────────

    public async Task<BotResponse> ProcessMessageAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        var handlerKey = (state.ActiveScenario, state.CurrentStep);

        if (!_handlers.TryGetValue(handlerKey, out var handler))
        {
            _logger.LogWarning(
                "No se encontró handler para ({Scenario}, {Step}). Retornando fallback.",
                state.ActiveScenario, state.CurrentStep);

            return BotResponse.Ok(_resources.GetResponse(ScenarioKey.None, StepKey.Initial));
        }

        HandlerResult result;
        try
        {
            result = await handler.HandleAsync(profile, state, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en handler ({Scenario}, {Step}) para usuario {UserId}.",
                state.ActiveScenario, state.CurrentStep, profile.UserId);
            return BotResponse.Error(_resources.GetResponse(ScenarioKey.None, StepKey.Initial));
        }

        // Avanzar estado — FunnelStage es monótonamente no decreciente
        state.CurrentStep     = result.NextStep;
        state.FunnelStage     = (FunnelStage)Math.Max((int)state.FunnelStage, (int)result.NextFunnelStage);
        state.LastInteraction = DateTime.UtcNow;

        // Aplicar actualizaciones de contexto del handler
        foreach (var (key, value) in result.ContextUpdates)
            state.ContextData[key] = value;

        // Sincronizar EtapaEmbudo en el perfil si avanzó
        if (result.NextFunnelStage > profile.EtapaEmbudo)
        {
            profile.EtapaEmbudo = result.NextFunnelStage;
            try { await _repository.UpdateProfileAsync(profile); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil de {UserId}.", profile.UserId);
                // No relanzar — la respuesta al usuario no debe bloquearse
            }
        }

        // Persistir estado (no bloquear respuesta si falla)
        try
        {
            await _repository.UpdateConversationStateAsync(profile.UserId, state);
            SetCache(profile.UserId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al persistir estado de {UserId}.", profile.UserId);
        }

        return result.Response;
    }

    public async Task<ConversationState> GetCurrentStateAsync(string userId)
    {
        var cacheKey = CacheKeyPrefix + userId;

        if (_cache.TryGetValue(cacheKey, out ConversationState? cached) && cached is not null)
            return cached;

        var persisted = await _repository.GetConversationStateAsync(userId);
        var state = persisted ?? new ConversationState { UserId = userId };

        SetCache(userId, state);
        return state;
    }

    public async Task InitiateScenarioAsync(string userId, ScenarioKey scenario)
    {
        var state = new ConversationState
        {
            UserId          = userId,
            ActiveScenario  = scenario,
            CurrentStep     = StepKey.TOFU_Question,
            FunnelStage     = FunnelStage.TOFU,
            LastInteraction = DateTime.UtcNow,
            ContextData     = new(),   // Limpiar contexto previo
            IsActive        = true
        };

        await _repository.UpdateConversationStateAsync(userId, state);
        SetCache(userId, state);

        _logger.LogInformation(
            "Escenario {Scenario} iniciado para usuario {UserId}.", scenario, userId);
    }

    public async Task ResetStateAsync(string userId)
    {
        var state = new ConversationState
        {
            UserId         = userId,
            ActiveScenario = ScenarioKey.None,
            CurrentStep    = StepKey.Initial,
            FunnelStage    = FunnelStage.TOFU,
            IsActive       = false
        };

        await _repository.UpdateConversationStateAsync(userId, state);
        SetCache(userId, state);

        _logger.LogInformation("Estado reseteado para usuario {UserId}.", userId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers de caché
    // ────────────────────────────────────────────────────────────────────────

    private void SetCache(string userId, ConversationState state)
    {
        var cacheKey = CacheKeyPrefix + userId;
        _cache.Set(cacheKey, state, CacheTtl);
    }
}
