# Design Document: WhatsApp Voice Integration

## Overview

Esta feature implementa el puente de integración entre WhatsApp (vía Twilio) y el `GymConversationRouter` existente. El sistema expone un endpoint HTTP webhook que recibe notificaciones de Twilio, transcribe notas de voz localmente con Vosk cuando el mensaje es audio, y envía la respuesta del bot de vuelta al usuario a través del SDK de Twilio.

El diseño se apoya en las abstracciones ya existentes (`IChannelAdapter`, `IGymConversationRouter`) y añade los componentes mínimos necesarios para conectar el canal WhatsApp con la lógica de conversación del gimnasio. La migración de `Host.CreateApplicationBuilder` a `WebApplication.CreateBuilder` es un prerequisito para exponer el endpoint HTTP.

### Hallazgos de Investigación

- **Vosk NuGet**: El paquete `Vosk` (v0.3.x, alphacep) es compatible con .NET Standard 2.0+ y por tanto con .NET 8. Expone `VoskModel` (carga el modelo desde disco) y `VoskRecognizer` (acepta PCM 16kHz mono como `short[]`). El modelo se carga una sola vez → singleton.
- **Conversión de audio**: WhatsApp envía notas de voz en formato `audio/ogg` (codec Opus). Vosk requiere PCM 16kHz mono. Se usará `NAudio` + `Concentus` (Opus decoder para .NET) para la conversión en memoria, evitando dependencias de sistema como `ffmpeg`.
- **Twilio SDK**: `Twilio` NuGet (v7.x). El envío de mensajes WhatsApp usa `MessageResource.CreateAsync` con `From = "whatsapp:+NUMERO"` y `To = userId` (que ya llega con el prefijo `whatsapp:` desde el webhook). La inicialización del cliente se hace con `TwilioClient.Init(accountSid, authToken)`.
- **Twilio Webhook**: Twilio envía `application/x-www-form-urlencoded` con campos `From`, `Body`, `NumMedia`, `MediaUrl0`, `MediaContentType0`. Requiere respuesta HTTP 200 para no activar reintentos automáticos.

---

## Architecture

El flujo de un mensaje entrante sigue este camino:

```
WhatsApp User
     │
     ▼ POST /api/whatsapp/webhook (form-urlencoded)
┌─────────────────────────┐
│   WhatsAppController    │
│  (ASP.NET Core MVC)     │
└────────────┬────────────┘
             │
     ┌───────┴────────┐
     │                │
  [texto]          [audio]
     │                │
     │    ┌───────────▼──────────────┐
     │    │  IAudioTranscriptionService │
     │    │  (VoskTranscriptionService) │
     │    └───────────┬──────────────┘
     │                │ texto transcrito
     └───────┬────────┘
             │
             ▼
┌────────────────────────────┐
│   IGymConversationRouter   │
│   (GymConversationRouter)  │
└────────────┬───────────────┘
             │ BotResponse
             ▼
┌────────────────────────────┐
│     IChannelAdapter        │
│  (TwilioWhatsAppAdapter)   │
└────────────────────────────┘
             │
             ▼ Twilio REST API
        WhatsApp User
```

### Decisiones de Diseño

1. **`WhatsAppController` no conoce `TwilioSettings` directamente**: Las credenciales de Twilio solo son accesibles desde `TwilioWhatsAppAdapter` y `VoskTranscriptionService`. El controlador solo inyecta `IGymConversationRouter`, `IChannelAdapter` e `IAudioTranscriptionService`.

2. **`VoskTranscriptionService` como singleton**: El modelo Vosk es costoso de cargar (varios segundos, cientos de MB). Se carga una vez en el constructor y se reutiliza. `VoskRecognizer` se crea por llamada a `TranscribeAsync` para evitar estado compartido entre requests concurrentes.

3. **Conversión de audio en memoria**: Se usa `NAudio` + `Concentus.OggFile` para decodificar Ogg/Opus a PCM 16kHz mono sin escribir archivos intermedios de audio convertido. Solo el archivo descargado de Twilio se escribe a disco temporalmente.

