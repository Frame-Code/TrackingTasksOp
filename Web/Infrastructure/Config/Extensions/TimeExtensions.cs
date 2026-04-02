using System.Text;

namespace Web.Infrastructure.Config.Extensions;

public static class TimeExtensions
{
    public static string ToIso8601Duration(this double hours)
    {
        if (hours < 0)
            throw new ArgumentException("Las horas no pueden ser negativas.");

        TimeSpan ts = TimeSpan.FromHours(hours);

        var parts = new StringBuilder("PT");

        int totalHours = (int)ts.TotalHours;
        if (totalHours > 0)
            parts.Append($"{totalHours}H");

        if (ts.Minutes > 0)
            parts.Append($"{ts.Minutes}M");

        if (ts.Seconds > 0)
            parts.Append($"{ts.Seconds}S");

        return parts.Length == 2 ? "PT0H" : parts.ToString();
    }
}