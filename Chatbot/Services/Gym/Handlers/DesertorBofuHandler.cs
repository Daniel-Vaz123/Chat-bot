using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler BOFU para el escenario "Desertor".
/// Maneja: (Desertor, MOFU_Offer)
/// Confirma la reserva de la clase de retorno.
/// Transición: MOFU_Offer → BOFU_Confirm, avanza a FunnelStage.BOFU.
/// </summary>
public sealed class DesertorBofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.Desertor;
    public StepKey StepKey => StepKey.MOFU_Offer;

    public DesertorBofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        var responseText = _resources.GetResponse(ScenarioKey, StepKey);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.BOFU_Confirm,
            nextFunnelStage: FunnelStage.BOFU));
    }
}
