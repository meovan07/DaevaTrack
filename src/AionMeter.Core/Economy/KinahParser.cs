using System.Buffers.Binary;

namespace AionMeter.Core.Economy;

/// <summary><see cref="StorageKinah"/> is unbound Kinah in the account's server storage, shared by its characters.</summary>
public enum Currency { Kinah, BoundKinah, StorageKinah }

/// <summary>A currency's new total, as the server sends it (absolute, never a delta).</summary>
public readonly record struct CurrencyBalance(Currency Currency, long Amount);

/// <summary>A Kinah balance from the server; the combat engine ignores it.</summary>
/// <param name="FromInventory">From the full inventory sent on every map load, a few seconds <i>before</i> the
/// character's own record (<c>SelfInfo</c>): it belongs to the character announced next, not the one before.</param>
public sealed record KinahEvent(long TimeMs, Currency Currency, long Amount, bool FromInventory = false) : Events.GameEvent(TimeMs);

/// <summary>
/// DaevaTrack, milestone 1: Kinah from packet <c>1b 56</c>, the server's "these item stacks changed" message, and
/// from <c>11 56</c>, the full inventory at login (see docs/economy-notes.md). Kinah is an item there: a 4-byte item id followed by the new total as i64. The entry layout
/// around it is not fully known yet (entries are ~40 bytes with a variable tail), so this looks for the known currency
/// ids directly rather than walking entries.
/// </summary>
public static class KinahParser
{
    /// <summary>Opcodes as the two bytes appear on the wire, like <see cref="Protocol.Opcodes"/>.</summary>
    public const ushort ItemStacks = 0x1B56;

    /// <summary>The whole inventory, sent once at login with the same entry layout: the starting balances.</summary>
    public const ushort Inventory = 0x1156;

    public static bool Carries(ushort opcode) => opcode is ItemStacks or Inventory;

    public const uint KinahId = 0x3770333F;
    public const uint BoundKinahId = 0x37703321;

    /// <summary>The byte after the amount says where the stack is: the character's bag, or the server storage.</summary>
    private const byte InStorage = 0x02;

    /// <summary>More Kinah than this is not a balance but a false match.</summary>
    private const long MaxAmount = 1_000_000_000_000;

    public static List<CurrencyBalance> Parse(ReadOnlySpan<byte> body)
    {
        var found = new List<CurrencyBalance>();
        // The id sits 11 bytes into an entry (after a 2-byte tag and two u32s), so it never starts before offset 11.
        for (var i = 11; i + 12 <= body.Length; i++)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(body[i..]);
            var stored = i + 12 < body.Length && body[i + 12] == InStorage;
            Currency currency;
            if (id == KinahId) currency = stored ? Currency.StorageKinah : Currency.Kinah;
            else if (id == BoundKinahId) currency = Currency.BoundKinah;
            else continue;
            var amount = BinaryPrimitives.ReadInt64LittleEndian(body[(i + 4)..]);
            if (amount is < 0 or > MaxAmount) continue;
            found.Add(new(currency, amount));
            i += 11;
        }
        return found;
    }
}
