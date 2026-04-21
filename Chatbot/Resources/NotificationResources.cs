using Chatbot.Models.Gym;
using Chatbot.Services.Gym;

namespace Chatbot.Resources;

/// <summary>
/// Repositorio centralizado de todos los textos de respuesta del chatbot de gimnasio.
/// Se registra como Singleton — carga todos los templates en el constructor (O(1) por lookup).
/// Los placeholders usan la sintaxis {key} y se reemplazan en tiempo de ejecución.
/// </summary>
public sealed class NotificationResources : INotificationResources
{
    private const string FallbackMessage =
        "Disculpa, no entendí tu mensaje. Escribe un número del 1 al 5 para ver el menú de opciones. 😊";

    // ─── Diccionario principal: (ScenarioKey, StepKey) → template ───────────
    private readonly Dictionary<(ScenarioKey, StepKey), string> _responses;

    // ─── Diccionario de mensajes proactivos: TriggerType → template ─────────
    private readonly Dictionary<TriggerType, string> _proactiveMessages;

    public NotificationResources()
    {
        _responses = BuildResponseDictionary();
        _proactiveMessages = BuildProactiveMessageDictionary();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Interfaz pública
    // ────────────────────────────────────────────────────────────────────────

    public string GetWelcomeMessage() =>
        """
        ¡Hola! 💪 Bienvenido a [Nombre del Gimnasio].
        Hoy es un excelente día para empezar a construir tu mejor versión. 🔥

        Estamos listos para ayudarte a alcanzar tus objetivos.
        Para darte la información exacta, dinos qué buscas hoy:

        1️⃣ CLASE DE PRUEBA GRATIS: Ven a conocernos y entrena hoy sin costo. 🆓
        2️⃣ PLANES Y PROMOS: Conoce nuestras membresías (Individual, Dúo y Estudiante). 💸
        3️⃣ MISIÓN TRANSFORMACIÓN: Quiero bajar de peso o ganar músculo (Asesoría con Coach). 📈
        4️⃣ HORARIOS Y CLASES: Consulta el calendario de Yoga, Box, CrossFit y más. 🗓️
        5️⃣ YA SOY SOCIO: Reservas de clases, pagos o soporte. 👤

        🎁 BONO DE HOY: Si te inscribes en las próximas 24 horas, ¡te regalamos la Inscripción y un Plan Nutricional básico! 🍎✨

        Escribe el número de tu opción para comenzar.
        """;

    public string GetResponse(
        ScenarioKey scenario,
        StepKey step,
        Dictionary<string, string>? variables = null)
    {
        if (!_responses.TryGetValue((scenario, step), out var template))
            return FallbackMessage;

        return variables is { Count: > 0 }
            ? InterpolateVariables(template, variables)
            : template;
    }

    public string GetProactiveMessage(
        TriggerType trigger,
        Dictionary<string, string>? variables = null)
    {
        if (!_proactiveMessages.TryGetValue(trigger, out var template))
            return FallbackMessage;

        return variables is { Count: > 0 }
            ? InterpolateVariables(template, variables)
            : template;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Construcción del diccionario de respuestas
    // ────────────────────────────────────────────────────────────────────────

    private static Dictionary<(ScenarioKey, StepKey), string> BuildResponseDictionary() => new()
    {
        // ── Fallback global ──────────────────────────────────────────────────
        [(ScenarioKey.None, StepKey.Initial)] = FallbackMessage,

        // ── Caso 1: Propósito de Año Nuevo ───────────────────────────────────
        [(ScenarioKey.PropositoAnoNuevo, StepKey.TOFU_Question)] =
            "¡Qué gran decisión estás tomando para tu salud! 💪 " +
            "En [Nombre del Gimnasio] no solo te damos acceso a máquinas, te acompañamos en tu proceso. " +
            "Para darte el plan ideal, cuéntame: ¿Has entrenado antes o buscas empezar desde cero?",

        [(ScenarioKey.PropositoAnoNuevo, StepKey.TOFU_Response)] =
            "¡Es totalmente normal sentir eso! 😊 Por eso tenemos el *Plan Welcome*: " +
            "incluye una rutina de inducción con entrenador personal y acceso a nuestra App con videos de cada ejercicio. 📱\n\n" +
            "Mira cómo es tu primer día con nosotros: [Link/Video]\n\n" +
            "¿Te gustaría venir a una *Clase de Prueba GRATIS* para que conozcas el ambiente?",

        [(ScenarioKey.PropositoAnoNuevo, StepKey.MOFU_Offer)] =
            "¡Claro que sí! 🎉 Te espera el Coach [Nombre del Coach].\n\n" +
            "Solo trae ropa cómoda, agua y mucha actitud. 💧\n\n" +
            "👉 *Oferta especial:* Si te inscribes al terminar tu clase, te regalamos la Inscripción (ahorras $[Monto]). ¿Te anoto?\n\n" +
            "Dime el horario que prefieres y lo confirmamos.",

        [(ScenarioKey.PropositoAnoNuevo, StepKey.BOFU_Confirm)] =
            "¡Perfecto, quedas anotado/a! ✅\n\n" +
            "Recuerda: mañana a las {horario} te espera el Coach {coach}.\n" +
            "Trae ropa cómoda, agua y mucha actitud. ¡Nos vemos! 💪",

        [(ScenarioKey.PropositoAnoNuevo, StepKey.Fidelizacion_Day1)] =
            "¡Felicidades por tu primer entrenamiento, {nombre}! 🎊\n\n" +
            "¿Cómo te sientes? Es normal estar un poco adolorido/a, ¡es señal de progreso! 💪\n" +
            "Nos vemos pasado mañana para tu segunda sesión. ¡Tú puedes!",

        // ── Caso 2: Atleta Estancado ─────────────────────────────────────────
        [(ScenarioKey.AtletaEstancado, StepKey.TOFU_Question)] =
            "¡Hola! Tenemos un equipo de expertos en transformación rápida. ⚡\n\n" +
            "Ese es un reto ambicioso pero totalmente lograble con la guía correcta. " +
            "¿Es para algún evento especial o tienes una fecha límite en mente?",

        [(ScenarioKey.AtletaEstancado, StepKey.TOFU_Response)] =
            "¡Genial, eso es motivación de sobra! 🏖️\n\n" +
            "Para objetivos con fecha límite, recomendamos el *Reto Beach Body*:\n" +
            "✅ 4 semanas de entrenamiento HIIT\n" +
            "✅ Plan de nutrición personalizado\n" +
            "✅ Suplementación básica\n\n" +
            "Aquí puedes ver resultados de otros alumnos en 30 días: [Fotos Antes/Después]\n\n" +
            "¿Quieres una cita con nuestro nutriólogo mañana para iniciar?",

        [(ScenarioKey.AtletaEstancado, StepKey.MOFU_Offer)] =
            "¡Excelente decisión! 💰\n\n" +
            "El paquete integral *Reto Beach Body* cuesta $[Monto] e incluye todo lo mencionado. " +
            "Además, te regalamos un Shaker oficial del gym. 🥤\n\n" +
            "¿Te mando el link de pago para asegurar tu lugar?",

        [(ScenarioKey.AtletaEstancado, StepKey.BOFU_Confirm)] =
            "¡Listo! Tu lugar en el Reto Beach Body está confirmado. ✅\n\n" +
            "El Coach {coach} te estará esperando. ¡A romper esas marcas! 🔥",

        [(ScenarioKey.AtletaEstancado, StepKey.Fidelizacion_Week2)] =
            "¡Mitad del camino, {nombre}! 🔥\n\n" +
            "El Coach me dice que estás rompiendo tus marcas. " +
            "No aflojes con la dieta hoy, ¡esas vacaciones valen el esfuerzo! 🏖️",

        // ── Caso 3: Desertor ─────────────────────────────────────────────────
        [(ScenarioKey.Desertor, StepKey.TOFU_Question)] =
            "¡Hola {nombre}! Notamos un vacío en el área de pesas... 🏋️‍♀️\n\n" +
            "Llevas {dias} días sin registrar tu entrada. ¿Todo bien? Te extrañamos en la comunidad. 💙",

        [(ScenarioKey.Desertor, StepKey.TOFU_Response)] =
            "Te entendemos, la rutina a veces gana. Pero recuerda por qué empezaste. ✨\n\n" +
            "Para ayudarte a retomar, mañana tenemos una *Clase Especial de [Yoga/Box]* a las 7:00 PM para liberar el estrés. " +
            "Si vienes, te regalamos un *Smoothie de Proteína* al terminar. 🥤\n\n" +
            "¿Te reservo un lugar?",

        [(ScenarioKey.Desertor, StepKey.MOFU_Offer)] =
            "¡Esa es la actitud! 💪 El primer paso es el más difícil.\n\n" +
            "Te esperamos mañana a las 7:00 PM. ¡Tu smoothie ya está en la lista! 🥤✅",

        [(ScenarioKey.Desertor, StepKey.BOFU_Confirm)] =
            "¡Confirmado! Te vemos mañana. 🎉\n\n" +
            "Recuerda: solo necesitas ropa cómoda y ganas de volver. ¡El gym te extrañó! 💙",

        [(ScenarioKey.Desertor, StepKey.Fidelizacion_Day1)] =
            "¡Qué bueno verte de vuelta ayer, {nombre}! ⚡\n\n" +
            "Para que no se te complique por el trabajo, ¿te gustaría que te mandemos una " +
            "*rutina express de 20 min* para esos días pesados? Así no pierdes el hábito. 💪",
    };

    // ────────────────────────────────────────────────────────────────────────
    // Construcción del diccionario de mensajes proactivos
    // ────────────────────────────────────────────────────────────────────────

    private static Dictionary<TriggerType, string> BuildProactiveMessageDictionary() => new()
    {
        [TriggerType.Inactivity15Days] =
            "¡Hola {nombre}! Notamos un vacío en el área de pesas... 🏋️‍♀️\n\n" +
            "Llevas {dias} días sin registrar tu entrada. ¿Todo bien? Te extrañamos en la comunidad. 💙",

        [TriggerType.PostFirstClass24h] =
            "¡Felicidades por tu primer entrenamiento, {nombre}! 🎊\n\n" +
            "¿Cómo te sientes? Es normal estar un poco adolorido/a, ¡es señal de progreso! 💪\n" +
            "Nos vemos pasado mañana para tu segunda sesión. ¡Tú puedes!",

        [TriggerType.AbandonedForm2h] =
            "¡Hola {nombre}! Vi que estabas viendo el plan anual pero no terminaste tu registro. 📝\n\n" +
            "¿Tuviste algún problema con el pago o tienes alguna duda? ¡Estoy aquí para ayudarte!",

        [TriggerType.Milestone] =
            "¡BRUTAL, {nombre}! 🔥 Acabas de completar tu entrenamiento número {numero} con nosotros.\n\n" +
            "Eres parte del 5% más disciplinado. Pasa por recepción por tu recompensa. ¡A por el siguiente hito!",

        [TriggerType.MonthlyReport] =
            "¡Es día de pesaje, {nombre}! 📉\n\n" +
            "Mañana te toca tu evaluación corporal para ver cuánta grasa has perdido y cuánto músculo ganaste. " +
            "¿A qué hora pasas a la oficina del Coach?",
    };

    // ────────────────────────────────────────────────────────────────────────
    // Interpolación de variables
    // ────────────────────────────────────────────────────────────────────────

    private static string InterpolateVariables(string template, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
            template = template.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);

        return template;
    }
}
