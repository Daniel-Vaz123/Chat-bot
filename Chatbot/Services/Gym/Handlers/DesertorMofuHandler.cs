using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler MOFU para el escenario "Desertor".
/// Maneja: (Desertor, TOFU_Response)
/// Presenta incentivo de retorno: clase especial + smoothie de proteína.
/// Transición: TOFU_Response → MOFU_Offer, avanza a FunnelStage.MOFU.
/// </summary>
public sealed class DesertorMofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.Desertor;
    public StepKey StepKey => StepKey.TOFU_Response;

    public DesertorMofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        // Guardar la razón de inactividad para seguimiento del staff
        var contextUpdates = new Dictionary<string, string>
        {
            ["razon_inactividad"] = message
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.MOFU_Offer,
            nextFunnelStage: FunnelStage.MOFU,
            contextUpdates:  contextUpdates));
    }
}
