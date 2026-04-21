# Plan de Implementación: gym-chatbot-conversational-flows

## Overview

Implementación incremental del módulo de chatbot conversacional para gimnasio en C# .NET 8. Las tareas siguen el orden de dependencias: modelos → interfaces → repositorio → motor de estados → router → handlers de escenarios → triggers → background service → registro DI → tests de integración.

## Tasks

- [x] 1. Crear modelos de dominio en `Chatbot/Models/Gym/`
  - [x] 1.1 Crear enumeraciones de dominio (`ScenarioKey`, `StepKey`, `FunnelStage`, `MembershipType`, `FitnessGoal`, `MilestoneType`, `TriggerType`) en `Chatbot/Models/Gym/Enums.cs`
    - Definir todos los valores de `ScenarioKey` (None=0 hasta GestionQuejas=16)
    - Definir `StepKey` con los pasos del embudo (Initial, TOFU_Question, TOFU_Response, MOFU_Offer, MOFU_Response, BOFU_Confirm, BOFU_Payment, Fidelizacion_Day1, Fidelizacion_Week2, Completed)
    - _Requirements: 3.1, 3.5_

  - [x] 1.2 Crear `UserProfile` en `Chatbot/Models/Gym/UserProfile.cs`
    - Incluir todos los campos del diseño: `UserId`, `Name`, `PhoneNumber`, `TipoMembresia`, `Objetivo`, `FechaUltimoCheckIn`, `FechaPrimeraClase`, `CreatedAt`, `LesionesPrevias`, `TotalClasesAsistidas`, `EtapaEmbudo`, `CurrentState`, `Metadata`
    - _Requirements: 2.1, 2.6_

  - [x] 1.3 Crear `ConversationState` en `Chatbot/Models/Gym/ConversationState.cs`
    - Incluir: `UserId`, `ActiveScenario`, `CurrentStep`, `FunnelStage`, `LastInteraction`, `ContextData`, `IsActive`
    - _Requirements: 3.1, 3.2, 10.1_

  - [x] 1.4 Crear `BotResponse` en `Chatbot/Models/Gym/BotResponse.cs`
    - Propiedades: `Message` (string), `IsError` (bool), `Metadata` (Dictionary<string,string>)
    - _Requirements: 1.4, 11.3_

  - [x] 1.5 Crear `HandlerResult` en `Chatbot/Models/Gym/HandlerResult.cs`
    - Propiedades: `Response` (BotResponse), `NextStep` (StepKey), `NextFunnelStage` (FunnelStage)
    - _Requirements: 3.3, 3.4_

- [x] 2. Definir interfaces de servicios en `Chatbot/Services/Gym/`
  - [x] 2.1 Crear `IGymConversationRouter` en `Chatbot/Services/Gym/IGymConversationRouter.cs`
    - Método: `Task<BotResponse> RouteMessageAsync(string userId, string incomingMessage)`
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 2.2 Crear `IGymStateEngine` en `Chatbot/Services/Gym/IGymStateEngine.cs`
    - Métodos: `ProcessMessageAsync`, `GetCurrentStateAsync`, `InitiateScenarioAsync`, `ResetStateAsync`
    - _Requirements: 3.1, 3.3, 3.7, 3.8_

  - [x] 2.3 Crear `IGymUserProfileRepository` en `Chatbot/Services/Gym/IGymUserProfileRepository.cs`
    - Métodos: `GetOrCreateProfileAsync`, `UpdateProfileAsync`, `GetInactiveUsersAsync`, `GetUsersWithMilestoneAsync`, `UpdateConversationStateAsync`, `GetConversationStateAsync`
    - _Requirements: 2.1, 2.3, 5.1_

  - [x] 2.4 Crear `IGymTriggerService` en `Chatbot/Services/Gym/IGymTriggerService.cs`
    - Métodos: `RunInactivityCheckAsync`, `RunPostFirstClassFollowUpAsync`, `RunMilestoneCheckAsync`, `RunAbandonedFormCheckAsync`, `RunMonthlyProgressReportAsync`
    - _Requirements: 5.1, 5.6_

  - [x] 2.5 Crear `INotificationResources` en `Chatbot/Services/Gym/INotificationResources.cs`
    - Métodos: `GetWelcomeMessage()`, `GetResponse(ScenarioKey, StepKey, Dictionary<string,string>?)`, `GetProactiveMessage(TriggerType, Dictionary<string,string>?)`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 2.6 Crear `IIntentHandler` en `Chatbot/Services/Gym/Handlers/IIntentHandler.cs`
    - Método: `Task<HandlerResult> HandleAsync(UserProfile profile, ConversationState state, string message)`
    - _Requirements: 3.3_

