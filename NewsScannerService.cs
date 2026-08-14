using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace NewsTelegramNotifier;

public record NewsArticle(string Id, string Title, string Summary, string Url, string Source, DateTime PublishDate);

public class NewsScannerService
{
    private readonly AppState _state;
    private readonly HttpClient _httpClient;

    // Elenco fonti accreditate
    private readonly List<(string SourceName, string FeedUrl)> _trustedFeeds = new()
    {
        ("Il Sole 24 Ore - Economia", "https://www.ilsole24ore.com/rss/economia.xml"),
        ("Il Sole 24 Ore - Finanza", "https://www.ilsole24ore.com/rss/finanza-mercati.xml"),
        ("ANSA - Economia", "https://www.ansa.it/sito/notizie/economia/economia_rss.xml"),
        ("CNBC - Markets", "https://search.cnbc.com/rs/search/combinedList/view.xml?partnerId=wrss01&id=10000664")
    };

    // Parole chiave per il topic finanziario pre-settato
    private readonly string[] _marketKeywords = new[]
    {
        "borsa", "borse", "inflazione", "bce", "fed", "wall street", "spread", "btp",
        "tassi", "petrolio", "pil", "recessione", "mercati", "trimestrale", "banca centrale",
        "nasdaq", "dow jones", "ftse", "dividendo", "dollaro", "euro"
    };

    public NewsScannerService(AppState state, HttpClient httpClient)
    {
        _state = state;
        _httpClient = httpClient;
    }

    public async Task<List<NewsArticle>> ScanNewArticlesAsync()
    {
        var matchedArticles = new List<NewsArticle>();
        var currentTopics = _state.GetTopics();

        if (currentTopics.Count == 0) return matchedArticles;

        foreach (var (sourceName, feedUrl) in _trustedFeeds)
        {
            try
            {
                using var response = await _httpClient.GetStreamAsync(feedUrl);
                using var reader = XmlReader.Create(response);
                var feed = SyndicationFeed.Load(reader);

                if (feed == null) continue;

                foreach (var item in feed.Items.Take(15)) // Controlla le ultime 15 per fonte
                {
                    string id = item.Id ?? item.Links.FirstOrDefault()?.Uri.ToString() ?? item.Title.Text;

                    // Se già inviata in precedenza, salta
                    if (_state.SeenNewsIds.ContainsKey(id)) continue;

                    string title = item.Title?.Text ?? "";
                    string rawSummary = item.Summary?.Text ?? "";
                    string cleanSummary = StripHtml(rawSummary);
                    string url = item.Links.FirstOrDefault()?.Uri.ToString() ?? "";
                    DateTime pubDate = item.PublishDate.UtcDateTime != DateTime.MinValue 
                        ? item.PublishDate.UtcDateTime 
                        : DateTime.UtcNow;

                    string fullText = $"{title} {cleanSummary}";

                    // Verifica se matcha uno degli argomenti attivi
                    if (MatchesAnyTopic(fullText, currentTopics))
                    {
                        matchedArticles.Add(new NewsArticle(id, title, cleanSummary, url, sourceName, pubDate));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVVISO] Errore nel feed {sourceName}: {ex.Message}");
            }
        }

        return matchedArticles;
    }

    private bool MatchesAnyTopic(string text, List<string> topics)
    {
        foreach (var topic in topics)
        {
            if (topic.Equals(AppState.DefaultTopic, StringComparison.OrdinalIgnoreCase))
            {
                // Controllo euristico parole chiave di borsa/finanza
                if (_marketKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            else
            {
                // Match per parola/frase dell'argomento inserito dall'utente
                if (text.Contains(topic, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Nessun sommario disponibile.";
        string noHtml = Regex.Replace(input, "<.*?>", string.Empty);
        return Regex.Replace(noHtml, @"\s+", " ").Trim();
    }
}