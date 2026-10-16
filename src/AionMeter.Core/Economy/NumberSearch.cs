using System.Buffers.Binary;

namespace AionMeter.Core.Economy;

/// <summary>One way a known amount can be written on the wire.</summary>
public sealed record NumberPattern(long Value, string Encoding, byte[] Bytes);

/// <summary>A pattern found in a packet body, at a byte offset.</summary>
public sealed record NumberHit(NumberPattern Pattern, int Offset);

/// <summary>
/// DaevaTrack, milestone 1: finding the packets that carry kinah, energy and claims. Given amounts you know (a price,
/// a balance before or after), lists the byte patterns they can take — little-endian 32 and 64 bit, LEB128 varint and
/// zigzag varint — and where those occur in a packet body.
/// </summary>
public static class NumberSearch
{
    /// <summary>Patterns shorter than this match almost anywhere (a 1-byte varint), so they are left out.</summary>
    public const int MinPatternLength = 2;

    public static IReadOnlyList<NumberPattern> Patterns(IEnumerable<long> values)
    {
        var list = new List<NumberPattern>();
        foreach (var value in values.Distinct())
        {
            var b8 = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(b8, value);
            list.Add(new(value, "i64", b8));
            if (value is >= int.MinValue and <= uint.MaxValue)
            {
                var b4 = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(b4, unchecked((uint)value));
                list.Add(new(value, value < 0 ? "i32" : "u32", b4));
            }
            if (value >= 0) list.Add(new(value, "varint", Varint((ulong)value)));
            list.Add(new(value, "zigzag", Varint(unchecked((ulong)((value << 1) ^ (value >> 63))))));
        }
        // Longest first: an i64 hit hides the u32 hit at the same offset (its first four bytes).
        return list.Where(p => p.Bytes.Length >= MinPatternLength).OrderByDescending(p => p.Bytes.Length).ToList();
    }

    /// <summary>LEB128 unsigned varint of any length (the wire's own reader stops at 32 bits).</summary>
    public static byte[] Varint(ulong value)
    {
        var bytes = new List<byte>();
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }
        bytes.Add((byte)value);
        return bytes.ToArray();
    }

    /// <summary>Every occurrence of every pattern; at one offset only the longest pattern of a value counts.</summary>
    public static List<NumberHit> Find(ReadOnlySpan<byte> body, IReadOnlyList<NumberPattern> patterns)
    {
        var hits = new List<NumberHit>();
        var taken = new HashSet<(long Value, int Offset)>();
        foreach (var p in patterns.OrderByDescending(p => p.Bytes.Length))
        {
            for (var from = 0; from < body.Length;)
            {
                var at = body[from..].IndexOf(p.Bytes);
                if (at < 0) break;
                if (taken.Add((p.Value, from + at))) hits.Add(new(p, from + at));
                from += at + 1;
            }
        }
        hits.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        return hits;
    }
}
