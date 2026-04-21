using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym;

/// <summary>
/// Punto de entrada único para todos los mensajes entrantes del chatbot de gimnasio.
/// Decide si mostrar el menú de bienvenida, inicializar un escenario o delegar al motor de estados.
/// </summary>
public interface IGymConversationRouter
{
    /// <summary>
    /// Enruta un mensaje entrante al flujo correcto y retorna la respuesta del bot.
    /// Precondición: userId no nulo/vacío, incomingMessage no nulo.
    /// Postcondición: siempre retorna un BotResponse no nulo con Message no vacío.
    /// </summary>
    Task<BotResponse> RouteMessageAsync(string userId, string incomingMessage);
}