- [x] 3. Implementar `NotificationResources` en `Chatbot/Resources/NotificationResources.cs`
  - Implementar `INotificationResources` con un `Dictionary<(ScenarioKey, StepKey), string>` inicializado en el constructor
  - Incluir textos para los 3 escenarios iniciales: `PropositoAnoNuevo`, `AtletaEstancado`, `Desertor` (todos los pasos TOFU→BOFU)
  - Incluir `WelcomeMessage` con las 5 opciones numeradas
  - Incluir mensajes proactivos para `TriggerType.Inactivity15Days` y `TriggerType.PostFirstClass24h`
  - Implementar interpolación de `{key}` placeholders en `GetResponse` y `GetProactiveMessage`
  - Retornar mensaje de fallback (nunca lanzar excepción) para claves no registradas
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 9.2_

  - [ ]* 3.1 Escribir property test: GetResponse nunca lanza excepción para claves registradas
    - **Property 9: GetResponse nunca lanza excepción para claves registradas**
    - **Validates: Requirements 4.2**
    - Usar FsCheck para generar combinaciones arbitrarias de `ScenarioKey` y `StepKey` registradas

  - [ ]* 3.2 Escribir property test: Interpolación reemplaza todos los placeholders
    - **Property 10: Interpolación de variables reemplaza todos los placeholders**
    - **Validates: Requirements 4.4, 4.5**
    - Generar templates con placeholders arbitrarios y diccionarios con todas las claves presentes

- [x] 4. Implementar `GymUserProfileRepository` en `Chatbot/Services/Gym/GymUserProfileRepository.cs`
  - Implementar `IGymUserProfileRepository` con almacenamiento en memoria (`ConcurrentDictionary`) como capa inicial
  - Validar `UserId` no vacío → lanzar `ArgumentException` (Req 2.4)
  - Validar `FechaUltimoCheckIn` no futura → lanzar `ArgumentException` (Req 2.5)
  - Garantizar `TotalClasesAsistidas >= 0` en `UpdateProfileAsync` (Req 2.6)
  - Implementar `GetInactiveUsersAsync(thresholdDays)` filtrando por `(UtcNow - FechaUltimoCheckIn).TotalDays > thresholdDays`
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [ ]* 4.1 Escribir property test: GetInactiveUsersAsync filtra correctamente por umbral
    - **Property 11: GetInactiveUsersAsync filtra correctamente por umbral**
    - **Validates: Requirements 2.3, 5.1**
    - Generar conjuntos arbitrarios de perfiles con fechas variadas y verificar que solo se retornan los que superan el umbral

  - [ ]* 4.2 Escribir property test: TotalClasesAsistidas siempre no negativo
    - **Property 15: TotalClasesAsistidas es siempre no negativo**
    - **Validates: Requirements 2.6**

