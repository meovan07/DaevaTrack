using System.Globalization;
using AionMeter.Core.Capture;
using AionMeter.Core.Economy;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;

/// <summary>
/// DaevaTrack, milestone 1: tools for finding the economy packets (kinah, Odyle Energy, claims, inventory) in a
/// recording made with <c>live [seconds] --record</c>. Nothing here is used by the app.
/// </summary>
static class EconomyCommands
{
    /// <summary>Opcodes upstream already decodes; <c>economy-dump</c> leaves them out unless asked for <c>--all</c>.</summary>
    private static readonly HashSet<ushort> Decoded =
    [
        Opcodes.Heartbeat, Opcodes.SelfInfo, Opcodes.PlayerInfo, Opcodes.PlayerInfoOld, Opcodes.Spawn, Opcodes.Death,
        Opcodes.MapLoad, Opcodes.Teleport, Opcodes.Cast, Opcodes.HitNotice, Opcodes.Damage, Opcodes.Dot,
        Opcodes.RemainHp, Opcodes.InstanceTimer, Opcodes.KillCredit, Opcodes.BattleToggle, Opcodes.HpMp,
        Opcodes.PartyRoster, Opcodes.FieldBossList,
    ];

    private static string Clock(long t) => $"{DateTimeOffset.FromUnixTimeMilliseconds(t).ToLocalTime():HH:mm:ss.fff}";

    /// <summary>
    /// <c>findnum &lt;file.pcap&gt; &lt;number&gt;...</c>: every packet (and LZ4 bundle carried inside one) holding one of
    /// the numbers as i64, u32/i32, varint or zigzag varint, then a count per opcode and encoding.
    /// </summary>
    public static int FindNumbers(GameData data, string path, IEnumerable<string> numbers)
    {
        var values = new List<long>();
        foreach (var n in numbers)
        {
            if (!long.TryParse(n.Replace(",", "").Replace("_", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            {
                Console.WriteLine($"Not a whole number: {n}");
                return 1;
            }
            values.Add(v);
        }
        var patterns = NumberSearch.Patterns(values);
        var replay = new PcapReplaySource(data, path, realtime: false);
        var shown = 0;
        var perKind = new Dictionary<(ushort Op, long Value, string Encoding, bool Embedded), int>();

        void Report(long t, ushort op, ReadOnlySpan<byte> body, List<NumberHit> hits, string where)
        {
            foreach (var h in hits)
            {
                var key = (op, h.Pattern.Value, h.Pattern.Encoding, where.Length > 0);
                perKind[key] = perKind.GetValueOrDefault(key) + 1;
                if (shown++ >= 200) continue;
                var from = Math.Max(0, h.Offset - 24);
                var to = Math.Min(body.Length, h.Offset + h.Pattern.Bytes.Length + 24);
                Console.WriteLine($"{Clock(t)} {Opcodes.Name(op),-12} len={body.Length,5} at={h.Offset,4} {h.Pattern.Value} as {h.Pattern.Encoding}{where}");
                Console.WriteLine($"   near: {Wire.Hex(body[from..to], 96)}");
            }
        }

        replay.Pipeline.Parser.Tap = (t, op, body) =>
        {
            if (op == Opcodes.Heartbeat) return;
            var hits = NumberSearch.Find(body, patterns);
            if (hits.Count > 0) Report(t, op, body, hits, "");
            // LZ4 bundles carried inside other packets (not opened by the framing), as in the find command.
            for (var i = 1; i + 8 < body.Length; i++)
            {
                if (body[i] != 0xFF || body[i + 1] != 0xFF) continue;
                var raw = (int)Wire.U32(body, i + 2);
                if (raw is <= 0 or > 1 << 22) continue;
                var buf = new byte[raw];
                var n = K4os.Compression.LZ4.LZ4Codec.Decode(body[(i + 6)..], buf);
                if (n <= 0) continue;
                var inner = NumberSearch.Find(buf.AsSpan(0, n), patterns);
                if (inner.Count > 0) Report(t, op, buf.AsSpan(0, n), inner, $" (bundle at {i})");
            }
        };
        replay.RunToEnd();

        Console.WriteLine($"\n{shown} matches{(shown > 200 ? " (first 200 shown)" : "")}");
        foreach (var ((op, value, encoding, embedded), count) in perKind.OrderByDescending(x => x.Value))
            Console.WriteLine($"   {Opcodes.Name(op),-12} {value,14} {encoding,-7}{(embedded ? " in bundle" : ""),-10} {count}");
        return 0;
    }

    /// <summary>
    /// <c>economy-dump &lt;file.pcap&gt; [HH:mm:ss [seconds]] [--all]</c>: packets upstream does not decode, optionally only
    /// those within <c>seconds</c> (default 15) of a local time you noted during the recording.
    /// </summary>
    public static int Dump(GameData data, string path, string[] a)
    {
        var all = a.Contains("--all");
        var rest = a.Where(x => x != "--all").ToArray();
        TimeSpan? at = rest.Length > 0 && TimeSpan.TryParse(rest[0], CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var window = TimeSpan.FromSeconds(rest.Length > 1 && int.TryParse(rest[1], out var s) ? s : 15);
        var counts = new Dictionary<ushort, int>();
        var replay = new PcapReplaySource(data, path, realtime: false);
        replay.Pipeline.Parser.Tap = (t, op, body) =>
        {
            if (op == Opcodes.Heartbeat || (!all && Decoded.Contains(op))) return;
            if (at is { } mark && (DateTimeOffset.FromUnixTimeMilliseconds(t).ToLocalTime().TimeOfDay - mark).Duration() > window) return;
            counts[op] = counts.GetValueOrDefault(op) + 1;
            Console.WriteLine($"{Clock(t)} {Opcodes.Name(op),-12} [{body.Length,5}] {Wire.Hex(body, 96)}");
        };
        replay.RunToEnd();
        Console.WriteLine("\n-- opcodes");
        foreach (var (op, n) in counts.OrderByDescending(x => x.Value)) Console.WriteLine($"   {Opcodes.Name(op),-12} {n}");
        return 0;
    }
}
