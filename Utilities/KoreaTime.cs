namespace ReadMeApp.Utilities;

public static class KoreaTime
{
    private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9);

    public static DateTime FromUtc(DateTime utcDateTime)
    {
        // SQL Server commonly materializes datetime2 values with Kind=Unspecified.
        // Read.me stores CreatedAt/UpdatedAt timestamps as UTC, so normalize them
        // to UTC before applying Korea Standard Time (UTC+9).
        var normalizedUtc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return normalizedUtc.Add(KoreaOffset);
    }

    public static DateTime Now => DateTime.UtcNow.Add(KoreaOffset);
}
