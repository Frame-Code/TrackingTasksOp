namespace Infrastructure.Adapters.Services;

public class TimeTrackService
{
    public static double GetRandomMinutes(int start, int end)
    {
        Random random = new Random();
        return Math.Round(random.NextDouble() * (end - start) + start, 2);
    }
}