- [x] 5. Implementar `GymStateEngine` en `Chatbot/Services/Gym/GymStateEngine.cs`
  - Implementar `IGymStateEngine` con `IMemoryCache` (TTL 30 min) para `GetCurrentStateAsync`
  - Construir `Dictionary<(ScenarioKey, StepKey), IIntentHandler>` inyectado vía constructor (recibir `IEnumerable<IIntentHandler>` con metadata de clave)
  - Implementar `InitiateScenarioAsync`: setear `ActiveScenario`, `CurrentStep = TOFU_Question`, `FunnelStage = TOFU`, limpiar `ContextData`
  - Implementar `ProcessMessageAsync`: resolver handler, ejecutar, avanzar estado, sincronizar `EtapaEmbudo` si `FunnelStage` avanza, persistir estado (capturar excepción de persistencia y loggear sin relanzar)
  - Implementar `ResetStateAsync`: setear `ActiveScenario = None`
  - Invalidar/actualizar caché tras `UpdateConversationStateAsync`
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 10.1, 10.2, 10.3, 11.1_

  - [ ]* 5.1 Escribir property test: FunnelStage es monótonamente no decreciente
    - **Property 5: FunnelStage es monótonamente no decreciente**
    - **Validates: Requirements 3.5**
    - Generar secuencias arbitrarias de mensajes y verificar que `FunnelStage` nunca retrocede

  - [ ]* 5.2 Escribir property test: InitiateScenarioAsync limpia ContextData
    - **Property 4: InitiateScenarioAsync limpia ContextData**
    - **Validates: Requirements 3.2**

  - [ ]* 5.3 Escribir property test: Reset restaura estado inicial
    - **Property 7: Reset restaura estado inicial**
    - **Validates: Requirements 3.7**

  - [ ]* 5.4 Escribir property test: ProcessMessageAsync persiste el estado actualizado
    - **Property 8: ProcessMessageAsync persiste el estado actualizado**
    - **Validates: Requirements 3.8**

  - [ ]* 5.5 Escribir property test: Caché devuelve estado consistente tras actualización
    - **Property 16: Caché devuelve estado consistente tras actualización**
    - **Validates: Requirements 10.2**

  - [ ]* 5.6 Escribir property test: Fallo de persistencia no bloquea la respuesta al usuario
    - **Property 14: Fallo de persistencia no bloquea la respuesta al usuario**
    - **Validates: Requirements 11.1**

- [x] 6. Implementar handlers para Escenario 1: Propósito de Año Nuevo en `Chatbot/Services/Gym/Handlers/`
  - [x] 6.1 Crear `PropositoAnoNuevoTofuHandler.cs` para `(PropositoAnoNuevo, TOFU_Question)`
    - Preguntar si el usuario ha entrenado antes o empieza desde cero
    - Retornar `NextStep = TOFU_Response`, `NextFunnelStage = TOFU`
    - _Requirements: 6.2_

  - [x] 6.2 Crear `PropositoAnoNuevoMofuHandler.cs` para `(PropositoAnoNuevo, TOFU_Response)`
    - Presentar Plan Welcome con clase de prueba gratis
    - Retornar `NextStep = MOFU_Offer`, `NextFunnelStage = MOFU`
    - _Requirements: 6.3_

  - [x] 6.3 Crear `PropositoAnoNuevoBofuHandler.cs` para `(PropositoAnoNuevo, MOFU_Offer)`
    - Confirmar clase con 20% de descuento
    - Retornar `NextStep = BOFU_Confirm`, `NextFunnelStage = BOFU`
    - _Requirements: 6.4, 6.5_

  - [ ]* 6.4 Escribir unit tests para los 3 handlers de PropositoAnoNuevo
    - Verificar transiciones de `StepKey` y `FunnelStage` correctas
    - Verificar que la respuesta no es nula ni vacía
    - _Requirements: 6.2, 6.3, 6.4, 6.5_

- [x] 7. Implementar handlers para Escenario 2: Atleta Estancado en `Chatbot/Services/Gym/Handlers/`
  - [x] 7.1 Crear `AtletaEstancadoTofuHandler.cs` para `(AtletaEstancado, TOFU_Question)`
    - Preguntar sobre objetivo actual y el estancamiento que experimenta
    - Retornar `NextStep = TOFU_Response`, `NextFunnelStage = TOFU`
    - _Requirements: 7.2_

  - [x] 7.2 Crear `AtletaEstancadoMofuHandler.cs` para `(AtletaEstancado, TOFU_Response)`
    - Presentar plan de entrenamiento personalizado o sesión con coach
    - Retornar `NextStep = MOFU_Offer`, `NextFunnelStage = MOFU`
    - _Requirements: 7.3_

  - [x] 7.3 Crear `AtletaEstancadoBofuHandler.cs` para `(AtletaEstancado, MOFU_Offer)`
    - Presentar paso concreto (reserva de sesión)
    - Retornar `NextStep = BOFU_Confirm`, `NextFunnelStage = BOFU`
    - _Requirements: 7.4_

  - [ ]* 7.4 Escribir unit tests para los 3 handlers de AtletaEstancado
    - Verificar transiciones de `StepKey` y `FunnelStage` correctas
    - _Requirements: 7.2, 7.3, 7.4_