4. **HTTP 200 siempre**: El controlador captura todas las excepciones y siempre retorna 200 con un body TwiML vacío (`<Response/>`). Esto previene que Twilio reintente el webhook y genere mensajes duplicados.

5. **`TwilioWhatsAppAdapter` como scoped**: A diferencia del modelo Vosk, la inicialización de Twilio es barata. Scoped es apropiado para un adaptador de canal que puede necesitar configuración por-request en el futuro.

---

## Components and Interfaces

### 1. `TwilioSettings`

```csharp
namespace Chatbot.Models;

public class TwilioSettings
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string WhatsAppNumber { get; set; } = string.Empty;
}
```

Ubicación: `Chatbot/Models/TwilioSettings.cs`

### 2. `VoskSettings`

```csharp
namespace Chatbot.Models;

public class VoskSettings
{
    public string ModelPath { get; set; } = string.Empty;
}
```

Ubicación: `Chatbot/Models/VoskSettings.cs`

### 3. `TwilioWebhookPayload`

Modelo que mapea el payload `application/x-www-form-urlencoded` de Twilio. Usa `[FromForm]` en el controlador.

```csharp
namespace Chatbot.Models;

public class TwilioWebhookPayload
{
    [FromForm(Name = "From")]
    public string From { get; set; } = string.Empty;

    [FromForm(Name = "Body")]
    public string Body { get; set; } = string.Empty;

    [FromForm(Name = "NumMedia")]
    public int NumMedia { get; set; }

    [FromForm(Name = "MediaUrl0")]
    public string? MediaUrl0 { get; set; }

    [FromForm(Name = "MediaContentType0")]
    public string? MediaContentType0 { get; set; }
}
```

Ubicación: `Chatbot/Models/TwilioWebhookPayload.cs`

### 4. `IAudioTranscriptionService`

```csharp
namespace Chatbot.Services;

public interface IAudioTranscriptionService
{
    /// <summary>
    /// Descarga el audio desde audioUrl usando las credenciales proporcionadas
    /// y lo transcribe localmente. Retorna el texto transcrito (puede ser vacío).
    /// Lanza InvalidOperationException si el audio no puede descargarse.
    /// </summary>
    Task<string> TranscribeAsync(string audioUrl, string authUser, string authPassword);
}
```

Ubicación: `Chatbot/Services/IAudioTranscriptionService.cs`

### 5. `VoskTranscriptionService`

Implementación de `IAudioTranscriptionService`. Responsabilidades:

- Descargar el archivo de audio desde la URL de Twilio con autenticación HTTP Basic.
- Guardar el archivo en un path temporal (`Path.GetTempFileName()`).
- Decodificar Ogg/Opus a PCM 16kHz mono usando `NAudio` + `Concentus.OggFile`.
- Pasar el PCM a `VoskRecognizer.AcceptWaveform(short[], length)`.
- Extraer el texto del resultado JSON de Vosk (`result.text`).
- Eliminar el archivo temporal en un bloque `finally`.

```csharp
namespace Chatbot.Services;

public sealed class VoskTranscriptionService : IAudioTranscriptionService, IDisposable
{
    private readonly VoskModel _model;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VoskTranscriptionService> _logger;

    public VoskTranscriptionService(
        IOptions<VoskSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<VoskTranscriptionService> logger);

    public async Task<string> TranscribeAsync(
        string audioUrl, string authUser, string authPassword);

    public void Dispose();
}
```

Ubicación: `Chatbot/Services/VoskTranscriptionService.cs`

### 6. `TwilioWhatsAppAdapter`

Implementación de `IChannelAdapter`. Responsabilidades:

- Inicializar el cliente Twilio con `TwilioClient.Init(accountSid, authToken)`.
- Llamar a `MessageResource.CreateAsync` con `From = "whatsapp:{WhatsAppNumber}"` y `To = userId`.
- Loguear `userId` y longitud del mensaje en `Debug` antes de cada envío.
- Propagar excepciones al caller sin capturarlas.

