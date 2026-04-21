using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler TOFU para el escenario "Propósito de Año Nuevo".
/// Maneja: (PropositoAnoNuevo, TOFU_Question)
/// Pregunta si el usuario ha entrenado antes o empieza desde cero.
/// Transición: TOFU_Question → TOFU_Response (permanece en TOFU).
/// </summary>
public sealed class PropositoAnoNuevoTofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.PropositoAnoNuevo;
    public StepKey StepKey => StepKey.TOFU_Question;

    public PropositoAnoNuevoTofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        var responseText = _resources.GetResponse(ScenarioKey, StepKey);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.TOFU_Response,
            nextFunnelStage: FunnelStage.TOFU));
    }
}
