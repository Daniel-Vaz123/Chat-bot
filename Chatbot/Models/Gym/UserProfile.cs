namespace Chatbot.Models.Gym;

/// <summary>
/// Perfil completo del usuario/socio del gimnasio.
/// Contiene tanto datos de negocio (membresía, objetivo) como datos de seguimiento
/// (último check-in, lesiones, etapa del embudo).
/// </summary>
public class UserProfile
{
    /// <summary>Identificador único proveniente del canal de mensajería (ej. WhatsApp ID).</summary>
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Tipo de membresía activa del socio.</summary>
    public MembershipType TipoMembresia { get; set; } = MembershipType.None;

    /// <summary>Objetivo de fitness declarado. Usado para personalizar respuestas.</summary>
    public FitnessGoal Objetivo { get; set; } = FitnessGoal.NotDefined;

    /// <summary>
    /// Fecha y hora UTC del último check-in registrado.
    /// Vital para el trigger del escenario Desertor (inactividad > 15 días).
    /// </summary>
    public DateTime? FechaUltimoCheckIn { get; set; }

    /// <summary>
    /// Fecha y hora UTC de la primera clase asistida.
    /// Usado para el trigger de seguimiento post-primera-clase (24h).
    /// </summary>
    public DateTime? FechaPrimeraClase { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Lista de lesiones previas declaradas por el usuario.
    /// Información crítica de salud para personalizar rutinas.
    /// </summary>
    public List<string> LesionesPrevias { get; set; } = new();

    /// <summary>
    /// Total acumulado de clases asistidas. Siempre >= 0.
    /// Usado para hitos de gamificación (clase 10, 50, 100).
    /// </summary>
    public int TotalClasesAsistidas { get; set; }

    /// <summary>Etapa actual del embudo de conversión del usuario.</summary>
    public FunnelStage EtapaEmbudo { get; set; } = FunnelStage.TOFU;

    /// <summary>Estado activo de la conversación. Null si no hay conversación en curso.</summary>
    public ConversationState? CurrentState { get; set; }

    /// <summary>
    /// Datos adicionales flexibles (ej. LastInactivityTrigger, LastMilestoneSent).
    /// Evita agregar columnas para cada trigger.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
