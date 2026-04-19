using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler MOFU para el escenario "Atleta Estancado".
/// Maneja: (AtletaEstancado, TOFU_Response)
/// Presenta el Reto Beach Body con nutriólogo.
/// Transición: TOFU_Response → MOFU_Offer, avanza a FunnelStage.MOFU.
/// </summary>
public sealed class AtletaEstancadoMofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.AtletaEstancado;
    public StepKey StepKey => StepKey.TOFU_Response;

    public AtletaEstancadoMofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        // Guardar el evento especial mencionado por el usuario
        var contextUpdates = new Dictionary<string, string>
        {
            ["evento_especial"] = message
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.MOFU_Offer,
            nextFunnelStage: FunnelStage.MOFU,
            contextUpdates:  contextUpdates));
    }
}
