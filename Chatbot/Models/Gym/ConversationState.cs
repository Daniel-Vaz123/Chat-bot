namespace Chatbot.Models.Gym;

/// <summary>
/// Representa el estado activo de una conversación para un usuario específico.
/// Es la pieza central de la máquina de estados: determina qué handler se ejecuta
/// en cada mensaje entrante.
/// </summary>
public class ConversationState
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>Escenario activo. ScenarioKey.None indica que no hay flujo activo (mostrar menú).</summary>
    public ScenarioKey ActiveScenario { get; set; } = ScenarioKey.None;

    /// <summary>Paso actual dentro del escenario activo.</summary>
    public StepKey CurrentStep { get; set; } = StepKey.Initial;

    /// <summary>
    /// Etapa del embudo de conversión. Es monótonamente no decreciente:
    /// TOFU(0) → MOFU(1) → BOFU(2) → Fidelizacion(3).
    /// </summary>
    public FunnelStage FunnelStage { get; set; } = FunnelStage.TOFU;

    /// <summary>Timestamp UTC de la última interacción. Usado para TTL de caché y detección de abandono.</summary>
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Datos de contexto específicos del escenario activo (ej. horario elegido, evento especial).
    /// Se limpia al iniciar un nuevo escenario.
    /// </summary>
    public Dictionary<string, string> ContextData { get; set; } = new();

    /// <summary>Indica si la conversación está activa. False cuando el flujo se completó.</summary>
    public bool IsActive { get; set; } = true;
}
