using System.Collections.Concurrent;

namespace AgentScope.Api;

/// <summary>
/// Thread-safe in-memory store for SSH login challenges.
/// Each nonce expires after 5 minutes.
/// </summary>
public sealed class SshChallengeStore
{
    private sealed record Entry(string Nonce, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public string Issue(string identity)
    {
        PurgeExpired();
        var nonce = $"agentscope-{Guid.NewGuid():N}";
        _store[identity] = new Entry(nonce, DateTimeOffset.UtcNow.AddMinutes(5));
        return nonce;
    }

    public bool TryConsume(string identity, out string nonce)
    {
        if (_store.TryRemove(identity, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            nonce = entry.Nonce;
            return true;
        }
        nonce = "";
        return false;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _store.Keys)
            if (_store.TryGetValue(key, out var e) && e.ExpiresAt <= now)
                _store.TryRemove(key, out _);
    }
}
