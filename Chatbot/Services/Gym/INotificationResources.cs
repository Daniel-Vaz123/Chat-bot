using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym;

/// <summary>
/// Repositorio centralizado de todos los textos de respuesta del chatbot.
/// Ninguna clase de lógica de negocio debe contener strings hardcoded de respuesta al usuario.
/// </summary>
public interface INotificationResources
{
    /// <summary>Retorna el mensaje maestro de bienvenida con las 5 opciones del menú.</summary>
    string GetWelcomeMessage();

    /// <summary>
    /// Retorna el template de respuesta para una combinación (ScenarioKey, StepKey).
    /// Si variables no es null, reemplaza los placeholders {key} con los valores del diccionario.
    /// Nunca lanza excepción — retorna mensaje de fallback si la clave no está registrada.
    /// </summary>
    string GetResponse(ScenarioKey scenario, StepKey step, Dictionary<string, string>? variables = null);

    /// <summary>
    /// Retorna el template de mensaje proactivo para un tipo de trigger.
    /// Si variables no es null, reemplaza los placeholders {key}.
    /// </summary>
    string GetProactiveMessage(TriggerType trigger, Dictionary<string, string>? variables = null);
}
