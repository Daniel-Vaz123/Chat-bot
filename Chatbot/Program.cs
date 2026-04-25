using Amazon.S3Vectors;
using Amazon.Extensions.NETCore.Setup;
using Chatbot.BackgroundServices;
using Chatbot.Models;
using Chatbot.Resources;
using Chatbot.Services;
using Chatbot.Services.Gym;
using Chatbot.Services.Gym.Handlers;
using Chatbot.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.Options;
using System.Text.Json;

Console.WriteLine("                                          ");
Console.WriteLine("          HighlandsBot - Chatbot AI       ");
Console.WriteLine("                                          ");
Console.WriteLine();

// Configurar servicios
var builder = WebApplication.CreateBuilder(args);

// Cargar configuración
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Puerto/URLs: ASPNETCORE_URLS (env) > appsettings "Urls" > 5000 por defecto.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(listenUrls))
    listenUrls = builder.Configuration["Urls"];
if (string.IsNullOrWhiteSpace(listenUrls))
    listenUrls = "http://0.0.0.0:5000";
builder.WebHost.UseUrls(listenUrls);

// Configurar opciones
builder.Services.Configure<AppSettings>(builder.Configuration);

var settings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();

// Configurar AWS con credenciales explícitas
var awsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
{
    Region = Amazon.RegionEndpoint.GetBySystemName(settings.AWS.Region)
};

// Si hay credenciales en el archivo, usarlas
if (!string.IsNullOrEmpty(settings.AWS.AccessKey) && !string.IsNullOrEmpty(settings.AWS.SecretKey))
{
    awsOptions.Credentials = new Amazon.Runtime.BasicAWSCredentials(
        settings.AWS.AccessKey,
        settings.AWS.SecretKey
    );
}

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3Vectors>();
builder.Services.AddAWSService<IAmazonBedrockRuntime>();

// Registrar el gestor de índices
builder.Services.AddSingleton<S3VectorsIndexManager>();

// Configurar Semantic Kernel con Bedrock
builder.Services.AddTransient(sp =>
{
    #pragma warning disable SKEXP0070 // Habilita conectores experimentales de Amazon

    var kernelBuilder = Kernel.CreateBuilder();

    // 1. Configurar credenciales y cliente
    var credentials = new Amazon.Runtime.BasicAWSCredentials(settings.AWS.AccessKey, settings.AWS.SecretKey);
    var bedrockClient = new AmazonBedrockRuntimeClient(credentials, Amazon.RegionEndpoint.GetBySystemName(settings.AWS.Region));

    // 2. IMPORTANTE: Registrar el cliente en el contenedor de servicios
    kernelBuilder.Services.AddSingleton<IAmazonBedrockRuntime>(bedrockClient);

    // 3. Registrar generador de embeddings de TEXTO
    // El conector Amazon SK 1.26.0 no expone AddBedrockEmbeddingGenerator en IKernelBuilder.
    // Usamos el generador personalizado BedrockImageEmbeddingGenerator que maneja
    // tanto texto como imagen en el espacio multimodal de Titan.
    kernelBuilder.AddBedrockImageEmbeddingGenerator(
        modelId: settings.Bedrock.TextEmbeddingModel,
        serviceId: "text-embeddings",
        outputLength: 1024
    );

    // 4. Registrar generador de embeddings de IMAGEN (usando nuestra extensión personalizada)
    kernelBuilder.AddBedrockImageEmbeddingGenerator(
        modelId: settings.Bedrock.ImageEmbeddingModel,
        serviceId: "image-embeddings",
        outputLength: 1024
    );

    #pragma warning restore SKEXP0070

    return kernelBuilder.Build();
});

builder.Services.AddTransient<ChatbotService>();
builder.Services.AddTransient<EmbeddingDebugHelper>();

// ── Módulo Gym Chatbot ────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// Recursos de notificación (singleton — carga templates al inicio)
builder.Services.AddSingleton<INotificationResources, NotificationResources>();

