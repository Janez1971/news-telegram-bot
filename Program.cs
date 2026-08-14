using NewsTelegramNotifier;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

// Recupera credenziali da variabili d'ambiente o fallback per test
string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "INSERISCI_IL_TUO_BOT_TOKEN";
string chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "INSERISCI_IL_TUO_CHAT_ID";

// Iniezione delle dipendenze
builder.Services.AddSingleton<AppState>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<NewsScannerService>();
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

// Registrazione handler comandi Telegram
builder.Services.AddSingleton<TelegramBotHandler>(sp =>
    new TelegramBotHandler(
        sp.GetRequiredService<ITelegramBotClient>(),
        sp.GetRequiredService<AppState>(),
        chatId
    ));

// Background Worker per la scansione
builder.Services.AddHostedService(sp =>
    new NewsWorker(
        sp.GetRequiredService<AppState>(),
        sp.GetRequiredService<NewsScannerService>(),
        sp.GetRequiredService<ITelegramBotClient>(),
        chatId
    ));

var host = builder.Build();

// Avvio ascolto comandi Telegram in background
var botClient = host.Services.GetRequiredService<ITelegramBotClient>();
var handler = host.Services.GetRequiredService<TelegramBotHandler>();
botClient.StartReceiving(handler.HandleUpdateAsync, handler.HandlePollingErrorAsync);

await host.RunAsync();