using StudyFlow.Core;
using Xunit;

namespace StudyFlow.Tests;

public sealed class StudyPlannerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static DailyAvailability[] Week(int minutes = 60) => Enum.GetValues<DayOfWeek>()
        .Select(day => new DailyAvailability(day, minutes)).ToArray();
    private static StudyTask Task(int id, int dueOffset = 0, int minutes = 60,
        TaskPriority priority = TaskPriority.Medium, bool completed = false) =>
        new(id, 1, $"Task {id}", Monday.AddDays(dueOffset), minutes, priority, completed, "");

    [Fact]
    public void EarliestDeadlineWinsThenPriorityThenId()
    {
        var tasks = new[] { Task(5, 1, 15, TaskPriority.High), Task(3, 0, 15),
            Task(2, 0, 15, TaskPriority.High), Task(1, 0, 15) };
        var plan = StudyPlanner.Create(tasks, Week(), Monday, 2);
        Assert.Equal(new[] { 2, 1, 3, 5 }, plan.Days[0].Allocations.Select(item => item.TaskId));
        Assert.Empty(plan.Unscheduled);
    }

    [Fact]
    public void SplitsAcrossDaysButDoesNotUseTimeAfterDeadline()
    {
        var plan = StudyPlanner.Create(new[] { Task(1, 1, 150) }, Week(), Monday, 3);
        Assert.Equal(new[] { 60, 60, 0 }, plan.Days.Select(day => day.PlannedMinutes));
        Assert.Equal(new UnscheduledTask(1, 30, UnscheduledReason.InsufficientTime), Assert.Single(plan.Unscheduled));
    }

    [Fact]
    public void CompletedAndOverdueTasksAreNeverAllocated()
    {
        var plan = StudyPlanner.Create(new[] { Task(1, -1), Task(2, 1, completed: true), Task(3) }, Week(), Monday, 2);
        Assert.Equal(new[] { 3 }, plan.Days.SelectMany(day => day.Allocations).Select(item => item.TaskId));
        Assert.Equal(new UnscheduledTask(1, 60, UnscheduledReason.Overdue), Assert.Single(plan.Unscheduled));
    }

    [Fact]
    public void LaterDeadlinesReportBeyondHorizonForAnyRemainingWork()
    {
        var plan = StudyPlanner.Create(new[] { Task(1, 20, 150) }, Week(), Monday, 2);
        Assert.Equal(120, plan.Days.Sum(day => day.PlannedMinutes));
        Assert.Equal(new UnscheduledTask(1, 30, UnscheduledReason.BeyondHorizon), Assert.Single(plan.Unscheduled));
    }

    [Fact]
    public void ZeroCapacityLeavesAllOpenWorkUnscheduled()
    {
        var plan = StudyPlanner.Create(new[] { Task(1), Task(2, 20) }, Week(0), Monday, 7);
        Assert.All(plan.Days, day => Assert.Empty(day.Allocations));
        Assert.Equal(new[] { UnscheduledReason.InsufficientTime, UnscheduledReason.BeyondHorizon },
            plan.Unscheduled.Select(item => item.Reason));
    }

    [Fact]
    public void EmptyInputStillReturnsEveryRequestedDayAndWeeklyCapacity()
    {
        var week = Week(0);
        week[(int)DayOfWeek.Monday] = new(DayOfWeek.Monday, 45);
        var plan = StudyPlanner.Create(Array.Empty<StudyTask>(), week, Monday, 8);
        Assert.Equal(8, plan.Days.Count);
        Assert.Equal(45, plan.Days[0].AvailableMinutes);
        Assert.Equal(45, plan.Days[7].AvailableMinutes);
        Assert.Empty(plan.Unscheduled);
    }

    [Fact]
    public void SameInputsAndDifferentInputOrderGiveTheSamePlanWithoutMutation()
    {
        var tasks = new[] { Task(3, 2), Task(1, 1, 90), Task(2, 1, 90, TaskPriority.High) };
        var original = tasks.ToArray();
        var week = Week();
        var originalWeek = week.ToArray();
        var first = StudyPlanner.Create(tasks, week, Monday, 3);
        var second = StudyPlanner.Create(tasks.Reverse(), week.Reverse().ToArray(), Monday, 3);
        Assert.Equal(original, tasks);
        Assert.Equal(originalWeek, week);
        Assert.Equal(first.Unscheduled.ToArray(), second.Unscheduled.ToArray());
        for (int index = 0; index < first.Days.Count; index++)
        {
            Assert.Equal(first.Days[index].Date, second.Days[index].Date);
            Assert.Equal(first.Days[index].Allocations.ToArray(), second.Days[index].Allocations.ToArray());
        }
        Assert.Throws<NotSupportedException>(() => ((IList<PlannedDay>)first.Days).Clear());
    }

    [Fact]
    public void MixedWorkloadPreservesCapacityDeadlinesAndEveryMinute()
    {
        var random = new Random(1234);
        var tasks = Enumerable.Range(1, 200).Select(id => Task(id, random.Next(-3, 25),
            random.Next(1, 20) * 15, (TaskPriority)random.Next(1, 4), id % 9 == 0)).ToArray();
        var week = Enum.GetValues<DayOfWeek>().Select(day => new DailyAvailability(day, (int)day * 30)).ToArray();
        var plan = StudyPlanner.Create(tasks, week, Monday, 14);
        var byId = tasks.ToDictionary(task => task.Id);
        foreach (var day in plan.Days)
        {
            Assert.InRange(day.PlannedMinutes, 0, day.AvailableMinutes);
            foreach (var allocation in day.Allocations)
            {
                Assert.True(allocation.Minutes > 0);
                Assert.False(byId[allocation.TaskId].IsCompleted);
                Assert.True(day.Date <= byId[allocation.TaskId].DueDate);
            }
        }
        foreach (var task in tasks)
        {
            int allocated = plan.Days.SelectMany(day => day.Allocations).Where(item => item.TaskId == task.Id).Sum(item => item.Minutes);
            int remaining = plan.Unscheduled.Where(item => item.TaskId == task.Id).Sum(item => item.RemainingMinutes);
            Assert.Equal(task.IsCompleted ? 0 : task.EstimatedMinutes, allocated + remaining);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void RejectsInvalidHorizon(int days) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        StudyPlanner.Create(Array.Empty<StudyTask>(), Week(), Monday, days));

    [Fact]
    public void DateBoundaryIsCheckedBeforeAddingDays()
    {
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(Array.Empty<StudyTask>(), Week(), default));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(Array.Empty<StudyTask>(), Week(), DateOnly.MaxValue, 2));
        Assert.Equal(DateOnly.MaxValue, Assert.Single(StudyPlanner.Create(Array.Empty<StudyTask>(), Week(), DateOnly.MaxValue, 1).Days).Date);
    }

    [Fact]
    public void RejectsDuplicateIdsAndInvalidTaskData()
    {
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(new[] { Task(1), Task(1) }, Week(), Monday));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(new[] { Task(0) }, Week(), Monday));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(new[] { Task(1, minutes: 0) }, Week(), Monday));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(new[] { Task(1, priority: (TaskPriority)9) }, Week(), Monday));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(new[] { Task(1) with { DueDate = default } }, Week(), Monday));
        Assert.Throws<ArgumentException>(() => StudyPlanner.Create(Array.Empty<StudyTask>(), Week()[..6], Monday));
    }

    [Fact]
    public void AvailabilityRequiresSevenUniqueValidDaysAndBoundedMinutes()
    {
        Assert.Null(AvailabilityRules.Validate(Week(0)));
        Assert.Null(AvailabilityRules.Validate(Week(720)));
        Assert.NotNull(AvailabilityRules.Validate(Week(721)));
        Assert.NotNull(AvailabilityRules.Validate(Week(-1)));
        Assert.NotNull(AvailabilityRules.Validate(Week()[..6]));
        var duplicate = Week(); duplicate[0] = duplicate[1];
        Assert.NotNull(AvailabilityRules.Validate(duplicate));
        var invalidDay = Week(); invalidDay[0] = new((DayOfWeek)7, 60);
        Assert.NotNull(AvailabilityRules.Validate(invalidDay));
        Assert.NotNull(AvailabilityRules.Validate(null!));
    }
}
