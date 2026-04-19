using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler BOFU para el escenario "Atleta Estancado".
/// Maneja: (AtletaEstancado, MOFU_Offer)
/// Presenta el precio del paquete y ofrece el link de pago.
/// Transición: MOFU_Offer → BOFU_Confirm, avanza a FunnelStage.BOFU.
/// </summary>
public sealed class AtletaEstancadoBofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.AtletaEstancado;
    public StepKey StepKey => StepKey.MOFU_Offer;

    public AtletaEstancadoBofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        var variables = new Dictionary<string, string>
        {
            ["coach"] = "Coach Asignado"
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey, variables);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.BOFU_Confirm,
            nextFunnelStage: FunnelStage.BOFU));
    }
}
