# Requirements Document

## Introduction

Este módulo implementa la lógica de negocio de un chatbot conversacional para gimnasio sobre la infraestructura existente de C# .NET 8 / AWS Bedrock. El sistema gestiona 16 escenarios de retención, ventas y fidelización mediante una máquina de estados basada en intenciones, un enrutador principal con menú de bienvenida de 5 opciones, y un motor de triggers proactivos para eventos como inactividad, seguimiento post-clase e hitos de gamificación.

## Glossary

- **GymConversationRouter**: Servicio punto de entrada único que enruta mensajes entrantes al flujo correcto.
- **GymStateEngine**: Motor de estados basado en `Dictionary<(ScenarioKey, StepKey), IIntentHandler>` que procesa mensajes dentro de un escenario activo.
- **GymUserProfileRepository**: Repositorio CRUD del perfil de usuario con campos de negocio del gimnasio.
- **GymTriggerService**: Servicio de eventos proactivos ejecutado como `BackgroundService` de .NET.
- **NotificationResources**: Repositorio centralizado de todos los textos de respuesta al usuario.
- **UserProfile**: Modelo de datos del usuario con campos `UserId`, `Name`, `TipoMembresia`, `Objetivo`, `FechaUltimoCheckIn`, `LesionesPrevias`, `EtapaEmbudo`, `TotalClasesAsistidas`.
- **ConversationState**: Modelo que representa el estado activo de una conversación: `ActiveScenario`, `CurrentStep`, `FunnelStage`, `LastInteraction`, `ContextData`.
- **ScenarioKey**: Enumeración de los 16 escenarios de negocio (ej. `PropositoAnoNuevo`, `Desertor`, `AtletaEstancado`).
- **StepKey**: Enumeración de los pasos dentro de un escenario (`TOFU_Question`, `MOFU_Offer`, `BOFU_Confirm`, etc.).
- **FunnelStage**: Etapa del embudo de conversión: `TOFU`, `MOFU`, `BOFU`, `Fidelizacion`.
- **IIntentHandler**: Interfaz que encapsula la lógica de respuesta para una combinación `(ScenarioKey, StepKey)`.
- **WelcomeMessage**: Mensaje inicial con 5 opciones de menú presentado a usuarios sin estado activo.
- **TriggerType**: Tipo de evento proactivo (`Inactivity15Days`, `PostFirstClass24h`, `AbandonedForm2h`, `Milestone`, `MonthlyReport`).

---

## Requirements

### Requirement 1: Enrutamiento de Mensajes Entrantes

**User Story:** As a gym member, I want the chatbot to correctly route my messages, so that I receive the appropriate response based on my current conversation state.

#### Acceptance Criteria

1. WHEN a user sends a message and their `ConversationState.ActiveScenario` equals `ScenarioKey.None`, THE `GymConversationRouter` SHALL return the `WelcomeMessage` with the 5-option menu.
2. WHEN a user sends a message with value "1" through "5" and their state is `ScenarioKey.None`, THE `GymConversationRouter` SHALL initialize the corresponding scenario via `GymStateEngine.InitiateScenarioAsync` and return the first TOFU response.
3. WHEN a user sends a message and their `ConversationState.ActiveScenario` is not `ScenarioKey.None`, THE `GymConversationRouter` SHALL delegate processing to `GymStateEngine.ProcessMessageAsync`.
4. THE `GymConversationRouter` SHALL always return a non-null `BotResponse` with a non-empty `Message` property.
5. IF `userId` is null or empty, THEN THE `GymConversationRouter` SHALL return an error `BotResponse` without attempting to access the repository.

---

### Requirement 2: Gestión del Perfil de Usuario

**User Story:** As a gym member, I want my profile to be created automatically on first contact, so that the chatbot can personalize my experience from the start.

#### Acceptance Criteria

1. WHEN a user sends their first message, THE `GymUserProfileRepository` SHALL create a new `UserProfile` with default values if no profile exists for that `userId`.
2. THE `GymUserProfileRepository` SHALL persist `UserProfile` changes atomically so that concurrent reads return consistent data.
3. WHEN `GetInactiveUsersAsync` is called with a `thresholdDays` value, THE `GymUserProfileRepository` SHALL return only users whose `FechaUltimoCheckIn` is older than `thresholdDays` days from the current UTC time.
4. IF `UserProfile.UserId` is empty, THEN THE `GymUserProfileRepository` SHALL reject the operation and throw an `ArgumentException`.
5. IF `UserProfile.FechaUltimoCheckIn` is a future date, THEN THE `GymUserProfileRepository` SHALL reject the update and throw an `ArgumentException`.
6. THE `GymUserProfileRepository` SHALL ensure `TotalClasesAsistidas` is always greater than or equal to zero.

---

### Requirement 3: Motor de Estados y Transiciones de Embudo

**User Story:** As a gym member, I want the chatbot to guide me through a structured conversation flow, so that I receive relevant offers and information at each stage of my journey.

#### Acceptance Criteria

