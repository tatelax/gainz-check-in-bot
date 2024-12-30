using System.Threading;
using System.Threading.Tasks;

namespace gainz_bot;

public static class Program
{
    public const int CheckInsToEarnReward = 10;

    private const int PollingInterval = 600000;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting controllers...");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        await FirebaseController.Instance.Run(cts.Token);
        await TelegramController.Instance.Run(cts.Token);
        var pollingTask = PollingController.Instance.Run(PollingInterval, cts.Token);

        await Task.WhenAll(pollingTask);

        Console.WriteLine("All controllers stopped.");
    }
}
