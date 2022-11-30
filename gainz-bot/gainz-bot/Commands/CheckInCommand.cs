using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace gainz_bot.Commands;

public static class CheckInCommand
{
    private static HashSet<long> PendingCheckIn = new (); // User typed /checkin now we are waiting for their media

    public static async Task Execute(Update update, CancellationToken token)
    {
        await AddUserAsPendingCheckin(update, token);
    }

    public static async Task CheckIn(Update update, CancellationToken token)
    {
        if (!PendingCheckIn.Contains(update.Message.From.Id))
            return;

        DocumentReference userDoc = FirebaseController.Instance.db.Document($"users/{update.Message.From.Username}");
        DocumentSnapshot userSnapshot = await userDoc.GetSnapshotAsync(token);

        int currentCheckIns;
        int checkInsUntilNextReward = userSnapshot.GetValue<int>("CheckInsUntilNextReward");
        
        if (!userSnapshot.ContainsField("TotalCheckIns"))
            currentCheckIns = 0;
        else
            currentCheckIns = userSnapshot.GetValue<int>("TotalCheckIns");

        var updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("HasBeenWarned"), false },
            { new FieldPath("TotalCheckIns"), ++currentCheckIns },
            { new FieldPath("LastCheckIn"), DateTimeOffset.Now.ToUnixTimeSeconds()}
        };

        if (checkInsUntilNextReward <= 1)
        {
            await AwardVacationDay(update, userDoc, userSnapshot, token);
            checkInsUntilNextReward = Program.CheckInsToEarnReward;
        }
        else
        {
            updates.Add(new FieldPath("CheckInsUntilNextReward"), --checkInsUntilNextReward);
        }

        await userDoc.UpdateAsync(updates, cancellationToken: token);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: $"😤 You're checked in! You've checked in {currentCheckIns} times. {checkInsUntilNextReward} more check-ins until your next reward.", cancellationToken: token);
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

    private static async Task AwardVacationDay(Update update, DocumentReference documentReference, DocumentSnapshot documentSnapshot, CancellationToken token)
    {
        int currVacationDays = documentSnapshot.GetValue<int>("VacationDays");
        
        Dictionary<FieldPath, object> updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("VacationDays"),  ++currVacationDays },
            { new FieldPath("CheckInsUntilNextReward"), Program.CheckInsToEarnReward}
        };

        await documentReference.UpdateAsync(updates);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: $"🎉 Congratulations! You've earned a vacation day. You now have {currVacationDays} vacation days.", cancellationToken: token);
    }
}