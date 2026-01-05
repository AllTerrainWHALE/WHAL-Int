using System.ComponentModel;

namespace Formatter;

public class DiscordTimestamp : IComparable<DiscordTimestamp>
{
    public long UnixSeconds { get; set; } = 0;

    public DiscordTimestamp(long secondsSinceEpoch) => UnixSeconds = secondsSinceEpoch;

    public string Format(DiscordTimestampDisplay displayMode)
    {
        char identifier = displayMode switch
        {
            DiscordTimestampDisplay.ShortDate => 'd',
            DiscordTimestampDisplay.FullDate => 'D',
            DiscordTimestampDisplay.HourMinuteTime => 't',
            DiscordTimestampDisplay.HourMinuteSecondTime => 'T',
            DiscordTimestampDisplay.FullDateTime => 'f',
            DiscordTimestampDisplay.FullDateTimeDayOfWeek => 'F',
            DiscordTimestampDisplay.Relative => 'R',
            _ => throw new InvalidEnumArgumentException(),
        };

        return $"<t:{UnixSeconds}:{identifier}>";
    }

    public int CompareTo(DiscordTimestamp? other)
    {
        return other is null ? 1 : UnixSeconds.CompareTo(other.UnixSeconds);
    }
}

public enum DiscordTimestampDisplay
{
    ShortDate,
    FullDate,
    HourMinuteTime,
    HourMinuteSecondTime,
    FullDateTime,
    FullDateTimeDayOfWeek,
    Relative,
}
