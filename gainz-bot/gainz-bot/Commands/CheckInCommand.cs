using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace gainz_bot.Commands;

public static class CheckInCommand
{
    private static HashSet<long> PendingCheckIn = new (); // User typed /checkin now we are waiting for their media

    public static async Task Execute(Update update, CancellationToken token)
    {
        AddUserAsPendingCheckin(update, token);
    }

    public static async Task CheckIn(Update update, CancellationToken token)
    {
        if (!PendingCheckIn.Contains(update.Message.From.Id))
            return;

        DocumentReference userDoc = FirebaseController.Instance.db.Document($"users/{update.Message.From.Username}");
        DocumentSnapshot userSnapshot = await userDoc.GetSnapshotAsync(token);

        int currentCheckIns;
        
        if (!userSnapshot.ContainsField("TotalCheckIns"))
        {
            currentCheckIns = 0;
        }
        else
        {
            currentCheckIns = userSnapshot.GetValue<int>("TotalCheckIns");
        }

        Dictionary<FieldPath, object> updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("HasBeenWarned"), false },
            { new FieldPath("TotalCheckIns"), ++currentCheckIns }
        };

        await userDoc.UpdateAsync(updates, cancellationToken: token);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: $"You're checked in! You've checked in {currentCheckIns} times.", cancellationToken: token);
        PendingCheckIn.Remove(update.Message.From.Id);
    }

    private static async Task AddUserAsPendingCheckin(Update update, CancellationToken token)
    {
        if (PendingCheckIn.Contains(update.Message.From.Id))
        {
            await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: "Attach a photo or video to check in.", cancellationToken: token);
            return;
        }

        PendingCheckIn.Add(update.Message.From.Id);
        
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: "⭐ Great, send your photo or video!", cancellationToken: token);
    }
}