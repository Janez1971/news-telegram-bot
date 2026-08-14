namespace NewsTelegramNotifier;

/// <summary>
/// Rappresenta una singola notizia estratta e normalizzata dalle fonti RSS.
/// </summary>
public record NewsItem(
    string Id,
    string Title,
    string Summary,
    string Url,
    string Source,
    DateTime PublishDate
);