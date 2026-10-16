using AionMeter.Core.Economy;
using AionMeter.Core.Protocol;

namespace AionMeter.Tests.Economy;

/// <summary>Packet bodies cut from recordings (serials zeroed); see docs/economy-notes.md.</summary>
public class KinahParserTests
{
    [Fact]
    public void Unbound_SingleEntry()
    {
        // Sold an item: unbound Kinah 12,974,304 -> 12,974,416.
        var body = Wire.FromHex("01 0100 00000000 001c0100 3f337037 50f9c50000000000 01 0000000000000000000000000000 0b0000");
        Assert.Equal([new CurrencyBalance(Currency.Kinah, 12_974_416)], KinahParser.Parse(body));
    }

    [Fact]
    public void Bound_NextToAnItem()
    {
        // Bought 10 items for 250,000 bound Kinah: bound 336,000, item 1df78244 now 13.
        var body = Wire.FromHex(
            "02 0100 00000000 00720000 21337037 8020050000000000 0101000000080000000000000000000000004200" +
            "0100 00000000 00270100 4482f71d 0d00000000000000 0123000000008099a67da20100002f0000");
        Assert.Equal([new CurrencyBalance(Currency.BoundKinah, 336_000)], KinahParser.Parse(body));
    }

    [Fact]
    public void Bound_AfterAnotherCurrency()
    {
        // Achievement reward: currency 37703337 = 333,946, then bound Kinah 348,000.
        var body = Wire.FromHex(
            "02 0100 00000000 00850000 37337037 7a14050000000000 0109000000000000000000000000000002000000" +
            "0100 00000000 00970000 21337037 604f050000000000 010100000000000000000000000000000200 00");
        Assert.Equal([new CurrencyBalance(Currency.BoundKinah, 348_000)], KinahParser.Parse(body));
    }

    [Fact]
    public void OtherItems_NoKinah() =>
        Assert.Empty(KinahParser.Parse(Wire.FromHex("01 0100 00000000 00270100 4482f71d 0d00000000000000 00")));
}
