namespace gainz_bot;

public static class Program
{
    private const int PollingInterval = 600000;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting controllers...");
        
        await Task.WhenAll(TelegramController.Instance.Run(),
                           FirebaseController.Instance.Run(),
                           PollingController.Instance.Run(PollingInterval));
        
        Console.WriteLine("All controllers stopped.");
    }
}