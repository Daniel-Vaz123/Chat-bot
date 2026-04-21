namespace Chatbot.Models.Gym;

/// <summary>
/// Resultado que retorna un IIntentHandler al GymStateEngine.
/// Contiene la respuesta al usuario y las instrucciones de transición de estado.
/// </summary>
public class HandlerResult
{
    /// <summary>Respuesta a enviar al usuario.</summary>
    public BotResponse Response { get; set; } = BotResponse.Ok(string.Empty);

    /// <summary>Siguiente paso al que debe avanzar el ConversationState.</summary>
    public StepKey NextStep { get; set; }

    /// <summary>Siguiente etapa del embudo. Nunca debe ser menor a la etapa actual.</summary>
    public FunnelStage NextFunnelStage { get; set; }

    /// <summary>Datos de contexto adicionales a persistir en ConversationState.ContextData.</summary>
    public Dictionary<string, string> ContextUpdates { get; set; } = new();

    public static HandlerResult Create(
        string message,
        StepKey nextStep,
        FunnelStage nextFunnelStage,
        Dictionary<string, string>? contextUpdates = null) =>
        new()
        {
            Response         = BotResponse.Ok(message),
            NextStep         = nextStep,
            NextFunnelStage  = nextFunnelStage,
            ContextUpdates   = contextUpdates ?? new()
        };
}