// Repositorio de perfiles (singleton — ConcurrentDictionary en memoria)
builder.Services.AddSingleton<IGymUserProfileRepository, GymUserProfileRepository>();

// Motor de estados y router (scoped — un scope por request/mensaje)
builder.Services.AddScoped<IGymStateEngine, GymStateEngine>();
builder.Services.AddScoped<IGymConversationRouter, GymConversationRouter>();

// Handlers de intención — registrados como IIntentHandler para inyección en GymStateEngine
// Escenario 1: Propósito de Año Nuevo
builder.Services.AddScoped<IIntentHandler, PropositoAnoNuevoTofuHandler>();
builder.Services.AddScoped<IIntentHandler, PropositoAnoNuevoMofuHandler>();
builder.Services.AddScoped<IIntentHandler, PropositoAnoNuevoBofuHandler>();
// Escenario 2: Atleta Estancado
builder.Services.AddScoped<IIntentHandler, AtletaEstancadoTofuHandler>();
builder.Services.AddScoped<IIntentHandler, AtletaEstancadoMofuHandler>();
builder.Services.AddScoped<IIntentHandler, AtletaEstancadoBofuHandler>();
// Escenario 3: Desertor
builder.Services.AddScoped<IIntentHandler, DesertorTofuHandler>();
builder.Services.AddScoped<IIntentHandler, DesertorMofuHandler>();
builder.Services.AddScoped<IIntentHandler, DesertorBofuHandler>();

// Trigger service y background service
builder.Services.AddScoped<IGymTriggerService, GymTriggerService>();
builder.Services.AddHostedService<GymTriggerBackgroundService>();
// ─────────────────────────────────────────────────────────────────────────

// Registrar helper multimodal
builder.Services.AddTransient(sp =>
{
    var bedrockRuntime = sp.GetRequiredService<IAmazonBedrockRuntime>();
    return new MultimodalEmbeddingHelper(bedrockRuntime, settings.Bedrock.ImageEmbeddingModel, settings.S3Vectors.ImageEmbeddingDimensions);
});

// ── Integración WhatsApp Bridge / Vosk ───────────────────────────────────
// Configuración de Vosk desde appsettings
builder.Services.Configure<VoskSettings>(builder.Configuration.GetSection("Vosk"));

// HttpClient factory (requerido por VoskTranscriptionService para descargar audio)
builder.Services.AddHttpClient();

// Servicio de transcripción de audio (Singleton — el modelo Vosk se carga una sola vez)
builder.Services.AddSingleton<IAudioTranscriptionService, VoskTranscriptionService>();

// Add HttpClient for the Node bridge (respuestas RAG pueden tardar)
builder.Services.AddHttpClient<LocalNodeWhatsAppAdapter>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

// Añadimos DeepSeek Service
builder.Services.AddHttpClient<IDeepSeekAiService, DeepSeekAiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Canal de salida de mensajes por Node Bridge local
builder.Services.AddTransient<IChannelAdapter, LocalNodeWhatsAppAdapter>();
// ─────────────────────────────────────────────────────────────────────────

// Añadir soporte para controladores MVC (necesario para WhatsAppController)
builder.Services.AddControllers();

var app = builder.Build();

// Mapear rutas de controladores
app.MapControllers();

var chatbotService = app.Services.GetRequiredService<ChatbotService>();
var debugHelper = app.Services.GetRequiredService<EmbeddingDebugHelper>();

Console.WriteLine($"     Configuración cargada:");
Console.WriteLine($"   - Región: {settings.AWS.Region}");
Console.WriteLine($"   - Bucket: {settings.S3Vectors.BucketName}");
Console.WriteLine($"   - Índice: {settings.S3Vectors.IndexName}");
Console.WriteLine($"   - Modelo: {settings.Bedrock.TextEmbeddingModel}");
Console.WriteLine();

