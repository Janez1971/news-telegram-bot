using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace NewsTelegramNotifier;

public class NewsWorker : BackgroundService
{
    private readonly AppState _state;
    private readonly NewsScannerService _scanner;
    private readonly ITelegramBotClient _bot;
    private readonly string _chatId;

    public NewsWorker(AppState state, NewsScannerService scanner, ITelegramBotClient bot, string chatId)
    {
        _state = state;
        _scanner = scanner;
        _bot = bot;
        _chatId = chatId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("🚀 Motore di scansione notizie avviato.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.IsActive())
            {
                try
                {
                    var news = await _scanner.ScanNewArticlesAsync();

                    // Se non ci sono novità rispetto alle precedenti scansioni, non invia nulla!
                    if (news.Count > 0)
                    {
                        foreach (var item in news)
                        {
                            string msg = $"📰 *{EscapeMarkdown(item.Title)}*\n\n" +
                                         $"📌 *Fonte:* _{EscapeMarkdown(item.Source)}_ | 🕒 {item.PublishDate.ToLocalTime():HH:mm}\n\n" +
                                         $"📝 *Sintesi:*\n{EscapeMarkdown(item.Summary)}\n\n" +
                                         $"🔗 [Leggi l'articolo completo]({item.Url})";

                            await _bot.SendMessage(_chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: stoppingToken);
                            
                            // Registra la notizia come già vista per non re-inviarla
                            _state.SeenNewsIds.TryAdd(item.Id, 0);

                            // Pausa di 1 secondo tra messaggi per rispettare i rate-limit di Telegram
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERRORE WORKER] {ex.Message}");
                }
            }

            // Attesa dinamica in base all'intervallo configurato
            int waitMinutes = _state.IntervalMinutes;
            await Task.Delay(TimeSpan.FromMinutes(waitMinutes), stoppingToken);
        }
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
    }
}