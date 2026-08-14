using NewsTelegramNotifier;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Recupera credenziali da variabili d'ambiente o fallback per test
string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "8786192362:AAF2uIijb4PNkjSTmZThDrWwA6wvM-EZKGI";
string chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "1113321581";

// Iniezione dipendenze
builder.Services.AddSingleton<AppState>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<NewsScannerService>();
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

// Handler per i comandi Telegram
builder.Services.AddSingleton<TelegramBotHandler>(sp =>
    new TelegramBotHandler(
        sp.GetRequiredService<ITelegramBotClient>(),
        sp.GetRequiredService<AppState>(),
        chatId
    ));

// Worker in background per la scansione automatica
builder.Services.AddHostedService(sp =>
    new NewsWorker(
        sp.GetRequiredService<AppState>(),
        sp.GetRequiredService<NewsScannerService>(),
        sp.GetRequiredService<ITelegramBotClient>(),
        chatId
    ));

var app = builder.Build();

// Avvio ascolto Telegram
var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
var handler = app.Services.GetRequiredService<TelegramBotHandler>();
botClient.StartReceiving(handler.HandleUpdateAsync, handler.HandlePollingErrorAsync);

// Endpoint di stato per Google Cloud Run (Health Check)
app.MapGet("/", (AppState state) => new
{
    status = "Online",
    active = state.IsActive(),
    intervalMinutes = state.IntervalMinutes,
    topicsCount = state.GetTopics().Count,
    processedNews = state.SeenNewsIds.Count
});

app.Run();