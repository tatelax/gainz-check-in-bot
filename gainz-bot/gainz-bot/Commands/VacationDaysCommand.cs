using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace gainz_bot.Commands;

public static class VacationDaysCommand
{
    public static async Task Execute(Update update, CancellationToken token)
    {
        int totalVacationDays = 0;
        
        DocumentReference userDoc = FirebaseController.Instance.db.Document($"users/{update.Message.From.Username}");
        DocumentSnapshot userSnapshot = await userDoc.GetSnapshotAsync(token);

        totalVacationDays = userSnapshot.GetValue<int>("VacationDays");
        
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: $"You've earned {totalVacationDays} vacation days.", cancellationToken: token);
    }
}