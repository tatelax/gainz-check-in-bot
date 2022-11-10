using gainz_bot.Commands;
using Telegram.Bot.Types;

namespace gainz_bot.Helpers;

public static class CommandBroker
{
    public static async Task Command(Update update, CancellationToken cancellationToken)
    {
        switch (update.Message.Text.ToUpper().Split('@')[0])
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
            case "/VACATIONDAYS":
                await VacationDaysCommand.Execute(update, cancellationToken);
                break;
        }
    }
}