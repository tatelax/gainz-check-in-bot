namespace gainz_bot;

public class Program
{
    public static async Task Main(string[] args)
    {
        var telegramHandlerTask = new TelegramController().Run();
        var firebaseTask = new FirebaseController().Run();

        Console.WriteLine("Starting controllers...");
        
        await Task.WhenAll(telegramHandlerTask, firebaseTask);
        
        Console.WriteLine("All controllers stopped.");
    }
}