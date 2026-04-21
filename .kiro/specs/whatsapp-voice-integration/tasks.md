# Plan de Implementación: WhatsApp Voice Integration

## Overview

Implementación del puente de integración entre WhatsApp (vía Twilio) y el `GymConversationRouter` existente. El plan migra el host de `Host.CreateApplicationBuilder` a `WebApplication.CreateBuilder`, añade los modelos de configuración, el servicio de transcripción Vosk, el adaptador de canal Twilio, el controlador webhook y los tests correspondientes.

## Tasks

- [x] 1. Migrar el host a WebApplication y añadir paquetes NuGet
  - Reemplazar `Host.CreateApplicationBuilder` por `WebApplication.CreateBuilder` en `Chatbot/Program.cs`
  - Añadir `builder.Services.AddControllers()` y `app.MapControllers()` en `Program.cs`
  - Añadir los paquetes NuGet al `Chatbot/Chatbot.csproj`: `Twilio` (v7.x), `Vosk` (v0.3.x), `NAudio` (v2.x), `Concentus` (v2.x), `Concentus.OggFile` (v2.x)
  - Añadir el proyecto de tests `Chatbot.Tests` (xUnit) con los paquetes: `xunit`, `xunit.runner.visualstudio`, `Moq`, `CsCheck`, `Microsoft.AspNetCore.Mvc.Testing`
  - _Requirements: 6.4, 6.5_

- [x] 2. Crear modelos de configuración y payload
  - [x] 2.1 Crear `Chatbot/Models/TwilioSettings.cs` con propiedades `AccountSid`, `AuthToken` y `WhatsAppNumber`
    - Sin valores por defecto que contengan credenciales reales
    - _Requirements: 5.1_
  - [x] 2.2 Crear `Chatbot/Models/VoskSettings.cs` con propiedad `ModelPath`
    - Sin valores por defecto que contengan rutas reales
    - _Requirements: 5.2_
  - [x] 2.3 Crear `Chatbot/Models/TwilioWebhookPayload.cs` con propiedades `From`, `Body`, `NumMedia`, `MediaUrl0`, `MediaContentType0`
    - Usar atributos `[FromForm(Name = "...")]` para mapear los campos del payload `application/x-www-form-urlencoded`
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 3. Crear archivo de configuración de ejemplo
  - Crear `Chatbot/appsettings.example.json` con secciones `Twilio`, `Vosk`, `Logging`, `AWS`, `S3Vectors` y `Bedrock`
  - Usar únicamente valores placeholder como `"YOUR_ACCOUNT_SID_HERE"` — ningún valor real
  - Añadir la entrada `<None Update="appsettings.example.json">` en `Chatbot.csproj` para que se copie al output
  - Verificar que `appsettings.json` y `appsettings.Development.json` no contienen credenciales Twilio reales
  - Verificar que `.gitignore` incluye `appsettings.Production.json`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.4, 5.5_

- [x] 4. Implementar `IAudioTranscriptionService` y `VoskTranscriptionService`
  - [x] 4.1 Crear `Chatbot/Services/IAudioTranscriptionService.cs` con el método `TranscribeAsync(string audioUrl, string authUser, string authPassword)`
    - _Requirements: 3.1_
  - [x] 4.2 Crear `Chatbot/Services/VoskTranscriptionService.cs` implementando `IAudioTranscriptionService` y `IDisposable`
    - Constructor: cargar `VoskModel` desde `VoskSettings.ModelPath`; lanzar `InvalidOperationException` si la ruta no existe
    - `TranscribeAsync`: descargar el audio con HTTP Basic Auth, guardar en `Path.GetTempFileName()`, decodificar Ogg/Opus a PCM 16kHz mono con `NAudio` + `Concentus.OggFile`, pasar PCM a `VoskRecognizer`, extraer `result.text` del JSON de Vosk, eliminar el archivo temporal en bloque `finally`
    - Lanzar `InvalidOperationException` con mensaje descriptivo si el audio no puede descargarse
    - Retornar string vacío si la transcripción produce resultado vacío o whitespace
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_
  - [ ]* 4.3 Escribir tests unitarios para `VoskTranscriptionService`
    - Verificar que archivos temporales se eliminan tras transcripción exitosa
    - Verificar que archivos temporales se eliminan cuando la descarga falla
    - Verificar que `InvalidOperationException` se lanza con URL inaccesible
    - _Requirements: 3.5, 3.8_
  - [ ]* 4.4 Escribir property test para limpieza de archivos temporales
    - **Property 5: Limpieza de archivos temporales**
    - **Validates: Requirements 3.8**

- [x] 5. Checkpoint — Verificar compilación y tests de transcripción
  - Asegurarse de que el proyecto compila sin errores. Ejecutar los tests disponibles y preguntar al usuario si hay dudas.

