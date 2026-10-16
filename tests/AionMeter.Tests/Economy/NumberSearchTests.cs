using AionMeter.Core.Economy;
using AionMeter.Core.Protocol;

namespace AionMeter.Tests.Economy;

public class NumberSearchTests
{
    [Theory]
    [InlineData(6550UL, "9633")]
    [InlineData(300UL, "AC02")]
    [InlineData(50000UL, "D08603")]
    public void Varint_IsLeb128(ulong value, string hex) =>
        Assert.Equal(Wire.FromHex(hex), NumberSearch.Varint(value));

    [Fact]
    public void Patterns_LeaveOutOneByteEncodings()
    {
        var patterns = NumberSearch.Patterns([40]);
        Assert.DoesNotContain(patterns, p => p.Encoding is "varint" or "zigzag");
        Assert.Contains(patterns, p => p.Encoding == "u32");
    }

    [Fact]
    public void Find_PrefersTheLongestEncodingAtAnOffset()
    {
        // 6550 as i64 at offset 2, then as a varint at offset 12.
        var body = Wire.FromHex("0102 9619000000000000 FFFF 9633 00");
        var hits = NumberSearch.Find(body, NumberSearch.Patterns([6550]));
        Assert.Equal(2, hits.Count);
        Assert.Equal(("i64", 2), (hits[0].Pattern.Encoding, hits[0].Offset));
        Assert.Equal(("varint", 12), (hits[1].Pattern.Encoding, hits[1].Offset));
    }

    [Fact]
    public void Find_NegativeAmountAsSignedInt()
    {
        var body = Wire.FromHex("00 6AE6FFFF 00");
        var hits = NumberSearch.Find(body, NumberSearch.Patterns([-6550]));
        Assert.Contains(hits, h => h.Pattern.Encoding == "i32" && h.Offset == 1);
    }
}
