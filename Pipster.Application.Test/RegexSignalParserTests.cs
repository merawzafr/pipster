using Pipster.Application.Parsing;
using Pipster.Shared.Enums;

namespace Pipster.Application.Test;

[TestFixture]
public class RegexSignalParserTests
{
    private readonly RegexSignalParser _parser = new();

    [Test]
    public void TryParse_BuySignalFormat1_ShouldParseCorrectly()
    {
        // Arrange
        var signal = @"Buy #XAUUSD #GOLD 3750.5-3747
SL 3740
TP 3753
TP 3755
TP 3760
TP 3765
TP 3770
Follow Proper Money Management ‼️";

        // Regex pattern for format 1: Buy #SYMBOL entry-range SL value TP values
        var regex = @"(?<side>buy|sell)#?(?<symbol>[A-Z]{6,}).*?(?<entry>\d+\.?\d*)-?\d*\.?\d*.*?sl(?<sl>\d+\.?\d*).*?tp(?<tp1>\d+\.?\d*).*?tp(?<tp2>\d+\.?\d*).*?tp(?<tp3>\d+\.?\d*).*?tp(?<tp4>\d+\.?\d*).*?tp(?<tp5>\d+\.?\d*)";

        // Act
        var result = _parser.TryParse(regex, signal);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbol, Is.EqualTo("XAUUSD"));
        Assert.That(result.Side, Is.EqualTo(OrderSide.Buy));
        Assert.That(result.Entry, Is.EqualTo(3750.5m));
        Assert.That(result.StopLoss, Is.EqualTo(3740m));
        Assert.That(result.TakeProfits, Has.Count.EqualTo(5));
        Assert.That(result.TakeProfits, Contains.Item(3753m));
        Assert.That(result.TakeProfits, Contains.Item(3755m));
        Assert.That(result.TakeProfits, Contains.Item(3760m));
        Assert.That(result.TakeProfits, Contains.Item(3765m));
        Assert.That(result.TakeProfits, Contains.Item(3770m));
        Assert.That(result.SeenAt, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow));
    }

    [Test]
    public void TryParse_SellSignalFormat2_ShouldParseCorrectly()
    {
        // Arrange
        var signal = @"XAUUSD SHORT
Entry: 3740.775
Target 1: 3735.775
Target 2: 3728.775
Target 3: 3715.775
Stoploss: 3760.775";

        // Regex pattern for format 2: SYMBOL SIDE Entry: value Target N: value Stoploss: value
        var regex = @"(?<symbol>[A-Z]{6,})(?<side>short|long|buy|sell)entry:(?<entry>\d+\.?\d*)target1:(?<tp1>\d+\.?\d*)target2:(?<tp2>\d+\.?\d*)target3:(?<tp3>\d+\.?\d*)stoploss:(?<sl>\d+\.?\d*)";

        // Act
        var result = _parser.TryParse(regex, signal);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbol, Is.EqualTo("XAUUSD"));
        Assert.That(result.Side, Is.EqualTo(OrderSide.Sell));
        Assert.That(result.Entry, Is.EqualTo(3740.775m));
        Assert.That(result.StopLoss, Is.EqualTo(3760.775m));
        Assert.That(result.TakeProfits, Has.Count.EqualTo(3));
        Assert.That(result.TakeProfits, Contains.Item(3735.775m));
        Assert.That(result.TakeProfits, Contains.Item(3728.775m));
        Assert.That(result.TakeProfits, Contains.Item(3715.775m));
        Assert.That(result.SeenAt, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow));
    }

    [Test]
    public void TryParse_InvalidRegex_ShouldReturnNull()
    {
        // Arrange
        var signal = "Buy #XAUUSD 3750.5";
        var invalidRegex = "[invalid regex";

        // Act
        var result = _parser.TryParse(invalidRegex, signal);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParse_EmptySignal_ShouldReturnNull()
    {
        // Arrange
        var regex = @"(?<symbol>[A-Z]+)";
        var emptySignal = "";

        // Act
        var result = _parser.TryParse(regex, emptySignal);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParse_NoMatch_ShouldReturnNull()
    {
        // Arrange
        var signal = "This is not a trading signal";
        var regex = @"(?<symbol>[A-Z]{6,})(?<side>buy|sell)";

        // Act
        var result = _parser.TryParse(regex, signal);

        // Assert
        Assert.That(result, Is.Null);
    }
}