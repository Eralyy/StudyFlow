namespace StudyFlow.Core;

public sealed record DailyAvailability(DayOfWeek Day, int Minutes);

public static class AvailabilityRules
{
    public static string? Validate(IReadOnlyList<DailyAvailability> availability)
    {
        if (availability is null || availability.Count != 7)
            return "Set available study time for all seven days.";
        var days = new HashSet<DayOfWeek>();
        foreach (var item in availability)
        {
            if (item is null || !Enum.IsDefined(item.Day))
                return "Choose a valid day of the week.";
            if (!days.Add(item.Day))
                return "Each day must appear exactly once.";
            if (item.Minutes is < 0 or > 720)
                return "Daily study time must be between 0 and 720 minutes.";
        }
        return null;
    }
}
