using System.Diagnostics;
using Google.Cloud.Firestore;
using Telegram.Bot;

namespace gainz_bot;

public class PollingController
{
    private static readonly Lazy<PollingController> lazy = new(() => new PollingController());

    public static PollingController Instance => lazy.Value;

    public async Task Run(int pollingInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (FirebaseController.Instance.db is null)
            {
                await Task.Delay(pollingInterval, cancellationToken);
                continue;
            }

            Console.WriteLine("Polling for kicks...");

            long oneDayAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 86400;
            long sixDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 518400;
            long sevenDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 604800;

            CollectionReference usersCollection = FirebaseController.Instance.db.Collection("users");
            Query usersThatNeedToBeWarnedOrKicked = usersCollection.WhereLessThanOrEqualTo("LastCheckIn", sixDaysAgo);
            QuerySnapshot? snapshot = await usersThatNeedToBeWarnedOrKicked.GetSnapshotAsync();

            if (snapshot is not null)
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    int lastCheckIn = snapshot[i].GetValue<int>("LastCheckIn");
                    long chatID = snapshot[i].GetValue<long>("ChatID");
                    long telegramID = snapshot[i].GetValue<long>("TelegramID");
                    int remainingVacationDays = snapshot[i].GetValue<int>("VacationDays");
                    int lastVacationSubtract = snapshot[i].GetValue<int>("LastVacationSubtract");
                    bool hasBeenWarned = snapshot[i].GetValue<bool>("HasBeenWarned");

                    if (telegramID == 149010936 || telegramID == 193180406 || telegramID == 119714211)
                    {
                        continue;
                    }

                    if (lastCheckIn <= sixDaysAgo && lastCheckIn >= sevenDaysAgo && !hasBeenWarned)
                    {
                        await WarnUser(snapshot[i], remainingVacationDays, chatID);
                    }
                    else if (remainingVacationDays > 0 && lastVacationSubtract <= oneDayAgo)
                    {
                        await SubtractVacationDay(snapshot[i], chatID, remainingVacationDays);
                    }
                    else if (remainingVacationDays == 0)
                    {
                        await KickUser(snapshot[i], chatID, telegramID);
                    }
                }
            }

            // Wait asynchronously before the next polling iteration
            await Task.Delay(pollingInterval, cancellationToken);
        }
    }


    private async Task WarnUser(DocumentSnapshot snapshot, int remainingVacationDays, long chatID)
    {
        Console.WriteLine($"Warning user {snapshot.Id} in chat {chatID}...");

        string warningMessage = $"⚠️ @{snapshot.Id}, you have 24 hours to submit a check-in before you are kicked! You are out of vacation days.";

        if (remainingVacationDays > 0)
            warningMessage = $"⚠️ @{snapshot.Id}, if you don't check-in within 24 hours, a vacation day will be used. You have {remainingVacationDays} left!";

        Dictionary<FieldPath, object> updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("HasBeenWarned"), true },
            { new FieldPath("LastVacationSubtract"), DateTimeOffset.Now.ToUnixTimeSeconds() },
        };

        await snapshot.Reference.UpdateAsync(updates);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId: chatID, text: warningMessage);
    }

    private async Task SubtractVacationDay(DocumentSnapshot snapshot, long chatID, int remainingVacationDays)
    {
        Console.WriteLine($"Subtracting vacation day from user {snapshot.Id} in chat {chatID}");

        Dictionary<FieldPath, object> updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("LastVacationSubtract"), DateTimeOffset.Now.ToUnixTimeSeconds() },
            { new FieldPath("VacationDays"), --remainingVacationDays }
        };

        string message = $"🌴 @{snapshot.Id} you lost a vacation day and have {remainingVacationDays} remaining.";

        if (remainingVacationDays > 0)
            message += " Check-in within 24 hours to prevent losing another!";
        else
            message += " Check-in within 24 hours to prevent being kicked!";

        await snapshot.Reference.UpdateAsync(updates);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId: chatID, text: message);
    }

    private async Task KickUser(DocumentSnapshot snapshot, long chatID, long userID)
    {
        Console.WriteLine($"Kicking user {snapshot.Id}, in chat {chatID}");

        await snapshot.Reference.DeleteAsync();
        await TelegramController.Instance.Client.BanChatMemberAsync(chatId: chatID, userId: userID);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId: chatID, text: $"💀 @{snapshot.Id} has been kicked! All data deleted. RIP.");
    }
}