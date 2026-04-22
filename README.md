# HighlandsBot — Chatbot Multimodal con IA

Chatbot inteligente para gimnasio que combina búsqueda semántica (RAG), procesamiento de audio local y mensajería por WhatsApp. Construido sobre .NET 10, AWS Bedrock y Vosk.

---

## Descripción General

HighlandsBot responde preguntas de socios del gimnasio a través de WhatsApp. Cuando un usuario envía un mensaje de texto o una nota de voz, el sistema:

1. Transcribe el audio localmente con Vosk (sin enviar datos a la nube).
2. Convierte la pregunta en un vector de 1024 dimensiones usando Amazon Titan Embeddings.
3. Busca el fragmento más relevante en AWS S3 Vectors (base de conocimiento del gimnasio).
4. Genera una respuesta natural con DeepSeek AI usando el contexto recuperado.
5. Envía la respuesta al usuario por WhatsApp a través del puente Node.js / Twilio.

Además, el bot incluye un motor de flujos conversacionales (TOFU → MOFU → BOFU) para guiar a los socios a través de escenarios de ventas y retención, y un servicio de triggers proactivos que detecta inactividad y envía mensajes automáticos.

---

## Arquitectura del Sistema

```
Usuario WhatsApp
      │
      ▼
[Twilio / Node.js Bridge]  ←── ngrok (puerto 5000)
      │  POST /api/whatsapp
      ▼
[WhatsAppController.cs]
      │
      ├─── Audio? ──► [VoskTranscriptionService]  (100% local, sin nube)
      │
      ▼
[ChatbotService.AskQuestionAsync]
      │
      ├─► [Amazon Bedrock] Titan Embed Text v2  →  vector 1024 dims
      │
      ├─► [AWS S3 Vectors] QueryVectorsAsync (TopK=1, métrica coseno)
      │
      └─► [DeepSeek AI] Genera respuesta natural con el contexto RAG
              │
              ▼
      [LocalNodeWhatsAppAdapter]  →  http://localhost:3000/send
              │
              ▼
      Usuario WhatsApp
```

### Componentes Principales

| Componente | Tecnología | Responsabilidad |
|---|---|---|
| API Web | ASP.NET Core (.NET 10) | Recibe webhooks de Twilio |
| Embeddings de texto | Amazon Titan Embed Text v2 | Vectoriza preguntas y documentos |
| Embeddings de imagen | Amazon Titan Embed Image v1 | Búsqueda multimodal imagen↔texto |
| Base vectorial | AWS S3 Vectors | Almacena y consulta vectores |
| IA Generativa | DeepSeek Chat API | Genera respuestas en lenguaje natural |
| Transcripción de voz | Vosk (modelo español local) | STT offline, privacidad total |
| Mensajería | Twilio API for WhatsApp | Canal de comunicación |
| Puente local | Node.js (Bridge/index.js) | Proxy entre .NET y WhatsApp Web |
| Flujos conversacionales | GymStateEngine + Handlers | Máquina de estados TOFU/MOFU/BOFU |
| Triggers proactivos | GymTriggerBackgroundService | Mensajes automáticos por inactividad |

---

## Stack Tecnológico

- **Lenguaje:** C# con .NET 10.0
- **Framework web:** ASP.NET Core (Minimal API + Controllers)
- **Nube:** AWS — S3 Vectors (base vectorial), Bedrock (modelos de IA)
- **IA Generativa:** DeepSeek Chat (`deepseek-chat`)
- **Mensajería:** Twilio API for WhatsApp + Node.js Bridge
- **STT local:** Vosk 0.3.38 con modelo español
- **Audio:** Concentus 2.2.2 (decodificación Ogg/Opus), NAudio 2.2.1
- **Orquestación IA:** Microsoft Semantic Kernel 1.26.0 + Connectors Amazon

### Paquetes NuGet Clave

```xml
<PackageReference Include="Vosk" Version="0.3.38" />
<PackageReference Include="Twilio" Version="7.0.0" />
<PackageReference Include="AWSSDK.BedrockRuntime" Version="3.7.400" />
<PackageReference Include="AWSSDK.S3Vectors" Version="3.7.400" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.26.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Amazon" Version="1.26.0-alpha" />
<PackageReference Include="Concentus" Version="2.2.2" />
<PackageReference Include="NAudio" Version="2.2.1" />
```

---

## Requisitos Previos

1. **.NET 10.0 SDK** instalado.
2. **Cuenta AWS** con acceso a Bedrock (modelos Titan habilitados) y S3 Vectors.
3. **Cuenta Twilio** con Sandbox de WhatsApp configurado.
4. **API Key de DeepSeek** (plataforma.deepseek.com).
5. **Modelo Vosk en español** descargado y descomprimido:
   - Modelo recomendado: `vosk-model-small-es-0.42`
   - Descarga: https://alphacephei.com/vosk/models
6. **Node.js** instalado para el puente local (`Chatbot/Bridge/`).
7. **ngrok** para exponer el servidor local a Twilio.

---

## Instalación y Configuración

### 1. Clonar y restaurar dependencias

```bash
git clone <repo-url>
cd Chatbot
dotnet restore
```

### 2. Instalar el puente Node.js

```bash
cd Bridge
npm install
```

