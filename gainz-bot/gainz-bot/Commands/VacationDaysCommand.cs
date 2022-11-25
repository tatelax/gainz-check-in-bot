using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace gainz_bot.Commands;

public static class VacationDaysCommand
{
    public static async Task Execute(Update update, CancellationToken token)
    {
        DocumentReference userDoc = FirebaseController.Instance.db.Document($"users/{update.Message.From.Username}");
        DocumentSnapshot userSnapshot = await userDoc.GetSnapshotAsync(token);

        int totalVacationDays = userSnapshot.GetValue<int>("VacationDays");
        int checkInsTillNextReward = userSnapshot.GetValue<int>("CheckInsUntilNextReward");
        
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id,
            text: $"You've earned {totalVacationDays} vacation days. {checkInsTillNextReward} days until your next vacation day reward.", 
            cancellationToken: token);
    }
}