- [x] 6. Implementar `TwilioWhatsAppAdapter`
  - [x] 6.1 Crear `Chatbot/Services/Gym/TwilioWhatsAppAdapter.cs` implementando `IChannelAdapter`
    - Constructor: inyectar `IOptions<TwilioSettings>` e `ILogger<TwilioWhatsAppAdapter>`
    - `SendMessageAsync`: inicializar `TwilioClient.Init(accountSid, authToken)`, llamar a `MessageResource.CreateAsync` con `From = "whatsapp:{WhatsAppNumber}"` y `To = userId`
    - Loguear `userId` y longitud del mensaje en `Debug` antes de cada envío
    - Propagar excepciones al caller sin capturarlas
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_
  - [ ]* 6.2 Escribir tests unitarios para `TwilioWhatsAppAdapter`
    - Verificar formato `whatsapp:` en `From` y `To`
    - Verificar que las credenciales se leen de `TwilioSettings`
    - Verificar que excepciones de Twilio se propagan
    - _Requirements: 4.3, 4.4, 4.5_
  - [ ]* 6.3 Escribir property test para formato de números Twilio
    - **Property 6: Formato correcto de números en Twilio**
    - **Validates: Requirements 4.4**

- [x] 7. Implementar `WhatsAppController`
  - [x] 7.1 Crear `Chatbot/Controllers/WhatsAppController.cs` con el endpoint `POST /api/whatsapp/webhook`
    - Inyectar `IGymConversationRouter`, `IChannelAdapter`, `IAudioTranscriptionService`, `IOptions<TwilioSettings>` e `ILogger<WhatsAppController>`
    - Loguear `userId` y tipo de mensaje (`Information`) al recibir cada webhook
    - Si `From` es nulo o vacío: loguear `Warning` y retornar HTTP 200 sin routing
    - Si `NumMedia > 0` y `MediaContentType0` comienza con `"audio/"`: llamar a `TranscribeAsync`; si falla o retorna vacío, usar `"[audio no reconocido]"`
    - Llamar a `IGymConversationRouter.RouteMessageAsync` con `userId` y texto resuelto
    - Llamar a `IChannelAdapter.SendMessageAsync` con `userId` y `BotResponse.Message`
    - Capturar todas las excepciones, loguear en `Error` y retornar siempre HTTP 200 con `Content-Type: text/xml` y body `<Response/>`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 7.1, 7.2, 7.3, 7.4, 7.5_
  - [ ]* 7.2 Escribir tests unitarios para `WhatsAppController`
    - Verificar que HTTP 200 se retorna siempre (incluyendo casos de excepción en router y adapter)
    - Verificar routing correcto: texto vs audio según `NumMedia` y `MediaContentType0`
    - Verificar que `RouteMessageAsync` recibe el `userId` y texto correctos
    - Verificar que `SendMessageAsync` recibe el `userId` y mensaje del bot
    - Verificar fallback `"[audio no reconocido]"` cuando transcripción falla o retorna vacío
    - Verificar que `From` vacío no llega al router
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 7.1, 7.2, 7.3, 7.5_
  - [ ]* 7.3 Escribir property test para invariante HTTP 200
    - **Property 1: El webhook siempre retorna HTTP 200**
    - **Validates: Requirements 2.7, 7.1, 2.8, 7.3, 7.5**
  - [ ]* 7.4 Escribir property test para routing correcto según tipo de mensaje
    - **Property 2: Routing correcto según tipo de mensaje**
    - **Validates: Requirements 2.3, 2.4**
  - [ ]* 7.5 Escribir property test para router recibe userId y texto resuelto
    - **Property 3: El router siempre recibe el userId y texto resuelto**
    - **Validates: Requirements 2.2, 2.5**
  - [ ]* 7.6 Escribir property test para respuesta enviada al usuario correcto
    - **Property 4: La respuesta del bot siempre se envía al usuario correcto**
    - **Validates: Requirements 2.6**

- [x] 8. Registrar servicios en el contenedor de dependencias
  - En `Chatbot/Program.cs`, añadir:
    - `services.Configure<TwilioSettings>(configuration.GetSection("Twilio"))`
    - `services.Configure<VoskSettings>(configuration.GetSection("Vosk"))`
    - `services.AddSingleton<IAudioTranscriptionService, VoskTranscriptionService>()`
    - `services.AddScoped<IChannelAdapter, TwilioWhatsAppAdapter>()`
    - `services.AddHttpClient()` (requerido por `VoskTranscriptionService`)
  - Verificar que `AddControllers()` y `MapControllers()` están presentes (añadidos en tarea 1)
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [ ] 9. Tests de integración
  - [ ]* 9.1 Escribir test de integración con `WebApplicationFactory<Program>`
    - Verificar que `POST /api/whatsapp/webhook` responde HTTP 200 con un payload mínimo válido
    - Verificar que el DI container resuelve correctamente todos los servicios registrados
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 10. Checkpoint final — Asegurarse de que todos los tests pasan
  - Ejecutar todos los tests del proyecto. Preguntar al usuario si hay dudas o ajustes antes de cerrar.

## Notes

- Las sub-tareas marcadas con `*` son opcionales y pueden omitirse para un MVP más rápido.
- Cada tarea referencia los requisitos específicos para trazabilidad.
- Los property tests usan **CsCheck** con mínimo 100 iteraciones por propiedad.
- Cada property test debe incluir el comentario: `// Feature: whatsapp-voice-integration, Property N: <texto>`
- El modelo Vosk se registra como **singleton** porque su carga es costosa (cientos de MB).
- `TwilioWhatsAppAdapter` se registra como **scoped** para permitir configuración por-request en el futuro.
- El controlador siempre retorna HTTP 200 para evitar reintentos automáticos de Twilio.