```csharp
namespace Chatbot.Services.Gym;

public sealed class TwilioWhatsAppAdapter : IChannelAdapter
{
    private readonly TwilioSettings _settings;
    private readonly ILogger<TwilioWhatsAppAdapter> _logger;

    public TwilioWhatsAppAdapter(
        IOptions<TwilioSettings> settings,
        ILogger<TwilioWhatsAppAdapter> logger);

    public async Task SendMessageAsync(string userId, string message);
}
```

Ubicación: `Chatbot/Services/Gym/TwilioWhatsAppAdapter.cs`

### 7. `WhatsAppController`

Controlador ASP.NET Core MVC. Responsabilidades:

- Recibir el payload de Twilio en `POST /api/whatsapp/webhook`.
- Determinar si el mensaje es texto o audio (basado en `NumMedia` y `MediaContentType0`).
- Delegar transcripción a `IAudioTranscriptionService` si es audio.
- Usar `"[audio no reconocido]"` como fallback si la transcripción falla o retorna vacío.
- Llamar a `IGymConversationRouter.RouteMessageAsync`.
- Llamar a `IChannelAdapter.SendMessageAsync` con la respuesta del bot.
- Siempre retornar HTTP 200 con `Content-Type: text/xml` y body `<Response/>`.

```csharp
namespace Chatbot.Controllers;

[ApiController]
[Route("api/whatsapp")]
public sealed class WhatsAppController : ControllerBase
{
    private readonly IGymConversationRouter _router;
    private readonly IChannelAdapter _channelAdapter;
    private readonly IAudioTranscriptionService _transcriptionService;
    private readonly IOptions<TwilioSettings> _twilioSettings;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        IGymConversationRouter router,
        IChannelAdapter channelAdapter,
        IAudioTranscriptionService transcriptionService,
        IOptions<TwilioSettings> twilioSettings,
        ILogger<WhatsAppController> logger);

    [HttpPost("webhook")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ReceiveWebhook([FromForm] TwilioWebhookPayload payload);
}
```

> **Nota**: El controlador recibe `IOptions<TwilioSettings>` únicamente para extraer las credenciales necesarias para autenticar la descarga del audio desde Twilio (pasadas a `IAudioTranscriptionService.TranscribeAsync`). No usa las credenciales para enviar mensajes — eso es responsabilidad exclusiva de `TwilioWhatsAppAdapter`.

Ubicación: `Chatbot/Controllers/WhatsAppController.cs`

---

## Data Models

### Flujo de datos: mensaje de texto

```
TwilioWebhookPayload.From  →  userId  →  RouteMessageAsync(userId, body)
TwilioWebhookPayload.Body  →  text    ↗
                                        BotResponse.Message  →  SendMessageAsync(userId, message)
```

### Flujo de datos: nota de voz

```
TwilioWebhookPayload.From          →  userId
TwilioWebhookPayload.MediaUrl0     →  audioUrl  →  TranscribeAsync(audioUrl, sid, token)
TwilioWebhookPayload.NumMedia > 0  →  isAudio                    ↓
                                                          transcribedText
                                                                  ↓
                                              RouteMessageAsync(userId, transcribedText)
                                                                  ↓
                                                          BotResponse.Message
                                                                  ↓
                                              SendMessageAsync(userId, message)
```

### Estructura de configuración (`appsettings.json`)

```json
{
  "Twilio": {
    "AccountSid": "...",
    "AuthToken": "...",
    "WhatsAppNumber": "+XXXXXXXXXXX"
  },
  "Vosk": {
    "ModelPath": "/path/to/vosk-model"
  }
}
```

### Resultado JSON de Vosk

Vosk retorna un JSON con la forma `{"text": "texto transcrito"}`. El servicio extrae el campo `text` usando `System.Text.Json`.

---

## Correctness Properties

