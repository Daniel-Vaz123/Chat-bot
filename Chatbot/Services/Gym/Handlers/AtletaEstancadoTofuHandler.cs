using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler TOFU para el escenario "Atleta Estancado".
/// Maneja: (AtletaEstancado, TOFU_Question)
/// Pregunta sobre el objetivo y si hay un evento especial con fecha límite.
/// Transición: TOFU_Question → TOFU_Response (permanece en TOFU).
/// </summary>
public sealed class AtletaEstancadoTofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.AtletaEstancado;
    public StepKey StepKey => StepKey.TOFU_Question;

    public AtletaEstancadoTofuHandler(INotificationResources resources)
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
