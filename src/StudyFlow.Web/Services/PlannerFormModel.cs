using System.ComponentModel.DataAnnotations;
using StudyFlow.Core;

namespace StudyFlow.Web.Services;

public sealed class PlannerFormModel
{
    public List<DayAvailabilityInput> Days { get; } = [];

    public void Load(IReadOnlyList<DailyAvailability> availability)
    {
        Days.Clear();
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        foreach (DayOfWeek day in week)
            Days.Add(new() { Day = day, Minutes = availability.Single(a => a.Day == day).Minutes });
    }

    public IReadOnlyList<DailyAvailability> ToAvailability() =>
        Days.Select(day => new DailyAvailability(day.Day, day.Minutes)).ToArray();
}

public sealed class DayAvailabilityInput
{
    public DayOfWeek Day { get; init; }

    [Range(0, 720, ErrorMessage = "Use between 0 and 720 minutes.")]
    public int Minutes { get; set; }
}

public sealed class PlanRangeInput
{
    [Required(ErrorMessage = "Choose a start date.")]
    public DateOnly? StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Days { get; set; } = 14;
}
