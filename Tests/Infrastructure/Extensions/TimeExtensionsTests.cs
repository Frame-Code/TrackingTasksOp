using Infrastructure.Extensions;

namespace Tests.Infrastructure.Extensions;
public class TimeExtensionsTests
{
    [Fact]
    public void ToIso8601Duration_NegativeNumber_ThrowException()
    {
        const double number = -561.59;
        var ex = Assert.Throws<ArgumentException>(() => number.ToIso8601Duration());
        Assert.Contains("negativas", ex.Message);
    }

    [Theory]
    [InlineData(0.0, "PT0H")]
    [InlineData(1.0, "PT1H")]
    [InlineData(8.0, "PT8H")]
    [InlineData(8.5, "PT8H30M")]
    [InlineData(0.5, "PT30M")]
    public void ToIso861Duration_RetornaCorrecto(double hours, string expected)
    {
        var result =  hours.ToIso8601Duration();
        Assert.Equal(expected, result);
    } 
    
}

