using System.Collections.Concurrent;
using Chatbot.Models.Gym;

namespace Chatbot.Services.Gym;

/// <summary>
/// Implementación en memoria del repositorio de perfiles de usuario.
/// Usa ConcurrentDictionary para thread-safety en entornos de alta concurrencia.
/// Reemplazar por EF Core / DynamoDB en producción.
/// </summary>
public sealed class GymUserProfileRepository : IGymUserProfileRepository
{
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, ConversationState> _states = new();

    // ────────────────────────────────────────────────────────────────────────
    // Perfil de usuario
    // ────────────────────────────────────────────────────────────────────────

    public Task<UserProfile> GetOrCreateProfileAsync(string userId)
    {
        ValidateUserId(userId);

        var profile = _profiles.GetOrAdd(userId, id => new UserProfile
        {
            UserId    = id,
            CreatedAt = DateTime.UtcNow
        });

        return Task.FromResult(profile);
    }

    public Task UpdateProfileAsync(UserProfile profile)
    {
        ValidateUserId(profile.UserId);

        if (profile.FechaUltimoCheckIn.HasValue &&
            profile.FechaUltimoCheckIn.Value > DateTime.UtcNow)
            throw new ArgumentException(
                "FechaUltimoCheckIn no puede ser una fecha futura.",
                nameof(profile));

        if (profile.TotalClasesAsistidas < 0)
            throw new ArgumentException(
                "TotalClasesAsistidas no puede ser negativo.",
                nameof(profile));

        _profiles[profile.UserId] = profile;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<UserProfile>> GetInactiveUsersAsync(int thresholdDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

        var inactive = _profiles.Values
            .Where(p => p.FechaUltimoCheckIn.HasValue &&
                        p.FechaUltimoCheckIn.Value < cutoff);

        return Task.FromResult(inactive);
    }

    public Task<IEnumerable<UserProfile>> GetUsersWithMilestoneAsync(MilestoneType milestone)
    {
        var targetCount = (int)milestone;

        // Solo aplica para hitos de clases (valores < 1000)
        if (targetCount >= 1000)
            return Task.FromResult(Enumerable.Empty<UserProfile>());

        var users = _profiles.Values
            .Where(p => p.TotalClasesAsistidas == targetCount);

        return Task.FromResult(users);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Estado de conversación
    // ────────────────────────────────────────────────────────────────────────

    public Task UpdateConversationStateAsync(string userId, ConversationState state)
    {
        ValidateUserId(userId);
        state.UserId = userId;
        _states[userId] = state;

        // Sincronizar referencia en el perfil si existe
        if (_profiles.TryGetValue(userId, out var profile))
            profile.CurrentState = state;

        return Task.CompletedTask;
    }

    public Task<ConversationState?> GetConversationStateAsync(string userId)
    {
        ValidateUserId(userId);
        _states.TryGetValue(userId, out var state);
        return Task.FromResult(state);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId no puede ser nulo o vacío.", nameof(userId));
    }
}
