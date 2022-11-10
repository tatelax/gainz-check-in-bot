using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace gainz_bot.Commands;

public static class StatsCommand
{
    public static async Task Execute(Update update, CancellationToken token)
    {
        CollectionReference usersCollection = FirebaseController.Instance.db.Collection("users");
        Query usersInSameChatQuery = usersCollection.WhereEqualTo("ChatID", update.Message.Chat.Id);
        QuerySnapshot? snapshot = await usersInSameChatQuery.GetSnapshotAsync(token);

        if (snapshot is null)
            return;

        string chatTitle = update.Message.Chat.Username;

        if (update.Message.Chat.Type is ChatType.Group or ChatType.Channel)
            chatTitle = update.Message.Chat.Title;

        string statsMsg = $"Stats for {chatTitle}\n";

        if (snapshot.Count == 0)
        {
            statsMsg += "\nNo stats found. Use /register to get started.";
        }
        else
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                var userSnapshot = await snapshot[i].Reference.GetSnapshotAsync(token);
                string username = snapshot[i].Id;
                int totalCheckins = userSnapshot.GetValue<int>("TotalCheckIns");

                statsMsg += $"\n{username}: {totalCheckins}";
            }
        }
        
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId:update.Message.Chat.Id, text: statsMsg, cancellationToken: token);
    }
}