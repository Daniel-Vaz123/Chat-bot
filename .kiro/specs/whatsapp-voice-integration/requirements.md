# Requirements Document

## Introduction

Esta feature implementa el puente de integración entre WhatsApp (vía Twilio) y el `GymConversationRouter` existente, habilitando la recepción y procesamiento de mensajes de texto y notas de voz en el chatbot de gimnasio. El sistema expone un endpoint webhook que recibe notificaciones de Twilio, transcribe audio localmente con Vosk cuando el mensaje es una nota de voz, y envía la respuesta del bot de vuelta al usuario a través del SDK de Twilio. Las credenciales se gestionan exclusivamente mediante `IConfiguration` — ningún token se hardcodea en el código fuente.

## Glossary

- **WhatsAppController**: Controlador ASP.NET Core que expone el endpoint POST `/api/whatsapp/webhook` para recibir notificaciones de Twilio.
- **TwilioWhatsAppAdapter**: Implementación de `IChannelAdapter` que envía mensajes de respuesta al usuario a través del SDK de Twilio.
- **IAudioTranscriptionService**: Interfaz que abstrae la transcripción de audio a texto.
- **VoskTranscriptionService**: Implementación de `IAudioTranscriptionService` que descarga el audio desde la URL de Twilio y lo procesa localmente con el modelo Vosk.
- **TwilioWebhookPayload**: Modelo que representa el payload de una notificación entrante de Twilio (campos `From`, `Body`, `NumMedia`, `MediaUrl0`, `MediaContentType0`).
- **TwilioSettings**: Clase de configuración con propiedades `AccountSid`, `AuthToken` y `WhatsAppNumber`, leídas desde `IConfiguration`.
- **VoskSettings**: Clase de configuración con la ruta al modelo Vosk (`ModelPath`), leída desde `IConfiguration`.
- **Twilio SDK**: Biblioteca oficial `Twilio` para .NET usada para enviar mensajes de WhatsApp salientes.
- **Vosk**: Motor de reconocimiento de voz offline de código abierto usado para transcribir notas de voz.
- **IChannelAdapter**: Interfaz existente en el proyecto con el método `SendMessageAsync(string userId, string message)`.
- **IGymConversationRouter**: Interfaz existente con el método `RouteMessageAsync(string userId, string incomingMessage)`.
- **NumMedia**: Campo del payload de Twilio que indica el número de archivos multimedia adjuntos al mensaje.
- **MediaUrl0**: Campo del payload de Twilio con la URL del primer archivo multimedia adjunto.
- **MediaContentType0**: Campo del payload de Twilio con el tipo MIME del primer archivo multimedia (ej. `audio/ogg`).

---

## Requirements

### Requirement 1: Archivo de Configuración de Ejemplo

**User Story:** As a developer, I want an example configuration file at the project root, so that I can understand the required settings structure without exposing real credentials.

#### Acceptance Criteria

1. THE `Chatbot` project SHALL include an `appsettings.example.json` file at the project root with the complete configuration structure for Twilio, Vosk, Logging, and existing AWS/Bedrock sections.
2. THE `appsettings.example.json` SHALL contain a `Twilio` section with placeholder values for `AccountSid`, `AuthToken`, and `WhatsAppNumber`.
3. THE `appsettings.example.json` SHALL contain a `Vosk` section with a placeholder value for `ModelPath`.
4. THE `appsettings.example.json` SHALL contain a standard `Logging` section with `LogLevel` defaults.
5. THE `appsettings.example.json` SHALL NOT contain real credentials, tokens, or secrets — only placeholder strings such as `"YOUR_ACCOUNT_SID_HERE"`.

---

### Requirement 2: Endpoint Webhook de WhatsApp

**User Story:** As a gym member, I want to send WhatsApp messages to the gym chatbot, so that I can interact with it through my preferred messaging app.

#### Acceptance Criteria

1. THE `WhatsAppController` SHALL expose a POST endpoint at the route `/api/whatsapp/webhook` that accepts `application/x-www-form-urlencoded` payloads from Twilio.
2. WHEN a Twilio webhook notification is received, THE `WhatsAppController` SHALL extract the `From` field as the `userId` and the `Body` field as the incoming text message.
3. WHEN the `NumMedia` field in the payload is greater than zero and `MediaContentType0` indicates an audio type, THE `WhatsAppController` SHALL treat the message as a voice note and delegate transcription to `IAudioTranscriptionService`.
4. WHEN the `NumMedia` field is zero or absent, THE `WhatsAppController` SHALL treat the `Body` field as the text message directly.
5. WHEN the incoming message (text or transcribed audio) is processed, THE `WhatsAppController` SHALL call `IGymConversationRouter.RouteMessageAsync` with the `userId` and the resolved text.
6. WHEN `IGymConversationRouter.RouteMessageAsync` returns a `BotResponse`, THE `WhatsAppController` SHALL delegate sending the response to `TwilioWhatsAppAdapter.SendMessageAsync`.
7. THE `WhatsAppController` SHALL return HTTP 200 with an empty TwiML response body to Twilio after processing, regardless of whether the bot response was sent successfully.
8. IF `IGymConversationRouter.RouteMessageAsync` throws an unhandled exception, THEN THE `WhatsAppController` SHALL log the error and still return HTTP 200 to Twilio to prevent retry storms.

---

### Requirement 3: Transcripción de Audio con Vosk

**User Story:** As a gym member, I want to send voice notes to the chatbot, so that I can interact hands-free without typing.

#### Acceptance Criteria

