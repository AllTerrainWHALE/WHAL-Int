namespace Formatter;

public class Duration : IComparable<Duration>
{

    public const int SECONDS_IN_A_MINUTE = 60;
    public const int SECONDS_IN_AN_HOUR = SECONDS_IN_A_MINUTE * 60;
    public const int SECONDS_IN_A_DAY = SECONDS_IN_AN_HOUR * 24;

    public long DurationInSeconds { get; set; } = 0;

    public Duration(long durationInSeconds) => DurationInSeconds = durationInSeconds;

    public string Format()
    {
        // Find the total number of days, hours and minutes from the duration
        long day = DurationInSeconds / SECONDS_IN_A_DAY;
        long hour = (DurationInSeconds - day * SECONDS_IN_A_DAY) / SECONDS_IN_AN_HOUR;
        long min = (DurationInSeconds - day * SECONDS_IN_A_DAY - hour * SECONDS_IN_AN_HOUR) / SECONDS_IN_A_MINUTE;

        // Convert into string format of dd/hh/mm
        string coopDurationAsString = ""
            + (day > 0 ? $"{day}d" : "")
            + (hour > 0 ? $"{hour}h" : "")
            + $"{min}m"
        ;

        return coopDurationAsString;
    }

    public int CompareTo(Duration? other)
    {
        return other is null ? 1 : DurationInSeconds.CompareTo(other.DurationInSeconds);
    }
}
