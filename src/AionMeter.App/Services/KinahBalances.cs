using AionMeter.Core.Economy;

namespace AionMeter.App.Services;

/// <summary>
/// DaevaTrack: the latest Kinah balances the server sent, per character, and how much each moved since the first one
/// seen for that character this run. Written from the capture thread, read by the UI.
/// </summary>
public sealed class KinahBalances
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Character, Currency Currency), (long First, long Now)> _seen = new();
    // Balances from a map load's inventory wait for the character record that follows it.
    private readonly List<KinahEvent> _pending = new();
    private string? _character;

    /// <summary>Bumped on every change, so the view redraws only when needed.</summary>
    public int Version { get; private set; }

    /// <summary>The character the balances are shown for (null until the game has named it).</summary>
    public string? Character
    {
        get { lock (_gate) return _character; }
    }

    public void On(KinahEvent e)
    {
        lock (_gate)
        {
            if (e.FromInventory || _character is null) _pending.Add(e);
            else Apply(_character, e);
        }
    }

    /// <summary>The game named our character (every map load): balances waiting for it are its own.</summary>
    public void SetCharacter(string name)
    {
        lock (_gate)
        {
            if (name != _character) Version++;
            _character = name;
            foreach (var e in _pending) Apply(name, e);
            _pending.Clear();
        }
    }

    private void Apply(string character, KinahEvent e)
    {
        var key = (character, e.Currency);
        _seen[key] = _seen.TryGetValue(key, out var v) ? (v.First, e.Amount) : (e.Amount, e.Amount);
        Version++;
    }

    /// <summary>The current character's balance and change this session, or null until the server has sent one.</summary>
    public (long Amount, long Change)? Get(Currency currency)
    {
        lock (_gate)
            return _character is not null && _seen.TryGetValue((_character, currency), out var v) ? (v.Now, v.Now - v.First) : null;
    }
}