- [x] 8. Implementar handlers para Escenario 3: Desertor en `Chatbot/Services/Gym/Handlers/`
  - [x] 8.1 Crear `DesertorTofuHandler.cs` para `(Desertor, TOFU_Question)`
    - Enviar mensaje de re-engagement personalizado con el nombre del usuario
    - Retornar `NextStep = TOFU_Response`, `NextFunnelStage = TOFU`
    - _Requirements: 8.2_

  - [x] 8.2 Crear `DesertorMofuHandler.cs` para `(Desertor, TOFU_Response)`
    - Presentar incentivo de retorno (clase gratis o descuento)
    - Retornar `NextStep = MOFU_Offer`, `NextFunnelStage = MOFU`
    - _Requirements: 8.3_

  - [x] 8.3 Crear `DesertorBofuHandler.cs` para `(Desertor, MOFU_Offer)`
    - Completar flujo de re-engagement con confirmación
    - Retornar `NextStep = BOFU_Confirm`, `NextFunnelStage = BOFU`
    - _Requirements: 8.4_

  - [ ]* 8.4 Escribir unit tests para los 3 handlers de Desertor
    - Verificar que el mensaje TOFU incluye el nombre del usuario (interpolación)
    - Verificar transiciones de `StepKey` y `FunnelStage` correctas
    - _Requirements: 8.2, 8.3, 8.4_

- [x] 9. Checkpoint — Verificar que todos los tests pasan hasta este punto
  - Asegurarse de que todos los tests pasan. Consultar al usuario si surgen dudas.

- [x] 10. Implementar `GymConversationRouter` en `Chatbot/Services/Gym/GymConversationRouter.cs`
  - Implementar `IGymConversationRouter` con inyección de `IGymStateEngine`, `IGymUserProfileRepository`, `INotificationResources`
  - Validar `userId` no nulo/vacío → retornar error `BotResponse` sin acceder al repositorio (Req 1.5)
  - Si `ActiveScenario == None` → retornar `WelcomeMessage` sin modificar estado (Req 1.1, 9.3)
  - Si mensaje es "1"–"5" y estado es `None` → llamar `InitiateScenarioAsync` con el escenario mapeado (Req 1.2)
  - Delegar al `GymStateEngine.ProcessMessageAsync` para estados activos (Req 1.3)
  - Envolver toda la lógica en try/catch → retornar fallback `BotResponse` en caso de excepción no manejada (Req 11.3)
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 9.1, 9.2, 9.3, 11.3_

  - [ ]* 10.1 Escribir property test: RouteMessageAsync siempre retorna respuesta no nula
    - **Property 1: RouteMessageAsync siempre retorna respuesta no nula**
    - **Validates: Requirements 1.4, 11.3**
    - Generar `userId` y `incomingMessage` arbitrarios (incluyendo strings vacíos y nulos)

  - [ ]* 10.2 Escribir property test: Estado None produce WelcomeMessage
    - **Property 2: Estado None produce WelcomeMessage**
    - **Validates: Requirements 1.1, 9.1, 9.3**

  - [ ]* 10.3 Escribir property test: Selección de menú inicializa escenario correcto
    - **Property 3: Selección de menú inicializa escenario correcto**
    - **Validates: Requirements 1.2, 3.1**
    - Generar opciones "1"–"5" y verificar `ActiveScenario`, `CurrentStep = TOFU_Question`, `FunnelStage = TOFU`

  - [ ]* 10.4 Escribir property test: EtapaEmbudo del perfil se sincroniza con FunnelStage
    - **Property 6: EtapaEmbudo del perfil se sincroniza con FunnelStage**
    - **Validates: Requirements 3.4**

