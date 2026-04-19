namespace Chatbot.Services.Gym;

/// <summary>
/// Motor de eventos proactivos del chatbot. Detecta condiciones en los perfiles
/// de usuario y dispara mensajes automáticos sin intervención del usuario.
/// </summary>
public interface IGymTriggerService
{
    /// <summary>
    /// Detecta socios con FechaUltimoCheckIn > 15 días e inicia el escenario Desertor.
    /// No envía duplicados dentro de una ventana de 24h por usuario.
    /// </summary>
    Task RunInactivityCheckAsync();

    /// <summary>
    /// Envía mensaje de seguimiento a usuarios cuya FechaPrimeraClase fue hace 20-28 horas.
    /// </summary>
    Task RunPostFirstClassFollowUpAsync();

    /// <summary>
    /// Detecta usuarios que alcanzaron hitos de gamificación (clase 10, 50, 100).
    /// </summary>
    Task RunMilestoneCheckAsync();

    /// <summary>
    /// Recupera usuarios que iniciaron el proceso de pago/registro pero no lo completaron.
    /// Ventana: 2 horas después del abandono.
    /// </summary>
    Task RunAbandonedFormCheckAsync();

    /// <summary>
    /// Envía recordatorio de evaluación corporal mensual (InBody/progreso).
    /// </summary>
    Task RunMonthlyProgressReportAsync();
}
