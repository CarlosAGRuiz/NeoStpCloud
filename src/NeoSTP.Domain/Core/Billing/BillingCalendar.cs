namespace NeoSTP.Domain.Core.Billing;

public static class BillingCalendar
{
    public static readonly TimeZoneInfo ElSalvador = TimeZoneInfo.FindSystemTimeZoneById("America/El_Salvador");
    public static DateOnly MonthAt(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ElSalvador);
        return new DateOnly(local.Year, local.Month, 1);
    }
    public static DateTime StartUtc(DateOnly localDate) => TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), ElSalvador);
    public static DateOnly DueDate(DateOnly month) => month.AddMonths(1).AddDays(-1);
}