1. WHEN `GymStateEngine.InitiateScenarioAsync` is called with a valid `ScenarioKey`, THE `GymStateEngine` SHALL set `ConversationState.ActiveScenario` to that scenario, `CurrentStep` to `StepKey.TOFU_Question`, and `FunnelStage` to `FunnelStage.TOFU`.
2. WHEN `GymStateEngine.InitiateScenarioAsync` is called, THE `GymStateEngine` SHALL clear all previous `ContextData` from the `ConversationState`.
3. WHEN `GymStateEngine.ProcessMessageAsync` is called, THE `GymStateEngine` SHALL resolve the `IIntentHandler` for the current `(ActiveScenario, CurrentStep)` key and execute it.
4. WHEN an `IIntentHandler` returns a result with a higher `FunnelStage`, THE `GymStateEngine` SHALL advance `ConversationState.FunnelStage` and synchronize `UserProfile.EtapaEmbudo` accordingly.
5. THE `GymStateEngine` SHALL ensure `FunnelStage` never decreases (BOFU cannot revert to TOFU or MOFU).
6. IF no `IIntentHandler` is registered for the current `(ActiveScenario, CurrentStep)` key, THEN THE `GymStateEngine` SHALL return the fallback response from `NotificationResources` using `(ScenarioKey.None, StepKey.Initial)`.
7. WHEN `GymStateEngine.ResetStateAsync` is called, THE `GymStateEngine` SHALL set `ConversationState.ActiveScenario` to `ScenarioKey.None` so the next message triggers the `WelcomeMessage`.
8. WHEN `GymStateEngine.ProcessMessageAsync` completes successfully, THE `GymStateEngine` SHALL persist the updated `ConversationState` via `GymUserProfileRepository.UpdateConversationStateAsync`.

---

### Requirement 4: Recursos de Notificación Centralizados

**User Story:** As a developer, I want all response texts to be centralized in one place, so that content changes do not require modifying business logic.

#### Acceptance Criteria

1. THE `NotificationResources` SHALL store all user-facing response strings, and no business logic class SHALL contain hardcoded user-facing strings.
2. WHEN `NotificationResources.GetResponse` is called with a registered `(ScenarioKey, StepKey)` combination, THE `NotificationResources` SHALL return a non-null, non-empty string.
3. IF `NotificationResources.GetResponse` is called with an unregistered `(ScenarioKey, StepKey)` combination, THEN THE `NotificationResources` SHALL return a default fallback message and SHALL NOT throw an exception.
4. WHEN `NotificationResources.GetResponse` is called with a `variables` dictionary, THE `NotificationResources` SHALL replace all `{key}` placeholders in the template with the corresponding values.
5. WHEN `NotificationResources.GetProactiveMessage` is called with a `TriggerType` and variables, THE `NotificationResources` SHALL return the interpolated proactive message template for that trigger type.

---

### Requirement 5: Triggers Proactivos de Inactividad

**User Story:** As a gym operator, I want the system to automatically re-engage inactive members, so that I can reduce churn without manual intervention.

#### Acceptance Criteria

1. WHEN `GymTriggerService.RunInactivityCheckAsync` is called, THE `GymTriggerService` SHALL retrieve all users whose `FechaUltimoCheckIn` is more than 15 days ago.
2. WHEN an inactive user is identified and no inactivity trigger was sent in the last 24 hours, THE `GymTriggerService` SHALL initiate the `ScenarioKey.Desertor` scenario for that user and send the proactive message via the channel adapter.
3. WHEN a proactive message is sent successfully, THE `GymTriggerService` SHALL update `UserProfile.Metadata["LastInactivityTrigger"]` to the current UTC time.
4. IF the channel adapter fails to deliver a message, THEN THE `GymTriggerService` SHALL log a warning and SHALL NOT update `Metadata["LastInactivityTrigger"]`, allowing retry on the next cycle.
5. THE `GymTriggerService` SHALL NOT send more than one inactivity message to the same user within a 24-hour window.
6. WHEN `GymTriggerService.RunPostFirstClassFollowUpAsync` is called, THE `GymTriggerService` SHALL send a follow-up message to users whose `FechaPrimeraClase` was between 20 and 28 hours ago and who have not received a post-class trigger.

---

### Requirement 6: Escenario Propósito de Año Nuevo (Caso 1)

**User Story:** As a new gym prospect, I want the chatbot to guide me from initial interest to booking a free trial class, so that I can experience the gym before committing.

#### Acceptance Criteria

1. WHEN a user selects option "1" from the welcome menu, THE `GymConversationRouter` SHALL initiate `ScenarioKey.PropositoAnoNuevo` at `StepKey.TOFU_Question`.
2. WHEN the `PropositoAnoNuevo` scenario is at `StepKey.TOFU_Question`, THE `GymStateEngine` SHALL ask whether the user has trained before or is starting from scratch.
3. WHEN the user responds to the TOFU question in `PropositoAnoNuevo`, THE `GymStateEngine` SHALL advance to `StepKey.MOFU_Offer` and present the Welcome Plan with a free trial class offer.
4. WHEN the user confirms interest in the MOFU offer for `PropositoAnoNuevo`, THE `GymStateEngine` SHALL advance to `StepKey.BOFU_Confirm` with a 20% discount offer and class confirmation.
5. WHEN the `PropositoAnoNuevo` scenario reaches `StepKey.BOFU_Confirm`, THE `GymStateEngine` SHALL set `FunnelStage` to `FunnelStage.BOFU` and synchronize `UserProfile.EtapaEmbudo`.

