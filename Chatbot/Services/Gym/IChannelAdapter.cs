namespace Chatbot.Services.Gym;

/// <summary>
/// Abstracción del canal de mensajería (WhatsApp, Telegram, SMS, etc.).
/// Permite desacoplar el GymTriggerService del canal concreto.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// Envía un mensaje al usuario identificado por userId en el canal configurado.
    /// Lanza excepción si el canal no está disponible (el caller maneja el retry).
    /// </summary>
    Task SendMessageAsync(string userId, string message);
}
