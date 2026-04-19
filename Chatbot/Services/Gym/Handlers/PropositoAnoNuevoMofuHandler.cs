using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler MOFU para el escenario "Propósito de Año Nuevo".
/// Maneja: (PropositoAnoNuevo, TOFU_Response)
/// Presenta el Plan Welcome con clase de prueba gratis.
/// Transición: TOFU_Response → MOFU_Offer, avanza a FunnelStage.MOFU.
/// </summary>
public sealed class PropositoAnoNuevoMofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.PropositoAnoNuevo;
    public StepKey StepKey => StepKey.TOFU_Response;

    public PropositoAnoNuevoMofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        // Guardar en contexto si el usuario mencionó experiencia previa
        var contextUpdates = new Dictionary<string, string>
        {
            ["respuesta_experiencia"] = message
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.MOFU_Offer,
            nextFunnelStage: FunnelStage.MOFU,
            contextUpdates:  contextUpdates));
    }
}
