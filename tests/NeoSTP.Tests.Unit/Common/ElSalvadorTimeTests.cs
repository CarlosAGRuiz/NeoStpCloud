using FluentAssertions;
using NeoSTP.Application.Common;

namespace NeoSTP.Tests.Unit.Common;

public sealed class ElSalvadorTimeTests
{
    [Theory]
    [InlineData("2026-09-01T05:59:59Z", 2026, 8, 31)]
    [InlineData("2026-09-01T06:00:00Z", 2026, 9, 1)]
    [InlineData("2027-01-01T05:59:59Z", 2026, 12, 31)]
    [InlineData("2027-01-01T06:00:00Z", 2027, 1, 1)]
    public void Today_changes_at_midnight_in_El_Salvador(
        string utc, int year, int month, int day)
    {
        var clock = new FixedClock(DateTimeOffset.Parse(utc));

        ElSalvadorTime.Today(clock).Should().Be(new DateTime(year, month, day));
    }

    [Fact]
    public void UtcMonth_starts_at_06_00_utc()
    {
        var (start, end) = ElSalvadorTime.UtcMonth(2026, 9);

        start.Should().Be(new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
