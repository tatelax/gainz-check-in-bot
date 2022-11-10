using Google.Cloud.Firestore;
using Telegram.Bot;

namespace gainz_bot;

public class PollingController
{
    private static readonly Lazy<PollingController> lazy = new(() => new PollingController());

    public static PollingController Instance => lazy.Value;

    public async Task Run(int pollingInterval)
    {
        if (FirebaseController.Instance.db is null)
        {
            Task.Run(() => Run(pollingInterval));
            return;
        }
        
        Console.WriteLine("Polling for kicks...");

        //long sixDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 518400;
        //long sevenDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 604800;

        long oneDayAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 5;
        long sixDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 10;
        long sevenDaysAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 20;

        CollectionReference usersCollection = FirebaseController.Instance.db.Collection("users");
        Query usersThatNeedToBeWarnedOrKicked = usersCollection.WhereLessThanOrEqualTo("LastCheckIn", sixDaysAgo);
        QuerySnapshot? snapshot = await usersThatNeedToBeWarnedOrKicked.GetSnapshotAsync();

        if (snapshot is not null)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                int lastCheckIn = snapshot[i].GetValue<int>("LastCheckIn");
                long chatID = snapshot[i].GetValue<int>("ChatID");
                int remainingVacationDays = snapshot[i].GetValue<int>("VacationDays");
                int lastVacationSubtract = snapshot[i].GetValue<int>("LastVacationSubtract");
                bool hasBeenWarned = snapshot[i].GetValue<bool>("HasBeenWarned");

                Console.WriteLine($"{lastCheckIn} {remainingVacationDays}");

                if (lastCheckIn <= sixDaysAgo && lastCheckIn >= sevenDaysAgo && !hasBeenWarned) // Warn user
                {
                    await WarnUser(snapshot[i], remainingVacationDays, chatID);
                }
                else if (remainingVacationDays > 0 && lastVacationSubtract <= oneDayAgo) // Remove a vacation day
                {
                    await SubtractVacationDay(snapshot[i], chatID, remainingVacationDays);
                }
                else // Kick user
                {
                    await KickUser(snapshot[i], chatID);
                }
            }
        }

        Task.Delay(pollingInterval).Wait();

        Task.Run(() => Run(pollingInterval));
    }

    private async Task WarnUser(DocumentSnapshot snapshot, int remainingVacationDays, long chatID)
    {
        Console.WriteLine($"Warning user {snapshot.Id} in chat {chatID}...");

        string warningMessage = $"⚠️ @{snapshot.Id}, you have 24 hours to submit a check-in before you are kicked! You are out of vacation days.";

        if (remainingVacationDays > 0)
            warningMessage = $"⚠️ @{snapshot.Id}, if you don't check-in within 24 hours, a vacation day will be used. You have {remainingVacationDays} left!";

        Dictionary<FieldPath, object> updates = new Dictionary<FieldPath, object>
        {
            { new FieldPath("HasBeenWarned"), true }
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

        string message = $"🌴 @{snapshot.Id} you lost 1 vacation day and have {remainingVacationDays} remaining.";

        if (remainingVacationDays > 0)
            message += " Check-in within 24 hours to prevent losing another!";
        else
            message += " Check-in within 24 hours to prevent being kicked!";

        await snapshot.Reference.UpdateAsync(updates);
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId: chatID, text: message);
    }

    private async Task KickUser(DocumentSnapshot snapshot, long chatID)
    {
        Console.WriteLine($"Kicking user {snapshot.Id}, in chat {chatID}");

        await snapshot.Reference.DeleteAsync();
        await TelegramController.Instance.Client.SendTextMessageAsync(chatId: chatID, text: $"💀 @{snapshot.Id} has been kicked! All data deleted. RIP.");
    }
}