*Una propiedad es una característica o comportamiento que debe ser verdadero en todas las ejecuciones válidas del sistema — esencialmente, una declaración formal sobre lo que el sistema debe hacer. Las propiedades sirven como puente entre las especificaciones legibles por humanos y las garantías de corrección verificables por máquinas.*

### Property 1: El webhook siempre retorna HTTP 200

*Para cualquier* payload entrante al endpoint `/api/whatsapp/webhook` — incluyendo payloads válidos, malformados, con campos vacíos, o que causen excepciones internas — el controlador SHALL retornar siempre HTTP 200.

**Validates: Requirements 2.7, 7.1, 2.8, 7.3, 7.5**

---

### Property 2: Routing correcto según tipo de mensaje

*Para cualquier* payload de Twilio, el controlador SHALL llamar a `IAudioTranscriptionService.TranscribeAsync` si y solo si `NumMedia > 0` Y `MediaContentType0` comienza con `"audio/"`. En todos los demás casos, SHALL usar `Body` directamente sin llamar al servicio de transcripción.

**Validates: Requirements 2.3, 2.4**

---

### Property 3: El router siempre recibe el userId y texto resuelto

*Para cualquier* payload válido (con `From` no vacío), `IGymConversationRouter.RouteMessageAsync` SHALL ser llamado exactamente una vez con el `From` del payload como `userId` y el texto resuelto (transcripción o `Body`) como `incomingMessage`.

**Validates: Requirements 2.2, 2.5**

---

### Property 4: La respuesta del bot siempre se envía al usuario correcto

*Para cualquier* `BotResponse` retornada por `IGymConversationRouter.RouteMessageAsync`, `IChannelAdapter.SendMessageAsync` SHALL ser llamado con el mismo `userId` del payload original y el `BotResponse.Message` como contenido.

**Validates: Requirements 2.6**

---

### Property 5: Limpieza de archivos temporales

*Para cualquier* llamada a `VoskTranscriptionService.TranscribeAsync` — ya sea que la transcripción tenga éxito o lance una excepción — ningún archivo temporal creado durante el procesamiento SHALL persistir en el sistema de archivos después de que el método retorne.

**Validates: Requirements 3.8**

---

### Property 6: Formato correcto de números en Twilio

*Para cualquier* `userId` y `WhatsAppNumber` configurado, cuando `TwilioWhatsAppAdapter.SendMessageAsync` envía un mensaje, el campo `From` SHALL tener el formato `"whatsapp:{WhatsAppNumber}"` y el campo `To` SHALL ser el `userId` tal como se recibió (que ya incluye el prefijo `whatsapp:`).

**Validates: Requirements 4.4**

---

## Error Handling

### Estrategia general

El sistema sigue el principio de **fail-safe hacia Twilio**: cualquier error interno se absorbe en el controlador y se retorna HTTP 200. Los errores se loguean con suficiente contexto para diagnóstico.

### Tabla de errores y manejo

| Escenario | Componente | Manejo |
|-----------|-----------|--------|
| `From` vacío o nulo | `WhatsAppController` | Log Warning, retornar HTTP 200 sin routing |
| `TranscribeAsync` lanza excepción | `WhatsAppController` | Log Error, usar `"[audio no reconocido]"`, continuar routing |
| Transcripción retorna vacío/whitespace | `WhatsAppController` | Usar `"[audio no reconocido]"`, continuar routing |
| `RouteMessageAsync` lanza excepción | `WhatsAppController` | Log Error, retornar HTTP 200 |
| `SendMessageAsync` lanza excepción | `WhatsAppController` | Log Error nivel `Error`, retornar HTTP 200 |
| Audio no descargable | `VoskTranscriptionService` | Lanzar `InvalidOperationException` con mensaje descriptivo |
| Modelo Vosk no encontrado | `VoskTranscriptionService` | Lanzar `InvalidOperationException` en constructor (falla al inicio) |
| Twilio API falla | `TwilioWhatsAppAdapter` | Propagar excepción al caller |

