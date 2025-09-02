namespace WHAL_Int.Formatter;

public class Duration : IComparable<Duration>
{

    public const int SECONDS_IN_A_MINUTE = 60;
    public const int SECONDS_IN_AN_HOUR = SECONDS_IN_A_MINUTE * 60;
    public const int SECONDS_IN_A_DAY = SECONDS_IN_AN_HOUR * 24;

    public double DurationInSeconds { get; set; } = 0;
    public double DurationInMinutes => DurationInSeconds / SECONDS_IN_A_MINUTE;
    public double DurationInHours => DurationInSeconds / SECONDS_IN_AN_HOUR;
    public double DurationInDays => DurationInSeconds / SECONDS_IN_A_DAY;

    public Duration(long durationInSeconds) => DurationInSeconds = durationInSeconds;

    public string Format()
    {
        // Find the total number of days, hours and minutes from the duration
        long day = (long) (DurationInSeconds / SECONDS_IN_A_DAY);
        long hour = (long) ((DurationInSeconds - day * SECONDS_IN_A_DAY) / SECONDS_IN_AN_HOUR);
        long min = (long) ((DurationInSeconds - day * SECONDS_IN_A_DAY - hour * SECONDS_IN_AN_HOUR) / SECONDS_IN_A_MINUTE);

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