1. THE `IAudioTranscriptionService` SHALL define a method `TranscribeAsync(string audioUrl, string authUser, string authPassword)` that returns a `string` with the transcribed text.
2. WHEN `VoskTranscriptionService.TranscribeAsync` is called with a valid audio URL, THE `VoskTranscriptionService` SHALL download the audio file using HTTP with the provided Twilio credentials for authentication.
3. WHEN the audio file is downloaded, THE `VoskTranscriptionService` SHALL process it locally using the Vosk model loaded from the path specified in `VoskSettings.ModelPath`.
4. WHEN transcription completes successfully, THE `VoskTranscriptionService` SHALL return the transcribed text as a non-null string.
5. IF the audio file cannot be downloaded, THEN THE `VoskTranscriptionService` SHALL throw an `InvalidOperationException` with a descriptive message.
6. IF the Vosk model path does not exist or the model cannot be loaded, THEN THE `VoskTranscriptionService` SHALL throw an `InvalidOperationException` with a descriptive message at service initialization.
7. WHEN transcription produces an empty or whitespace result, THE `VoskTranscriptionService` SHALL return an empty string, and THE `WhatsAppController` SHALL use a fallback message such as `"[audio no reconocido]"` before routing.
8. THE `VoskTranscriptionService` SHALL delete any temporary audio files created during processing after transcription completes, whether successful or not.

---

### Requirement 4: Adaptador de Canal Twilio (TwilioWhatsAppAdapter)

**User Story:** As a gym operator, I want the chatbot responses to be delivered to users via WhatsApp, so that the conversation loop is complete.

#### Acceptance Criteria

1. THE `TwilioWhatsAppAdapter` SHALL implement the existing `IChannelAdapter` interface.
2. WHEN `TwilioWhatsAppAdapter.SendMessageAsync` is called with a `userId` and `message`, THE `TwilioWhatsAppAdapter` SHALL send the message to the `userId` phone number using the Twilio REST API via the official Twilio .NET SDK.
3. THE `TwilioWhatsAppAdapter` SHALL read `AccountSid`, `AuthToken`, and `WhatsAppNumber` exclusively from `TwilioSettings` injected via `IConfiguration` — no credentials SHALL be hardcoded.
4. THE `TwilioWhatsAppAdapter` SHALL format the `From` number as `whatsapp:{TwilioSettings.WhatsAppNumber}` and the `To` number as the `userId` value received from Twilio (which already includes the `whatsapp:` prefix).
5. IF the Twilio API call fails, THEN THE `TwilioWhatsAppAdapter` SHALL throw the exception so the caller (`WhatsAppController`) can log it and handle it appropriately.
6. THE `TwilioWhatsAppAdapter` SHALL log the `userId` and message length (not the message content) at `Debug` level before each send attempt.

---

### Requirement 5: Gestión Segura de Credenciales

**User Story:** As a security-conscious developer, I want all credentials to be read from configuration, so that no secrets are ever committed to source control.

#### Acceptance Criteria

1. THE `TwilioSettings` class SHALL be populated exclusively via `IConfiguration` binding — no default values SHALL contain real credentials.
2. THE `VoskSettings` class SHALL be populated exclusively via `IConfiguration` binding.
3. THE `WhatsAppController` SHALL NOT have direct access to `TwilioSettings` — it SHALL interact with Twilio only through `TwilioWhatsAppAdapter` and `IAudioTranscriptionService`.
4. THE `appsettings.json` and `appsettings.Development.json` files SHALL NOT contain real Twilio credentials — only `appsettings.example.json` SHALL document the expected structure with placeholder values.
5. THE `.gitignore` SHALL include entries to prevent accidental commit of files containing real credentials (e.g., `appsettings.Production.json` if it contains secrets).

---

### Requirement 6: Registro en Contenedor de Dependencias

**User Story:** As a developer, I want all new services to be registered in the DI container, so that they are available throughout the application lifecycle.

#### Acceptance Criteria

1. THE `Program.cs` SHALL register `TwilioSettings` and `VoskSettings` by binding them from `IConfiguration` using `services.Configure<T>`.
2. THE `Program.cs` SHALL register `IAudioTranscriptionService` with `VoskTranscriptionService` as its implementation, using the appropriate lifetime (singleton, as the Vosk model is loaded once).
3. THE `Program.cs` SHALL register `IChannelAdapter` with `TwilioWhatsAppAdapter` as its implementation, using scoped lifetime.
4. THE `Program.cs` SHALL add ASP.NET Core MVC controllers (`builder.Services.AddControllers()`) and map controller routes (`app.MapControllers()`).
5. THE `Program.cs` SHALL configure the application to use `WebApplication.CreateBuilder` instead of `Host.CreateApplicationBuilder` to support HTTP endpoint exposure.

---

### Requirement 7: Manejo de Errores y Resiliencia del Webhook

**User Story:** As a gym operator, I want the webhook to be resilient to transient errors, so that Twilio does not retry requests unnecessarily and flood the system.

#### Acceptance Criteria

1. THE `WhatsAppController` SHALL always return HTTP 200 to Twilio, even when internal processing fails, to prevent Twilio's automatic retry mechanism from sending duplicate messages.
2. IF `IAudioTranscriptionService.TranscribeAsync` throws an exception, THEN THE `WhatsAppController` SHALL log the error, use `"[audio no reconocido]"` as the fallback message, and continue routing to `IGymConversationRouter`.
3. IF `TwilioWhatsAppAdapter.SendMessageAsync` throws an exception, THEN THE `WhatsAppController` SHALL log the error at `Error` level and return HTTP 200 without re-throwing.
4. THE `WhatsAppController` SHALL log the `userId` and message type (text or audio) at `Information` level for each incoming webhook request.
5. IF the `From` field in the Twilio payload is null or empty, THEN THE `WhatsAppController` SHALL log a warning and return HTTP 200 without routing the message.
