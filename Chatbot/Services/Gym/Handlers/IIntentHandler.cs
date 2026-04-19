using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Contrato para cada handler de intención en la máquina de estados.
/// Cada implementación concreta maneja exactamente una combinación (ScenarioKey, StepKey).
/// El GymStateEngine los registra en un Dictionary y los resuelve sin if/else anidados.
/// </summary>
public interface IIntentHandler
{
    /// <summary>
    /// Clave del escenario que este handler maneja.
    /// Usada por GymStateEngine para construir el Dictionary de handlers.
    /// </summary>
    ScenarioKey ScenarioKey { get; }

    /// <summary>
    /// Paso del escenario que este handler maneja.
    /// </summary>
    StepKey StepKey { get; }

    /// <summary>
    /// Procesa el mensaje del usuario y retorna la respuesta + instrucciones de transición.
    /// Precondición: profile y state no son nulos.
    /// Postcondición: HandlerResult.Response.Message no es nulo ni vacío.
    /// </summary>
    Task<HandlerResult> HandleAsync(UserProfile profile, ConversationState state, string message);
}
