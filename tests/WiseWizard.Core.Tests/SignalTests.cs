using WiseWizard.Core.Models;

namespace WiseWizard.Core.Tests;

public class SignalTests
{
    [Theory]
    [InlineData(Signal.Hold, "hold")]
    [InlineData(Signal.Attention, "attention")]
    [InlineData(Signal.Review, "review")]
    public void ToToken_MapsToContractToken(Signal signal, string expected)
    {
        Assert.Equal(expected, signal.ToToken());
    }

    [Theory]
    [InlineData(Signal.Hold, "🟢")]
    [InlineData(Signal.Attention, "🟡")]
    [InlineData(Signal.Review, "🔴")]
    public void ToEmoji_MapsToTrafficLight(Signal signal, string expected)
    {
        Assert.Equal(expected, signal.ToEmoji());
    }

    [Theory]
    [InlineData("hold", Signal.Hold)]
    [InlineData("ATTENTION", Signal.Attention)]
    [InlineData(" review ", Signal.Review)]
    public void ParseSignal_ParsesTokenCaseAndSpaceInsensitive(string token, Signal expected)
    {
        Assert.Equal(expected, SignalExtensions.ParseSignal(token));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseSignal_RejectsUnknownToken(string? token)
    {
        Assert.Throws<ArgumentException>(() => SignalExtensions.ParseSignal(token));
    }

    [Fact]
    public void ToToken_RoundTripsThroughParse()
    {
        foreach (var signal in Enum.GetValues<Signal>())
        {
            Assert.Equal(signal, SignalExtensions.ParseSignal(signal.ToToken()));
        }
    }
}