// Arrancar el servidor HTTP en background ANTES del menú interactivo.
// Sin esto, app.Run() bloquearía el hilo y el menú nunca se mostraría,
// o el menú bloquearía el hilo y el servidor nunca escucharía peticiones.
await app.StartAsync();
Console.WriteLine($"   Servidor HTTP escuchando en {listenUrls}");
Console.WriteLine("   Webhook Bridge: POST http://<tu-host>:<puerto>/api/whatsapp/webhook");
Console.WriteLine();

// Menú principal (corre en el hilo principal mientras el servidor HTTP corre en background)
bool exit = false;

while (!exit)
{
    Console.WriteLine("\n┌─────────────────────────────────────┐");
    Console.WriteLine("│           MENÚ PRINCIPAL              │");
    Console.WriteLine("├──--───────────────────────────────────┤");
    Console.WriteLine("│ 1. Cargar preguntas al índice         │");
    Console.WriteLine("│ 2. Buscar por descripción (texto)     │");
    Console.WriteLine("│ 3. Buscar por URL de imagen           │");
    Console.WriteLine("│ 4. Test de Embeddings (Debug)         │");
    Console.WriteLine("│ 5. Auditar índice vectorial (AWS)     │");
    Console.WriteLine("│ 6. Salir                              │");
    Console.WriteLine("└────────────────────────────────────--─┘");
    Console.Write("\nSelecciona una opción: ");

    var option = Console.ReadLine();

    switch (option)
    {
        case "1":
            await LoadQuestionsAsync(chatbotService);
            break;

        case "2":
            await ChatbotImageModeAsync(chatbotService);
            break;

        case "3":
            await SearchByImageUrlAsync(chatbotService);
            break;

        case "4":
            await RunEmbeddingDebugAsync(debugHelper);
            break;

        case "5":
            await AuditVectorIndexAsync(chatbotService, settings);
            break;

        case "6":
            exit = true;
            Console.WriteLine("\n ¡Hasta pronto!");
            break;
        default:
            Console.WriteLine("\n Opción inválida. Intenta de nuevo.");
            break;
    }
}

// El servidor HTTP sigue corriendo hasta que el proceso se detenga (Ctrl+C o señal del SO)
await app.WaitForShutdownAsync();

