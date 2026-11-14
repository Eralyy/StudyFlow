using System.Diagnostics;
using StudyFlow.Core;

DateOnly start = new(2026, 11, 16);
var availability = Enum.GetValues<DayOfWeek>().Select(day =>
    new DailyAvailability(day, day == DayOfWeek.Monday ? 30 : 0)).ToArray();
StudyPlanner.Create(Array.Empty<StudyTask>(), availability, start, 90);
foreach (int count in new[] { 1_000, 10_000, 50_000 })
{
    var tasks = Enumerable.Range(1, count).Select(id => new StudyTask(id, 1, $"Task {id}",
        start.AddDays(100 + id % 500), 60, TaskPriority.Medium, false, "")).ToArray();
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    var plan = StudyPlanner.Create(tasks, availability, start, 90);
    stopwatch.Stop();
    long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    long planned = plan.Days.Sum(day => (long)day.PlannedMinutes);
    long remaining = plan.Unscheduled.Sum(task => (long)task.RemainingMinutes);
    if (planned + remaining != (long)count * 60 || plan.Days.Any(day => day.PlannedMinutes > day.AvailableMinutes))
        throw new InvalidOperationException("The large-input plan did not preserve minutes or daily limits.");
    Console.WriteLine($"Tasks: {count:N0}; horizon: {plan.Days.Count}; Mondays only: 30 minutes");
    Console.WriteLine($"Planned: {planned}; unallocated: {remaining}; elapsed: {stopwatch.Elapsed.TotalMilliseconds:F1} ms; allocated: {allocatedBytes:N0} bytes");
}
Console.WriteLine("Capacity and conservation checks passed. Timing is diagnostic, not a CI pass/fail threshold.");
