namespace StargateAPI.Common;

public static class DateOnlyUtils
{
    public static DateOnly ToDateOnly(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
        return DateOnly.FromDateTime(dt);
    }

    public static DateTime ToDateTimeAtMidnight(DateOnly d)
        => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
}
