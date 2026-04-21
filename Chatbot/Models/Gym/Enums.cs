namespace Chatbot.Models.Gym;

/// <summary>
/// Identifica cada uno de los 16 escenarios de negocio del chatbot de gimnasio.
/// Los valores numéricos 1-5 coinciden con las opciones del menú de bienvenida.
/// </summary>
public enum ScenarioKey
{
    None                 = 0,
    PropositoAnoNuevo    = 1,  // Opción 1: Clase de prueba gratis
    AtletaEstancado      = 2,  // Opción 2: Planes y promos / Misión transformación
    Desertor             = 3,  // Trigger automático: inactividad > 15 días
    ConsultaPrecios      = 4,
    MiedoPesas           = 5,
    HorariosMulticlub    = 6,
    UrgenciaClases       = 7,
    VentaGrupal          = 8,
    RecuperacionAbandono = 9,
    CongelacionMembresia = 10,
    CrossSellingNutricion = 11,
    FelicitacionHito     = 12,
    EncuestaClase        = 13,
    ProgramaReferidos    = 14,
    ReporteResultados    = 15,
    GestionQuejas        = 16
}

/// <summary>
/// Pasos dentro de un escenario activo. Representan la posición en el embudo de conversión.
/// </summary>
public enum StepKey
{
    Initial,
    TOFU_Question,
    TOFU_Response,
    MOFU_Offer,
    MOFU_Response,
    BOFU_Confirm,
    BOFU_Payment,
    Fidelizacion_Day1,
    Fidelizacion_Week2,
    Completed
}

/// <summary>
/// Etapa del embudo de conversión. Es monótonamente no decreciente durante una conversación.
/// </summary>
public enum FunnelStage
{
    TOFU         = 0,  // Atracción / Reactivación
    MOFU         = 1,  // Consideración
    BOFU         = 2,  // Conversión
    Fidelizacion = 3   // Post-venta / Retención
}

/// <summary>Tipo de membresía del socio.</summary>
public enum MembershipType
{
    None      = 0,
    Basic     = 1,
    Premium   = 2,
    Multiclub = 3,
    Frozen    = 4   // Membresía congelada temporalmente
}

/// <summary>Objetivo de fitness declarado por el usuario.</summary>
public enum FitnessGoal
{
    NotDefined  = 0,
    WeightLoss  = 1,
    MuscleGain  = 2,
    Endurance   = 3,
    Flexibility = 4,
    General     = 5
}

/// <summary>Hitos de gamificación que disparan mensajes de felicitación.</summary>
public enum MilestoneType
{
    Class10  = 10,
    Class50  = 50,
    Class100 = 100,
    Month1   = 1001,
    Month6   = 1006
}

/// <summary>Tipos de eventos proactivos que dispara el GymTriggerService.</summary>
public enum TriggerType
{
    Inactivity15Days,
    PostFirstClass24h,
    AbandonedForm2h,
    Milestone,
    MonthlyReport
}
