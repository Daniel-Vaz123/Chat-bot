using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym;

/// <summary>
/// Motor de estados del chatbot. Resuelve el IIntentHandler correcto para cada
/// combinación (ScenarioKey, StepKey) y gestiona las transiciones del embudo.
/// </summary>
public interface IGymStateEngine
{
    /// <summary>
    /// Procesa un mensaje dentro del escenario activo del usuario.
    /// Precondición: state.ActiveScenario != ScenarioKey.None.
    /// Postcondición: ConversationState persiste con el nuevo paso y etapa de embudo.
    /// </summary>
    Task<BotResponse> ProcessMessageAsync(UserProfile profile, ConversationState state, string message);

    /// <summary>
    /// Obtiene el estado de conversación actual. Usa caché en memoria (TTL 30 min).
    /// Si no existe, retorna un ConversationState con ActiveScenario = None.
    /// </summary>
    Task<ConversationState> GetCurrentStateAsync(string userId);

    /// <summary>
    /// Inicia un nuevo escenario para el usuario.
    /// Postcondición: ActiveScenario = scenario, CurrentStep = TOFU_Question,
    /// FunnelStage = TOFU, ContextData vacío.
    /// </summary>
    Task InitiateScenarioAsync(string userId, ScenarioKey scenario);

    /// <summary>
    /// Resetea el estado a ScenarioKey.None para que el próximo mensaje muestre el menú.
    /// </summary>
    Task ResetStateAsync(string userId);
}
