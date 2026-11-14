using StudyFlow.Core;
using Xunit;

namespace StudyFlow.Tests;

public sealed class TaskStatisticsTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly Subject[] Subjects = [new(1, "Programming", "#123456"), new(2, "Math", "#654321")];
    private static StudyTask Task(int id, int subject, int dueOffset, int minutes, bool done = false) =>
        new(id, subject, $"Task {id}", Today.AddDays(dueOffset), minutes, TaskPriority.Medium, done, "");

    [Fact]
    public void CountsCompletionOverdueAndEstimatedMinutesSeparately()
    {
        var tasks = new[] { Task(1, 1, -1, 60), Task(2, 1, -2, 90, true), Task(3, 2, 0, 30), Task(4, 2, 1, 60, true) };
        var summary = TaskStatistics.Calculate(tasks, Subjects, Today);
        Assert.Equal(4, summary.TotalTasks);
        Assert.Equal(2, summary.CompletedTasks);
        Assert.Equal(2, summary.OpenTasks);
        Assert.Equal(1, summary.OverdueTasks);
        Assert.Equal(50d, summary.CompletionPercentage);
        Assert.Equal(240, summary.TotalEstimatedMinutes);
        Assert.Equal(150, summary.CompletedEstimatedMinutes);
        Assert.Equal(90, summary.RemainingEstimatedMinutes);
        Assert.Equal(new[] { "Math", "Programming" }, summary.Subjects.Select(subject => subject.SubjectName));
        var programming = summary.Subjects.Single(subject => subject.SubjectId == 1);
        Assert.Equal(1, programming.OverdueTasks);
        Assert.Equal(60, programming.RemainingEstimatedMinutes);
    }

    [Fact]
    public void EmptySubjectsHaveZeroProgressWithoutDivisionByZero()
    {
        var result = TaskStatistics.Calculate(Array.Empty<StudyTask>(), Subjects, Today);
        Assert.Equal(0, result.TotalTasks);
        Assert.Equal(0d, result.CompletionPercentage);
        Assert.Equal(2, result.Subjects.Count);
        Assert.All(result.Subjects, subject => Assert.Equal(0d, subject.CompletionPercentage));
        Assert.Empty(TaskStatistics.Calculate(Array.Empty<StudyTask>(), Array.Empty<Subject>(), Today).Subjects);
    }

    [Fact]
    public void CalculationIsDeterministicAndDoesNotMutateInputs()
    {
        var tasks = new[] { Task(1, 1, 0, 30, true), Task(2, 1, 1, 60), Task(3, 2, -1, 90) };
        var before = tasks.ToArray();
        var first = TaskStatistics.Calculate(tasks, Subjects, Today);
        var second = TaskStatistics.Calculate(tasks.Reverse(), Subjects.Reverse(), Today);
        Assert.Equal(before, tasks);
        Assert.Equal(first.Subjects.ToArray(), second.Subjects.ToArray());
        Assert.Equal(100d / 3, first.CompletionPercentage, 10);
        Assert.Throws<NotSupportedException>(() => ((IList<SubjectProgress>)first.Subjects).Clear());
    }

    [Fact]
    public void MinuteTotalsDoNotOverflowInt32()
    {
        var tasks = new[] { Task(1, 1, 0, int.MaxValue), Task(2, 1, 1, int.MaxValue, true) };
        var result = TaskStatistics.Calculate(tasks, Subjects, Today);
        Assert.Equal(2L * int.MaxValue, result.TotalEstimatedMinutes);
    }

    [Fact]
    public void RejectsDuplicateSubjectsOrTasksAndUnknownSubjects()
    {
        Assert.Throws<ArgumentException>(() => TaskStatistics.Calculate(Array.Empty<StudyTask>(), new[] { Subjects[0], Subjects[0] }, Today));
        Assert.Throws<ArgumentException>(() => TaskStatistics.Calculate(new[] { Task(1, 9, 0, 30) }, Subjects, Today));
        Assert.Throws<ArgumentException>(() => TaskStatistics.Calculate(new[] { Task(1, 1, 0, 30), Task(1, 1, 0, 30) }, Subjects, Today));
        Assert.Throws<ArgumentException>(() => TaskStatistics.Calculate(Array.Empty<StudyTask>(), Subjects, default));
    }
}
