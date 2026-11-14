namespace StudyFlow.Core;

public enum UnscheduledReason { Overdue, InsufficientTime, BeyondHorizon }

public sealed record StudyAllocation(int TaskId, int Minutes);
public sealed record UnscheduledTask(int TaskId, int RemainingMinutes, UnscheduledReason Reason);
public sealed record PlannedDay(DateOnly Date, int AvailableMinutes, IReadOnlyList<StudyAllocation> Allocations)
{
    public int PlannedMinutes => Allocations.Sum(allocation => allocation.Minutes);
}
public sealed record StudyPlan(DateOnly StartDate, IReadOnlyList<PlannedDay> Days, IReadOnlyList<UnscheduledTask> Unscheduled);

public static class StudyPlanner
{
    public static StudyPlan Create(IEnumerable<StudyTask> tasks, IReadOnlyList<DailyAvailability> availability,
        DateOnly startDate, int days = 14)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        string? error = AvailabilityRules.Validate(availability);
        if (error is not null) throw new ArgumentException(error, nameof(availability));
        if (startDate == default) throw new ArgumentException("Choose a valid plan start date.", nameof(startDate));
        if (days is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(days), "Plan between 1 and 90 days.");
        if (startDate.DayNumber > DateOnly.MaxValue.DayNumber - days + 1)
            throw new ArgumentException("The plan extends beyond the supported date range.", nameof(startDate));

        StudyTask[] snapshot = TaskDataValidation.Snapshot(tasks);
        var capacity = availability.ToDictionary(item => item.Day, item => item.Minutes);
        var allocations = Enumerable.Range(0, days).Select(_ => new List<StudyAllocation>()).ToArray();
        var remainingCapacity = Enumerable.Range(0, days)
            .Select(offset => capacity[startDate.AddDays(offset).DayOfWeek]).ToArray();
        var unscheduled = new List<UnscheduledTask>();
        DateOnly endDate = startDate.AddDays(days - 1);
        int nextDay = 0;

        // Earliest deadlines get time first; priority and Id break ties consistently.
        foreach (var task in snapshot.Where(task => !task.IsCompleted)
                     .OrderBy(task => task.DueDate).ThenByDescending(task => task.Priority).ThenBy(task => task.Id))
        {
            if (task.DueDate < startDate)
            {
                unscheduled.Add(new(task.Id, task.EstimatedMinutes, UnscheduledReason.Overdue));
                continue;
            }

            int remaining = task.EstimatedMinutes;
            int lastDay = Math.Min(days - 1, task.DueDate.DayNumber - startDate.DayNumber);
            while (remaining > 0 && nextDay <= lastDay)
            {
                if (remainingCapacity[nextDay] == 0) { nextDay++; continue; }
                int minutes = Math.Min(remaining, remainingCapacity[nextDay]);
                allocations[nextDay].Add(new(task.Id, minutes));
                remaining -= minutes;
                remainingCapacity[nextDay] -= minutes;
            }
            if (remaining > 0)
                unscheduled.Add(new(task.Id, remaining, task.DueDate > endDate
                    ? UnscheduledReason.BeyondHorizon : UnscheduledReason.InsufficientTime));
        }

        var plannedDays = Enumerable.Range(0, days).Select(offset =>
        {
            DateOnly date = startDate.AddDays(offset);
            return new PlannedDay(date, capacity[date.DayOfWeek], allocations[offset].AsReadOnly());
        }).ToArray();
        return new(startDate, Array.AsReadOnly(plannedDays), unscheduled.AsReadOnly());
    }
}

internal static class TaskDataValidation
{
    internal static StudyTask[] Snapshot(IEnumerable<StudyTask> tasks)
    {
        var snapshot = tasks.ToArray();
        var ids = new HashSet<int>();
        foreach (var task in snapshot)
        {
            if (task is null || task.Id <= 0 || !ids.Add(task.Id))
                throw new ArgumentException("Tasks must have unique positive IDs.", nameof(tasks));
            if (task.EstimatedMinutes <= 0 || task.DueDate == default || !Enum.IsDefined(task.Priority))
                throw new ArgumentException("Tasks need a positive estimate, valid deadline, and known priority.", nameof(tasks));
        }
        return snapshot;
    }
}