// Función para cargar preguntas
static async Task LoadQuestionsAsync(ChatbotService chatbotService)
{
    Console.WriteLine("\n" + new string('═', 50));
    Console.WriteLine("  CARGA DE PREGUNTAS AL ÍNDICE VECTORIAL");
    Console.WriteLine(new string('═', 50));
    
    Console.WriteLine(" ¿Cuál es la ruta del archivo JSON que vamos a cargar?");
    Console.Write(" Ruta [Ramas/datos_gimnasio.json]: ");
    var inputPath = Console.ReadLine();
    
    string jsonPath = string.IsNullOrWhiteSpace(inputPath) 
        ? "Ramas/datos_gimnasio.json" 
        : inputPath;
        
    jsonPath = Path.Combine(Environment.CurrentDirectory, jsonPath);

    if (!File.Exists(jsonPath))
    {
        Console.WriteLine($" Error: No se encontró el archivo {jsonPath}");
        return;
    }

    Console.WriteLine("\n  ADVERTENCIA: Esta operación cargará todas las preguntas al índice.");
    Console.Write("¿Deseas continuar? (s/n): ");
    
    var confirm = Console.ReadLine()?.ToLower();
    
    if (confirm == "s" || confirm == "si")
    {
        try
        {
            await chatbotService.LoadQuestionsToVectorStoreAsync(jsonPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Error al cargar las preguntas: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine(" Operación cancelada.");
    }
}


// Función para búsqueda por descripción de texto
static async Task ChatbotImageModeAsync(ChatbotService chatbotService)
{
    Console.WriteLine("\n" + new string('═', 50));
    Console.WriteLine("   BÚSQUEDA POR DESCRIPCIÓN (TEXTO)");
    Console.WriteLine(new string('═', 50));
    Console.WriteLine("\n Escribe una descripción y obtén la imagen más parecida.");
    Console.WriteLine("   Escribe 'salir' para volver al menú principal.\n");

    while (true)
    {
        Console.Write(" Tú: ");
        var userQuestion = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            continue;
        }

        if (userQuestion.ToLower() == "salir")
        {
            Console.WriteLine("\n Volviendo al menú principal...");
            break;
        }

        try
        {
            Console.WriteLine($"\n🔍 Generando embedding para: '{userQuestion}'");
            
            var result = await chatbotService.SearchImageByTextAsync(userQuestion);
            
            if (!result.Success)
            {
                Console.WriteLine($"\n❌ {result.ErrorMessage}\n");
                continue;
            }
            
            Console.WriteLine($"✓ Embedding generado");
            Console.WriteLine($"📊 Resultados encontrados: {result.Matches.Count}");
            
            // Mostrar top resultados
            Console.WriteLine("\n📋 Top resultados:");
            for (int i = 0; i < Math.Min(result.Matches.Count, 5); i++)
            {
                var match = result.Matches[i];
                Console.WriteLine($"  {i + 1}. Distance: {match.Distance:F4} - {match.Description}");
            }
            
            // Mostrar el mejor resultado
            var topMatch = result.Matches[0];
            Console.WriteLine();
            
            if (topMatch.Distance > .5)
            {
                Console.WriteLine($"⚠️  Resultado con baja similitud (distance: {topMatch.Distance:F4})");
            }
            else
            {
                Console.WriteLine($"✓ Imagen encontrada (similitud: {topMatch.SimilarityPercentage:F1}%)");
                Console.WriteLine($"URL: {topMatch.ImageUrl}");
                Console.WriteLine($"Descripción: {topMatch.Description}");
                Console.WriteLine($"Categoría: {topMatch.Category}");
                Console.WriteLine($"Ubicación: {topMatch.Location}");
                Console.WriteLine();
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($" Error: {ex.Message}\n");
        }
    }
}

// Función para búsqueda por URL de imagen
static async Task SearchByImageUrlAsync(ChatbotService chatbotService)
{
    Console.WriteLine("\n" + new string('═', 50));
    Console.WriteLine("   BÚSQUEDA POR URL DE IMAGEN");
    Console.WriteLine(new string('═', 50));
    Console.WriteLine("\n Proporciona la URL de una imagen para encontrar similares.");
    Console.WriteLine("   Escribe 'salir' para volver al menú principal.\n");

    while (true)
    {
        Console.Write(" URL de imagen: ");
        var imageUrl = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            continue;
        }

        if (imageUrl.ToLower() == "salir")
        {
            Console.WriteLine("\n Volviendo al menú principal...");
            break;
        }

        try
        {
            Console.WriteLine($"\n🔍 Descargando imagen desde: {imageUrl}");
            
            var result = await chatbotService.SearchImageByUrlAsync(imageUrl);
            
            if (!result.Success)
            {
                Console.WriteLine($"\n❌ {result.ErrorMessage}\n");
                continue;
            }
            
            Console.WriteLine($"✓ Imagen procesada correctamente");
            Console.WriteLine($"✓ Embedding generado: {(result.Matches.Count > 0 ? "1024 dimensiones" : "")}");
            Console.WriteLine($"📊 Resultados encontrados: {result.Matches.Count}");
            
            // Mostrar top resultados
            Console.WriteLine("\n📋 Top resultados:");
            for (int i = 0; i < Math.Min(result.Matches.Count, 5); i++)
            {
                var match = result.Matches[i];
                Console.WriteLine($"  {i + 1}. Distance: {match.Distance:F4} - {match.Description}");
            }
            
            // Mostrar el mejor resultado
            var topMatch = result.Matches[0];
            Console.WriteLine();
            
            if (topMatch.Distance > .5)
            {
                Console.WriteLine($"⚠️  Resultado con baja similitud (distance: {topMatch.Distance:F4})");
            }
            else
            {
                Console.WriteLine($"✓ Imagen similar encontrada (similitud: {topMatch.SimilarityPercentage:F1}%)");
                Console.WriteLine($"URL: {topMatch.ImageUrl}");
                Console.WriteLine($"Descripción: {topMatch.Description}");
                Console.WriteLine($"Categoría: {topMatch.Category}");
                Console.WriteLine($"Ubicación: {topMatch.Location}");
                Console.WriteLine();
            }
            

        }
        catch (Exception ex)
        {
            Console.WriteLine($" Error: {ex.Message}\n");
        }
    }
}


// Función para test de embeddings
static async Task RunEmbeddingDebugAsync(EmbeddingDebugHelper debugHelper)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("  TEST DE EMBEDDINGS MULTIMODALES");
    Console.WriteLine(new string('═', 60));

    try
    {
        var results = await debugHelper.RunEmbeddingTestAsync();
        
        foreach (var result in results)
        {
            Console.WriteLine($"\n📊 '{result.Text1}' vs '{result.Text2}'");
            Console.WriteLine($"   Similitud: {result.Similarity:F4} | Distancia: {result.Distance:F4}");
            
            var icon = result.Distance switch
            {
                < 0.3 => "✓",
                < 0.6 => "✓",
                < 1.0 => "⚠️ ",
                _ => "❌"
            };
            
            Console.WriteLine($"   {icon} {result.SimilarityLevel}");
        }
        
        Console.WriteLine("\n" + new string('═', 60));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n Error al ejecutar el test: {ex.Message}");
    }
}

