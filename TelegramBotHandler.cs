using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NewsTelegramNotifier;

public class TelegramBotHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppState _state;
    private readonly string _authorizedChatId;

    public TelegramBotHandler(ITelegramBotClient bot, AppState state, string authorizedChatId)
    {
        _bot = bot;
        _state = state;
        _authorizedChatId = authorizedChatId;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message || update.Message?.Text is null) return;

        var message = update.Message;
        string chatId = message.Chat.Id.ToString();

        // Controllo di sicurezza: rispondi solo al proprietario
        if (chatId != _authorizedChatId) return;

        string text = message.Text.Trim();
        string[] parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].ToLowerInvariant();
        string args = parts.Length > 1 ? parts[1].Trim() : "";

        switch (command)
        {
            case "/start":
            case "/help":
                await SendHelpAsync(ct);
                break;

            case "/argomenti":
                var list = _state.GetTopics();
                var response = list.Count == 0 
                    ? "⚠️ Nessun argomento attivo." 
                    : "📋 *Argomenti attualmente monitorati:*\n\n" + string.Join("\n", list.Select((t, i) => $"{i + 1}. `{t}`"));
                await _bot.SendMessage(chatId, response, parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case "/aggiungi":
                if (string.IsNullOrWhiteSpace(args))
                {
                    await _bot.SendMessage(chatId, "❌ Specifica uno o più argomenti separati da virgola.\n_Es: /aggiungi semiconduttori, intelligenza artificiale_", parseMode: ParseMode.Markdown, cancellationToken: ct);
                    return;
                }
                var toAdd = args.Split(',', StringSplitOptions.RemoveEmptyEntries);
                _state.AddTopics(toAdd);
                await _bot.SendMessage(chatId, $"✅ Aggiunti {toAdd.Length} argomenti!", cancellationToken: ct);
                break;

            case "/rimuovi":
                if (string.IsNullOrWhiteSpace(args))
                {
                    await _bot.SendMessage(chatId, "❌ Specifica uno o più argomenti da rimuovere separati da virgola.", cancellationToken: ct);
                    return;
                }
                var toRemove = args.Split(',', StringSplitOptions.RemoveEmptyEntries);
                _state.RemoveTopics(toRemove);
                await _bot.SendMessage(chatId, $"🗑️ Rimossi gli argomenti indicati.", cancellationToken: ct);
                break;

            case "/intervallo":
                if (int.TryParse(args, out int minutes) && minutes >= 1)
                {
                    _state.IntervalMinutes = minutes;
                    await _bot.SendMessage(chatId, $"⏱️ Intervallo di scansione impostato a *{minutes} minuti*.", parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                else
                {
                    await _bot.SendMessage(chatId, "❌ Inserisci un numero valido di minuti (es. `/intervallo 30`).", parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                break;

            case "/sospendi":
                if (int.TryParse(args, out int pauseMin) && pauseMin >= 1)
                {
                    _state.IsSuspendedIndefinitely = false;
                    _state.SuspendedUntilUtc = DateTime.UtcNow.AddMinutes(pauseMin);
                    await _bot.SendMessage(chatId, $"⏸️ Scansione sospesa per *{pauseMin} minuti* (riattivazione automatica alle {DateTime.Now.AddMinutes(pauseMin):HH:mm}).", parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                else
                {
                    await _bot.SendMessage(chatId, "❌ Inserisci i minuti di sospensione (es. `/sospendi 60`).", parseMode: ParseMode.Markdown, cancellationToken: ct);
                }
                break;

            case "/stop":
            case "/pausa":
                _state.IsSuspendedIndefinitely = true;
                _state.SuspendedUntilUtc = null;
                await _bot.SendMessage(chatId, "🛑 *Scansione sospesa a tempo indeterminato.* Usa `/riattiva` per ripartire.", parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case "/riattiva":
            case "/resume":
                _state.IsSuspendedIndefinitely = false;
                _state.SuspendedUntilUtc = null;
                await _bot.SendMessage(chatId, "▶️ *Scansione riattivata con successo!*", parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case "/stato":
                string stato = _state.IsSuspendedIndefinitely 
                    ? "🔴 Sospeso a tempo indeterminato"
                    : _state.SuspendedUntilUtc.HasValue 
                        ? $"🟡 Sospeso fino alle {_state.SuspendedUntilUtc.Value.ToLocalTime():HH:mm:ss}" 
                        : "🟢 Attivo e in scansione regolare";

                string statusMsg = $"📊 *Stato del Servizio:*\n" +
                                   $"• Stato: {stato}\n" +
                                   $"• Intervallo: ogni *{_state.IntervalMinutes} min*\n" +
                                   $"• Argomenti attivi: *{_state.GetTopics().Count}*\n" +
                                   $"• Notizie in memoria: *{_state.SeenNewsIds.Count}*";
                await _bot.SendMessage(chatId, statusMsg, parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            default:
                await _bot.SendMessage(chatId, "Comando non riconosciuto. Invia /help per la lista comandi.", cancellationToken: ct);
                break;
        }
    }

    private async Task SendHelpAsync(CancellationToken ct)
    {
        string help = "🤖 *Comandi disponibili per il News Scanner:*\n\n" +
                      "📋 `/argomenti` - Visualizza la lista argomenti attivi\n" +
                      "➕ `/aggiungi <arg1>, <arg2>` - Aggiunge argomenti\n" +
                      "➖ `/rimuovi <arg1>, <arg2>` - Rimuove argomenti\n" +
                      "⏱️ `/intervallo <min>` - Cambia intervallo di scansione\n" +
                      "⏸️ `/sospendi <min>` - Sospende la scansione per X minuti\n" +
                      "🛑 `/stop` - Sospende a tempo indeterminato\n" +
                      "▶️ `/riattiva` - Riattiva la scansione\n" +
                      "📊 `/stato` - Mostra lo stato attuale del sistema";
        await _bot.SendMessage(_authorizedChatId, help, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ERRORE TELEGRAM] {exception.Message}");
        return Task.CompletedTask;
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[ERRORE TELEGRAM] {source}: {exception.Message}");
        return Task.CompletedTask;
    }
}