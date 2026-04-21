using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler BOFU para el escenario "Propósito de Año Nuevo".
/// Maneja: (PropositoAnoNuevo, MOFU_Offer)
/// Confirma la clase de prueba con descuento en inscripción.
/// Transición: MOFU_Offer → BOFU_Confirm, avanza a FunnelStage.BOFU.
/// </summary>
public sealed class PropositoAnoNuevoBofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.PropositoAnoNuevo;
    public StepKey StepKey => StepKey.MOFU_Offer;

    public PropositoAnoNuevoBofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        // Extraer horario del mensaje del usuario y guardarlo en contexto
        var contextUpdates = new Dictionary<string, string>
        {
            ["horario_solicitado"] = message,
            ["coach"]              = "Coach Asignado"  // Reemplazar con lógica de asignación real
        };

        var variables = new Dictionary<string, string>
        {
            ["horario"] = message,
            ["coach"]   = "Coach Asignado"
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey, variables);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.BOFU_Confirm,
            nextFunnelStage: FunnelStage.BOFU,
            contextUpdates:  contextUpdates));
    }
}