### 3. Configurar variables de entorno / appsettings

Copia `appsettings.example.json` como `appsettings.Development.json` y rellena los valores:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "AccessKey": "TU_ACCESS_KEY",
    "SecretKey": "TU_SECRET_KEY"
  },
  "S3Vectors": {
    "BucketName": "nombre-de-tu-bucket",
    "IndexName": "nombre-del-indice-texto",
    "ImageIndexName": "nombre-del-indice-imagenes",
    "TextEmbeddingDimensions": 1024,
    "ImageEmbeddingDimensions": 1024,
    "MaxQueryDistance": 0.75
  },
  "Bedrock": {
    "TextEmbeddingModel": "amazon.titan-embed-text-v2:0",
    "ImageEmbeddingModel": "amazon.titan-embed-image-v1"
  },
  "DeepSeek": {
    "ApiKey": "TU_DEEPSEEK_API_KEY"
  },
  "Twilio": {
    "AccountSid": "TU_ACCOUNT_SID",
    "AuthToken": "TU_AUTH_TOKEN",
    "WhatsAppNumber": "+14155238886"
  },
  "Vosk": {
    "ModelPath": "vosk-model-es"
  }
}
```

### Variables de entorno disponibles

| Variable | Descripción |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Debe ser `Development` para cargar `appsettings.Development.json` |
| `VOSK_MODEL_PATH` | Ruta absoluta al modelo Vosk (sobreescribe `appsettings`) |

### 4. Ejecutar el proyecto

**Terminal 1 — Backend .NET:**
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
cd Chatbot
dotnet run --project Chatbot.csproj
```

**Terminal 2 — Puente Node.js:**
```bash
cd Chatbot/Bridge
node index.js
```

**Terminal 3 — Túnel ngrok:**
```bash
ngrok http 5000
```

### 5. Configurar Twilio

1. Copia la URL HTTPS que genera ngrok (ej. `https://xxxx.ngrok-free.app`).
2. En el Sandbox de Twilio, configura el webhook de mensajes entrantes:
   ```
   https://xxxx.ngrok-free.app/api/whatsapp
   ```
3. Método: `POST`.

---

## Menú de Consola

Al iniciar el servidor, aparece un menú interactivo en la terminal:

```
┌─────────────────────────────────────┐
│           MENÚ PRINCIPAL              │
├───────────────────────────────────────┤
│ 1. Cargar preguntas al índice         │
│ 2. Buscar por descripción (texto)     │
│ 3. Buscar por URL de imagen           │
│ 4. Test de Embeddings (Debug)         │
│ 5. Salir                              │
└───────────────────────────────────────┘
```

| Opción | Función |
|---|---|
| **1. Cargar preguntas al índice** | Lee `Ramas/datos_gimnasio.json`, genera embeddings con Titan y los sube a S3 Vectors. Es el paso de ingestión RAG. Pide confirmación antes de ejecutar. |
| **2. Buscar por descripción (texto)** | Modo de prueba: escribe una descripción en texto y el sistema busca la imagen más similar en el índice multimodal. Muestra distancia coseno y porcentaje de similitud. |
| **3. Buscar por URL de imagen** | Proporciona una URL de imagen (incluyendo Google Drive) y el sistema la descarga, genera su embedding y busca las 5 imágenes más similares en el índice. |
| **4. Test de Embeddings (Debug)** | Ejecuta pruebas de similitud entre pares de textos para verificar que la conexión con Bedrock funciona correctamente. Útil para diagnosticar problemas de configuración AWS. |
| **5. Salir** | Cierra el menú interactivo. El servidor HTTP sigue corriendo hasta Ctrl+C. |

> El servidor HTTP escucha en `http://0.0.0.0:5000` y el menú corre en paralelo en el mismo proceso gracias a `app.StartAsync()` + `app.WaitForShutdownAsync()`.

---

## Flujos Conversacionales del Gimnasio

El bot implementa una máquina de estados con tres escenarios activos:

| Escenario | Trigger | Embudo |
|---|---|---|
| Propósito de Año Nuevo | Opción 1 del menú WhatsApp | TOFU → MOFU → BOFU |
| Atleta Estancado | Opciones 2 y 3 del menú | TOFU → MOFU → BOFU |
| Desertor | Automático: inactividad > 15 días | TOFU → MOFU → BOFU |

El `GymTriggerBackgroundService` corre cada hora y evalúa condiciones de inactividad, post-primera-clase, hitos y formularios abandonados.

---

## Solución de Errores Comunes

| Error | Causa | Solución |
|---|---|---|
| `HTTP 404` en Twilio | URL del webhook incorrecta | Verificar que termine en `/api/whatsapp` |
| `HTTP 500` al iniciar | Ruta del modelo Vosk incorrecta | Revisar `Vosk:ModelPath` en appsettings |
| `Authorization Error` | Variable de entorno no seteada | Ejecutar `$env:ASPNETCORE_ENVIRONMENT="Development"` antes de `dotnet run` |
| `No se pudo conectar con Node.js` | Puente no iniciado | Ejecutar `node index.js` en `Chatbot/Bridge/` |
| Distancia siempre > 0.75 | Índice vacío o modelo incorrecto | Ejecutar opción 1 del menú para cargar datos |