### Logging

| Nivel | Evento |
|-------|--------|
| `Information` | Webhook recibido: userId + tipo de mensaje (texto/audio) |
| `Debug` | Antes de enviar mensaje: userId + longitud del mensaje |
| `Warning` | `From` vacío o nulo en payload |
| `Error` | Excepción en transcripción, routing o envío |

---

## Testing Strategy

### Enfoque dual

Se combinan **tests unitarios con mocks** para lógica de negocio y **tests de integración** para verificar el ensamblaje de componentes.

### Tests unitarios (xUnit + Moq)

**`WhatsAppControllerTests`**:
- Verificar que HTTP 200 se retorna siempre (incluyendo casos de excepción).
- Verificar routing correcto: texto vs audio según `NumMedia` y `MediaContentType0`.
- Verificar que `RouteMessageAsync` recibe el `userId` y texto correctos.
- Verificar que `SendMessageAsync` recibe el `userId` y mensaje del bot.
- Verificar fallback `"[audio no reconocido]"` cuando transcripción falla o retorna vacío.
- Verificar que `From` vacío no llega al router.

**`TwilioWhatsAppAdapterTests`**:
- Verificar formato `whatsapp:` en `From` y `To`.
- Verificar que las credenciales se leen de `TwilioSettings`.
- Verificar que excepciones de Twilio se propagan.

**`VoskTranscriptionServiceTests`**:
- Verificar que archivos temporales se eliminan tras transcripción exitosa.
- Verificar que archivos temporales se eliminan cuando la descarga falla.
- Verificar que `InvalidOperationException` se lanza con URL inaccesible.

### Tests de propiedad (property-based tests con FsCheck o CsCheck)

Se usa **CsCheck** (NuGet `CsCheck`, compatible con xUnit y .NET 8) con mínimo 100 iteraciones por propiedad.

Cada test de propiedad referencia su propiedad del diseño con el comentario:
`// Feature: whatsapp-voice-integration, Property N: <texto de la propiedad>`

**Property 1 — HTTP 200 invariante**:
Generar payloads aleatorios (incluyendo campos vacíos, nulos, valores extremos). Configurar mocks para lanzar excepciones aleatoriamente. Verificar que la respuesta siempre es HTTP 200.

**Property 2 — Routing texto vs audio**:
Generar payloads con `NumMedia` aleatorio (0, 1, 2+) y `MediaContentType0` aleatorio (audio/*, image/*, text/*, null). Verificar que `TranscribeAsync` se llama si y solo si `NumMedia > 0` AND `MediaContentType0.StartsWith("audio/")`.

**Property 3 — Router recibe userId y texto correctos**:
Generar pares `(From, Body)` aleatorios con `From` no vacío. Verificar que `RouteMessageAsync` es llamado con exactamente esos valores (o la transcripción en caso de audio).

**Property 4 — Respuesta enviada al usuario correcto**:
Generar `BotResponse` con mensajes aleatorios. Verificar que `SendMessageAsync` recibe el mismo `userId` y el `Message` del `BotResponse`.

**Property 5 — Limpieza de archivos temporales**:
Generar URLs de audio aleatorias (válidas e inválidas). Verificar que después de cada llamada a `TranscribeAsync` (éxito o excepción), el directorio temporal no contiene archivos creados por el servicio.

**Property 6 — Formato de números Twilio**:
Generar pares `(userId, whatsAppNumber)` aleatorios. Verificar que el mock de Twilio recibe `From = "whatsapp:" + whatsAppNumber` y `To = userId`.

### Tests de integración

- Verificar que el endpoint `POST /api/whatsapp/webhook` responde con HTTP 200 usando `WebApplicationFactory<Program>`.
- Verificar que el DI container resuelve correctamente todos los servicios registrados.
- Verificar (con un modelo Vosk real y un archivo de audio de prueba) que la transcripción produce un string no nulo.

### Paquetes NuGet de test

```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="CsCheck" Version="3.9.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
```
