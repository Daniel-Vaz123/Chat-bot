const { Client, LocalAuth, MessageMedia } = require('whatsapp-web.js');
const qrcode = require('qrcode-terminal');
const express = require('express');
const axios = require('axios');

const app = express();
app.use(express.json());

// Puerto donde correrá este puente de Express (3000)
const PORT = 3000;
// URL donde corre tu aplicación de C#
const CSHARP_WEBHOOK_URL = 'http://localhost:5000/api/whatsapp';

// Inicializar cliente de WhatsApp con sesión guardada localmente
const client = new Client({
    authStrategy: new LocalAuth(),
    puppeteer: {
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    }
});

client.on('qr', (qr) => {
    // Generar el QR visible en la terminal
    qrcode.generate(qr, { small: true });
    console.log('🔗 ¡ESCANEA EL CÓDIGO QR DE ARRIBA CON TU WHATSAPP BUSINESS SECUNDARIO!');
});

client.on('ready', () => {
    console.log('✅ ¡WhatsApp Bridge conectado y listo para recibir mensajes!');
});

// Cuando llega un mensaje a WhatsApp
client.on('message', async msg => {
    // Evitar procesar mensajes de grupos
    if (msg.from.includes('@g.us')) return;
    // No reenviar al backend los mensajes que envía el propio bot (evita bucles y peticiones basura)
    if (msg.fromMe) return;

    try {
        let isAudio = false;
        let mediaUrl = null;

        // Comprobar si tiene Media (audio/imagen) - opcional
        if (msg.hasMedia) {
            const media = await msg.downloadMedia();
            if (media.mimetype.startsWith('audio/')) {
                isAudio = true;
                // En un caso real: podrías guardar el audio aquí o mandar base64
                // A tu bot C# (por simplicidad asumiremos que texto para empezar o procesaremos base64)
            }
        }

        const payload = {
            From: msg.from,
            Body: msg.body || "",
            IsAudio: isAudio
        };

        console.log(`📩 Recibido de ${msg.from}: ${msg.body}`);

        // Reenviar a C# (timeout alto: Bedrock + S3 + DeepSeek pueden tardar varios segundos)
        await axios.post(CSHARP_WEBHOOK_URL, payload, { timeout: 120000 });

    } catch (err) {
        console.error('❌ Error enviando webhook a C#:', err.message);
    }
});

client.initialize();

// ==== ENDPOINTS PARA QUE C# NOS ORDENE MANDAR MENSAJES ====

app.post('/send', async (req, res) => {
    const { userId, message } = req.body;

    if (!userId || !message) {
        return res.status(400).json({ error: 'Falta userId o message' });
    }

    try {
        console.log(`📤 Enviando a ${userId}: ${message.substring(0, 50)}...`);
        await client.sendMessage(userId, message);
        res.status(200).json({ success: true });
    } catch (error) {
        console.error('❌ Error enviando mensaje de WhatsApp:', error);
        res.status(500).json({ error: error.toString() });
    }
});

app.listen(PORT, () => {
    console.log(`🚀 Node.js Bridge API escuchando en http://localhost:${PORT}`);
});
