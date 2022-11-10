using gainz_bot.Commands;
using gainz_bot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace gainz_bot;

public class TelegramController
{
    public TelegramBotClient Client { get; private set; }

    private static readonly Lazy<TelegramController> lazy = new(() => new TelegramController());

    public static TelegramController Instance => lazy.Value;

    public async Task Run()
    {
        Client = new TelegramBotClient(await System.IO.File.ReadAllTextAsync("./Secrets/telegram-key-dev.txt"));

        using var cts = new CancellationTokenSource();

        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };
        
        Client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await Client.GetMeAsync(cancellationToken: cts.Token);

        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();
        
        // Send cancellation request to stop bot
        cts.Cancel();
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;

        long chatId = message.Chat.Id;

        Console.WriteLine($"Received a '{message.Type}' messageType in chat {chatId}.");
        
        if (message.Type is MessageType.Text && message.Text[0] == '/') // The user is sending a command
        {
            await CommandBroker.Command(update, cancellationToken);
        }
        else if(message.Type is MessageType.Document 
                             or MessageType.Video 
                             or MessageType.Audio 
                             or MessageType.Photo 
                             or MessageType.VideoNote) // The user is probably sending a check-in
        {
            await CheckInCommand.CheckIn(update, cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}