using System.Collections.Concurrent;

namespace NewsTelegramNotifier;

public class AppState
{
    private readonly object _lock = new();

    // Argomento pre-impostato
    public const string DefaultTopic = "Notizie che possono in qualche modo e con buona probabilità influenzare le borse";

    public HashSet<string> Topics { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        DefaultTopic
    };

    // Intervallo di scansione in minuti (default: 15)
    public int IntervalMinutes { get; set; } = 15;

    // Sospensione a tempo o indeterminata
    public bool IsSuspendedIndefinitely { get; set; } = false;
    public DateTime? SuspendedUntilUtc { get; set; } = null;

    // Cache degli ID/URL notizie già inviate per prevenire duplicati
    public ConcurrentDictionary<string, byte> SeenNewsIds { get; } = new();

    public bool IsActive()
    {
        lock (_lock)
        {
            if (IsSuspendedIndefinitely) return false;
            if (SuspendedUntilUtc.HasValue)
            {
                if (DateTime.UtcNow < SuspendedUntilUtc.Value) return false;
                // Tempo scaduto: riattivazione automatica
                SuspendedUntilUtc = null;
            }
            return true;
        }
    }

    public void AddTopics(IEnumerable<string> newTopics)
    {
        lock (_lock)
        {
            foreach (var topic in newTopics)
            {
                var clean = topic.Trim();
                if (!string.IsNullOrWhiteSpace(clean))
                    Topics.Add(clean);
            }
        }
    }

    public void RemoveTopics(IEnumerable<string> topicsToRemove)
    {
        lock (_lock)
        {
            foreach (var topic in topicsToRemove)
            {
                Topics.Remove(topic.Trim());
            }
        }
    }

    public List<string> GetTopics()
    {
        lock (_lock)
        {
            return Topics.ToList();
        }
    }
}