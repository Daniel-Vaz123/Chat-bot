using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Services.Gym.Handlers;

/// <summary>
/// Handler TOFU para el escenario "Desertor".
/// Maneja: (Desertor, TOFU_Question)
/// Envía mensaje de re-engagement personalizado con el nombre del usuario
/// y los días de inactividad.
/// Transición: TOFU_Question → TOFU_Response (permanece en TOFU).
/// </summary>
public sealed class DesertorTofuHandler : IIntentHandler
{
    private readonly INotificationResources _resources;

    public ScenarioKey ScenarioKey => ScenarioKey.Desertor;
    public StepKey StepKey => StepKey.TOFU_Question;

    public DesertorTofuHandler(INotificationResources resources)
        => _resources = resources;

    public Task<HandlerResult> HandleAsync(
        UserProfile profile,
        ConversationState state,
        string message)
    {
        // Calcular días de inactividad para personalizar el mensaje
        var diasInactivo = profile.FechaUltimoCheckIn.HasValue
            ? (int)(DateTime.UtcNow - profile.FechaUltimoCheckIn.Value).TotalDays
            : 15;

        var variables = new Dictionary<string, string>
        {
            ["nombre"] = string.IsNullOrWhiteSpace(profile.Name) ? "amigo/a" : profile.Name,
            ["dias"]   = diasInactivo.ToString()
        };

        var responseText = _resources.GetResponse(ScenarioKey, StepKey, variables);

        return Task.FromResult(HandlerResult.Create(
            message:         responseText,
            nextStep:        StepKey.TOFU_Response,
            nextFunnelStage: FunnelStage.TOFU));
    }
}
