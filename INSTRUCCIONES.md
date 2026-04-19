
# 🤖 Guía de Ejecución: Chatbot Multimodal Gym (RAG + IA)

Este proyecto es un chatbot inteligente que responde dudas sobre el gimnasio usando **Amazon Bedrock** (IA), **Vosk** (Audio local) y **Twilio** (WhatsApp).

### 🛠️ 1. Requisitos Previos
1.  **Instalar .NET 8.0 SDK.**
2.  **Descargar Modelo Vosk:** Bajar el modelo de español (`vosk-model-small-es-0.42`) y descomprimirlo.
3.  **Ngrok:** Tenerlo instalado para crear el túnel local.

### ⚙️ 2. Configuración (Archivo `appsettings.Development.json`)
Antes de correrlo, deben llenar estos campos en el JSON:
* **Vosk -> ModelPath:** Poner la ruta real de la carpeta del modelo en su PC (ej: `C:\\Users\\Nombre\\Downloads\\vosk-model-es`).
* **AWS:** Poner su `AccessKey`, `SecretKey` y `Region` (`us-east-1`).
* **Twilio:** Configurar su `AccountSid`, `AuthToken` y el número del Sandbox.

### 🚀 3. Comandos de Ejecución
Abran una terminal en la raíz del proyecto (`Chatbot/`) y ejecuten:

1.  **Activar modo Desarrollo (Vital):**
    ```powershell
    $env:ASPNETCORE_ENVIRONMENT="Development"
    ```
2.  **Correr el proyecto:**
    ```powershell
    dotnet run --project Chatbot.csproj
    ```

### 🌐 4. Conexión con WhatsApp
1.  En otra terminal, abran ngrok: `ngrok http 5000`.
2.  Copien la URL de ngrok y péguenla en el **Sandbox de Twilio**.
3.  **IMPORTANTE:** La URL debe terminar en: `/api/whatsapp`.
    * *Ejemplo:* `https://xxxx.ngrok-free.app/api/whatsapp`

### 📋 5. Uso del Menú
* **Opción 1:** Carga los documentos del gym a la nube (S3).
* **Opción 4:** Prueba si la conexión con Amazon Bedrock es correcta (Debug).
* **WhatsApp:** Manden un "Hola" o una nota de voz para probar la respuesta de la IA.

---

### ⚠️ Solución de errores comunes:
* **Error 404:** Revisen que la URL en Twilio incluya `/api/whatsapp`.
* **Error 500:** La ruta de la carpeta de Vosk está mal escrita en el JSON.
* **Authorization Error:** Asegúrense de haber corrido el comando `$env:ASPNETCORE_ENVIRONMENT="Development"` antes del `dotnet run`.

---
