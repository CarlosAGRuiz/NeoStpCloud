namespace NeoSTP.Application.Common;

/// <summary>
/// Reloj comercial/fiscal de El Salvador. Convierte límites de calendario local a UTC
/// para consultar timestamps persistidos sin adelantar el día o el mes a las 18:00.
/// </summary>
public static class ElSalvadorTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now(TimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return TimeZoneInfo.ConvertTime(provider.GetUtcNow(), Zone).DateTime;
    }

    public static DateTime Today(TimeProvider provider) => Now(provider).Date;

    public static (DateTime StartLocal, DateTime EndLocal) LocalMonth(
        int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return (start, start.AddMonths(1));
    }

    public static (DateTime StartUtc, DateTime EndUtc) UtcMonth(
        int year, int month)
    {
        var (start, end) = LocalMonth(year, month);
        return (
            TimeZoneInfo.ConvertTimeToUtc(start, Zone),
            TimeZoneInfo.ConvertTimeToUtc(end, Zone));
    }

    public static (DateTime StartUtc, DateTime EndUtc) UtcDay(DateTime localDate)
    {
        var start = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        return (
            TimeZoneInfo.ConvertTimeToUtc(start, Zone),
            TimeZoneInfo.ConvertTimeToUtc(start.AddDays(1), Zone));
    }

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/El_Salvador", "Central America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "SV-UTC-6", TimeSpan.FromHours(-6), "El Salvador", "El Salvador");
    }
}