- [x] 11. Implementar `GymTriggerService` en `Chatbot/Services/Gym/GymTriggerService.cs`
  - Implementar `IGymTriggerService` con inyección de `IGymUserProfileRepository`, `IGymStateEngine`, `INotificationResources`, `IChannelAdapter`, `ILogger`
  - `RunInactivityCheckAsync`: obtener usuarios inactivos > 15 días, verificar ventana 24h, iniciar escenario `Desertor`, enviar mensaje proactivo, actualizar `LastInactivityTrigger` solo si el envío fue exitoso
  - Si el canal falla → loggear Warning, no actualizar metadata, continuar con el siguiente usuario (Req 5.4, 11.2)
  - `RunPostFirstClassFollowUpAsync`: filtrar usuarios con `FechaPrimeraClase` entre 20 y 28 horas atrás sin trigger previo (Req 5.6)
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 11.2_

  - [ ]* 11.1 Escribir property test: Sin mensajes duplicados de inactividad en 24h
    - **Property 12: Sin mensajes duplicados de inactividad en 24h**
    - **Validates: Requirements 5.5**
    - Mockear repositorio con usuarios que tienen `LastInactivityTrigger` reciente y verificar que no se envían mensajes

  - [ ]* 11.2 Escribir property test: Fallo del canal no interrumpe el batch de triggers
    - **Property 13: Fallo del canal no interrumpe el batch de triggers**
    - **Validates: Requirements 5.2, 11.2**
    - Simular fallo en canal para un subconjunto de usuarios y verificar que el resto se procesa

- [x] 12. Crear `GymTriggerBackgroundService` en `Chatbot/BackgroundServices/GymTriggerBackgroundService.cs`
  - Heredar de `BackgroundService` de .NET 8
  - Usar `PeriodicTimer` con intervalo configurable (default: 1 hora) para evitar drift
  - Llamar a `RunInactivityCheckAsync`, `RunPostFirstClassFollowUpAsync`, `RunMilestoneCheckAsync` en cada ciclo
  - Loggear inicio y fin de cada ciclo con duración
  - _Requirements: 12.5_

- [x] 13. Registrar servicios en `Program.cs`
  - Agregar `builder.Services.AddMemoryCache()` si no está presente
  - Registrar `INotificationResources` como singleton
  - Registrar `IGymUserProfileRepository` como singleton
  - Registrar `IGymStateEngine` como scoped
  - Registrar `IGymConversationRouter` como scoped
  - Registrar `GymTriggerBackgroundService` como hosted service con `AddHostedService<>()`
  - Registrar todos los `IIntentHandler` concretos para que el contenedor DI los inyecte en `GymStateEngine`
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [ ] 14. Tests de integración de los 3 flujos completos
  - [ ]* 14.1 Test de integración: Flujo completo PropositoAnoNuevo (TOFU → BOFU)
    - Simular 3 mensajes consecutivos y verificar transiciones de estado y respuestas
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 14.2 Test de integración: Flujo completo AtletaEstancado (TOFU → BOFU)
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ]* 14.3 Test de integración: Flujo completo Desertor iniciado por trigger
    - Verificar que el trigger inicia el escenario y el usuario puede completar el flujo
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [ ]* 14.4 Test de integración: Reset de estado vuelve al WelcomeMessage
    - _Requirements: 3.7, 9.1_

- [x] 15. Checkpoint final — Verificar que todos los tests pasan
  - Asegurarse de que todos los tests pasan. Consultar al usuario si surgen dudas.

## Notes

- Las tareas marcadas con `*` son opcionales y pueden omitirse para un MVP más rápido
- Cada tarea referencia los requisitos específicos para trazabilidad
- Los property tests usan `FsCheck.Xunit` — agregar al proyecto de tests con `dotnet add package FsCheck.Xunit`
- El repositorio inicial usa `ConcurrentDictionary` en memoria; reemplazar por EF Core o DynamoDB en producción
- Los handlers concretos se registran en DI y se inyectan como `IEnumerable<IIntentHandler>` en `GymStateEngine`
