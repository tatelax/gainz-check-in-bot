using gainz_bot.Commands;
using Telegram.Bot.Types;

namespace gainz_bot.Helpers;

public static class CommandBroker
{
    public static async Task Command(Update update, CancellationToken cancellationToken)
    {
        string command = update.Message.Text.ToUpper();

        switch (command)
        {
            case "/REGISTER":
                await RegisterCommand.Execute(update, cancellationToken);
                break;
            case "/CHECKIN":
                await CheckInCommand.Execute(update, cancellationToken);
                break;
            case "/STATS":
                await StatsCommand.Execute(update, cancellationToken);
                break;
        }
    }
}