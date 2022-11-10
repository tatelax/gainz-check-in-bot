using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace gainz_bot.Commands;

public static class RegisterCommand
{
    public static async Task Execute(Update update, CancellationToken token)
    {
        CollectionReference usersCollection = FirebaseController.Instance.db.Collection("users");

        Console.WriteLine($"Attempting to register user {update.Message.From.Username}");
        
        if (!await CheckIfUserRegistered(update, token))
        {
            await RegisterUser(update, usersCollection, token);
            await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: "You're now registered!", cancellationToken: token);
        }
    }

    private static async Task<bool> CheckIfUserRegistered(Update update, CancellationToken token)
    {
        DocumentReference userDoc = FirebaseController.Instance.db.Document($"users/{update.Message.From.Username}");
        DocumentSnapshot userSnapshot = await userDoc.GetSnapshotAsync(token);

        if (userSnapshot.Exists)
        {
            await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: $"You're already registered to chat ID {userSnapshot.GetValue<int>("ChatID")}.", cancellationToken: token);
        }
        
        return userSnapshot.Exists;
    }

    private static async Task RegisterUser(Update update, CollectionReference usersCollection, CancellationToken token)
    {
        await usersCollection.Document(update.Message.From.Username).CreateAsync(new
        {
            ChatID = update.Message.Chat.Id,
            LastWarnTime = 0,
            HasBeenWarned = false,
            LastCheckIn = DateTimeOffset.Now.ToUnixTimeSeconds(),
            TelegramID = update.Message.From.Id,
            VacationDays = 3,
            TotalCheckIns = 0,
            LastVacationSubtract = 0
        }, token);
        
        Console.WriteLine($"New user registered: {update.Message.From.Username}");
    }
}