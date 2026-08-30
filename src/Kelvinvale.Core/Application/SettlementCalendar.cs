namespace Kelvinvale.Core.Application;

/// <summary>
/// A deliberately simple settlement calendar: value date is arrival + N working days (weekends
/// skipped, bank holidays ignored). Real dealing calendars vary by fund; called out in the README.
/// </summary>
public static class SettlementCalendar
{
    public const int SettlementWorkingDays = 2;

    public static DateOnly ValueDateFrom(DateOnly arrivalDate, int workingDays = SettlementWorkingDays)
    {
        var date = arrivalDate;
        var added = 0;
        while (added < workingDays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }

        return date;
    }
}
