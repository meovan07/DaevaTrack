using AionMeter.Core.Economy;

namespace AionMeter.App.Services;

/// <summary>
/// DaevaTrack: the latest Kinah balances the server sent, and how much each moved since the first one seen this run.
/// Written from the capture thread, read by the UI.
/// </summary>
public sealed class KinahBalances
{
    private readonly object _gate = new();
    private readonly Dictionary<Currency, (long First, long Now)> _seen = new();

    /// <summary>Bumped on every change, so the window redraws only when needed.</summary>
    public int Version { get; private set; }

    public void On(KinahEvent e)
    {
        lock (_gate)
        {
            _seen[e.Currency] = _seen.TryGetValue(e.Currency, out var v) ? (v.First, e.Amount) : (e.Amount, e.Amount);
            Version++;
        }
    }

    /// <summary>Current balance and change this session, or null until the server has sent one.</summary>
    public (long Amount, long Change)? Get(Currency currency)
    {
        lock (_gate) return _seen.TryGetValue(currency, out var v) ? (v.Now, v.Now - v.First) : null;
    }
}
