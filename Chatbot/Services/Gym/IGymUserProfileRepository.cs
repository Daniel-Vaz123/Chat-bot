using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym;

/// <summary>
/// Repositorio CRUD para perfiles de usuario del gimnasio y sus estados de conversación.
/// </summary>
public interface IGymUserProfileRepository
{
    /// <summary>
    /// Obtiene el perfil del usuario o crea uno nuevo con valores por defecto si no existe.
    /// </summary>
    Task<UserProfile> GetOrCreateProfileAsync(string userId);

    /// <summary>
    /// Persiste los cambios en el perfil del usuario.
    /// Lanza ArgumentException si UserId está vacío, FechaUltimoCheckIn es futura,
    /// o TotalClasesAsistidas es negativo.
    /// </summary>
    Task UpdateProfileAsync(UserProfile profile);

    /// <summary>
    /// Retorna usuarios cuyo FechaUltimoCheckIn supera el umbral de días de inactividad.
    /// </summary>
    Task<IEnumerable<UserProfile>> GetInactiveUsersAsync(int thresholdDays);

    /// <summary>
    /// Retorna usuarios que han alcanzado un hito específico de gamificación.
    /// </summary>
    Task<IEnumerable<UserProfile>> GetUsersWithMilestoneAsync(MilestoneType milestone);

    /// <summary>
    /// Persiste el estado de conversación del usuario e invalida la caché.
    /// </summary>
    Task UpdateConversationStateAsync(string userId, ConversationState state);

    /// <summary>
    /// Obtiene el estado de conversación persistido. Retorna null si no existe.
    /// </summary>
    Task<ConversationState?> GetConversationStateAsync(string userId);
}