// Función para auditar y listar TODOS los vectores cargados en AWS
static async Task AuditVectorIndexAsync(ChatbotService chatbotService, AppSettings settings)
{
    Console.WriteLine("\n" + new string('═', 60));
    Console.WriteLine("  AUDITORÍA COMPLETA DE ÍNDICE VECTORIAL (AWS S3 VECTORS)");
    Console.WriteLine(new string('═', 60));
    Console.WriteLine($" Bucket configurado: {settings.S3Vectors.BucketName}");
    Console.WriteLine($" 1) Índice texto  : {settings.S3Vectors.IndexName}");
    Console.WriteLine($" 2) Índice imagen : {settings.S3Vectors.ImageIndexName}");
    Console.Write(" Selecciona índice a auditar [1]: ");

    var selected = Console.ReadLine();
    var indexName = selected == "2"
        ? settings.S3Vectors.ImageIndexName
        : settings.S3Vectors.IndexName;

    Console.WriteLine("\n Consultando AWS y recorriendo todas las páginas...");

    try
    {
        var audit = await chatbotService.ListAllVectorsAsync(indexName, returnData: false, pageSize: 500);

        Console.WriteLine($"✓ Auditoría finalizada");
        Console.WriteLine($"   - Índice: {audit.IndexName}");
        Console.WriteLine($"   - Páginas leídas: {audit.PagesFetched}");
        Console.WriteLine($"   - Total vectores: {audit.TotalVectors}");
        Console.WriteLine();

        if (audit.TotalVectors == 0)
        {
            Console.WriteLine("⚠️  El índice está vacío.");
            return;
        }

        Console.WriteLine(" Listado completo de vectores:");
        for (int i = 0; i < audit.Vectors.Count; i++)
        {
            var item = audit.Vectors[i];
            var metadataShort = string.Join(" | ", item.Metadata.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            Console.WriteLine($"{i + 1,4}. Key={item.Key} | dims={item.Dimensions} | {metadataShort}");
        }

        // Guardar reporte completo para revisión posterior
        var safeIndexName = new string(indexName.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        var reportDir = Path.Combine(Environment.CurrentDirectory, "Ramas", "diagnostico_vectores");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(
            reportDir,
            $"audit_{safeIndexName}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var json = JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(reportPath, json);

        Console.WriteLine();
        Console.WriteLine($" Reporte JSON completo guardado en:");
        Console.WriteLine($"   {reportPath}");
        Console.WriteLine(" (Incluye todos los vectores y metadata retornada por AWS)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($" Error al auditar índice: {ex.Message}");
    }
}