---

### Requirement 7: Escenario Atleta Estancado (Caso 2)

**User Story:** As an existing member who has hit a plateau, I want the chatbot to offer me personalized training solutions, so that I can break through my performance barrier.

#### Acceptance Criteria

1. WHEN a user selects option "2" from the welcome menu, THE `GymConversationRouter` SHALL initiate `ScenarioKey.AtletaEstancado` at `StepKey.TOFU_Question`.
2. WHEN the `AtletaEstancado` scenario is at `StepKey.TOFU_Question`, THE `GymStateEngine` SHALL ask the user about their current training goal and the plateau they are experiencing.
3. WHEN the user responds to the TOFU question in `AtletaEstancado`, THE `GymStateEngine` SHALL advance to `StepKey.MOFU_Offer` and present a personalized training plan or coach session offer.
4. WHEN the user confirms interest in the MOFU offer for `AtletaEstancado`, THE `GymStateEngine` SHALL advance to `StepKey.BOFU_Confirm` and present a concrete next step (e.g., session booking).

---

### Requirement 8: Escenario Desertor (Caso 3)

**User Story:** As a lapsed gym member, I want to receive a personalized re-engagement message, so that I feel motivated to return to the gym.

#### Acceptance Criteria

1. WHEN `GymTriggerService` identifies a user with more than 15 days of inactivity, THE `GymTriggerService` SHALL initiate `ScenarioKey.Desertor` for that user.
2. WHEN the `Desertor` scenario is initiated, THE `GymStateEngine` SHALL set `CurrentStep` to `StepKey.TOFU_Question` and send a personalized re-engagement message using the user's name.
3. WHEN the user responds to the `Desertor` TOFU message, THE `GymStateEngine` SHALL advance to `StepKey.MOFU_Offer` and present a return incentive (e.g., free class, discount).
4. WHEN the user confirms the return offer in `Desertor`, THE `GymStateEngine` SHALL advance to `StepKey.BOFU_Confirm` and complete the re-engagement flow.

---

### Requirement 9: Mensaje Maestro de Bienvenida

**User Story:** As a gym member or prospect, I want to see a clear welcome menu on first contact, so that I can quickly navigate to the service I need.

#### Acceptance Criteria

1. WHEN a user contacts the chatbot for the first time or after a state reset, THE `GymConversationRouter` SHALL return the `WelcomeMessage` containing exactly 5 numbered options.
2. THE `NotificationResources` SHALL provide the `WelcomeMessage` text, and THE `GymConversationRouter` SHALL NOT hardcode the welcome text.
3. WHEN the `WelcomeMessage` is displayed, THE `GymConversationRouter` SHALL NOT modify `ConversationState.ActiveScenario` until the user selects a valid option.

---

### Requirement 10: Caché de Estado de Conversación

**User Story:** As a system operator, I want conversation states to be cached in memory, so that repeated database reads are avoided during active conversations.

#### Acceptance Criteria

1. WHEN `GymStateEngine.GetCurrentStateAsync` is called, THE `GymStateEngine` SHALL return the cached `ConversationState` if it exists in `IMemoryCache` with a TTL of 30 minutes.
2. WHEN `GymUserProfileRepository.UpdateConversationStateAsync` is called, THE `GymStateEngine` SHALL invalidate or update the corresponding cache entry.
3. IF the cache entry has expired, THEN THE `GymStateEngine` SHALL retrieve the `ConversationState` from the persistent store and repopulate the cache.

---

### Requirement 11: Manejo de Errores y Resiliencia

**User Story:** As a gym member, I want the chatbot to remain responsive even when internal errors occur, so that my conversation is not interrupted by technical issues.

#### Acceptance Criteria

1. IF `GymUserProfileRepository.UpdateConversationStateAsync` throws an exception, THEN THE `GymStateEngine` SHALL log the error and still return the `BotResponse` to the caller without re-throwing.
2. IF the channel adapter throws an exception during `GymTriggerService` execution, THEN THE `GymTriggerService` SHALL log a warning and continue processing remaining users in the batch.
3. WHEN an unhandled exception occurs in `GymConversationRouter.RouteMessageAsync`, THE `GymConversationRouter` SHALL return a fallback `BotResponse` with a generic error message rather than propagating the exception to the controller.

---

### Requirement 12: Registro en Contenedor de Dependencias

**User Story:** As a developer, I want all gym chatbot services to be registered in the DI container, so that they are available throughout the application lifecycle.

#### Acceptance Criteria

1. THE `Program.cs` SHALL register `INotificationResources` as a singleton.
2. THE `Program.cs` SHALL register `IGymUserProfileRepository` as a singleton.
3. THE `Program.cs` SHALL register `IGymStateEngine` as scoped.
4. THE `Program.cs` SHALL register `IGymConversationRouter` as scoped.
5. THE `Program.cs` SHALL register `GymTriggerBackgroundService` as a hosted service.
