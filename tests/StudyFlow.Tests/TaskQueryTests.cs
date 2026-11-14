using StudyFlow.Core;
using Xunit;

namespace StudyFlow.Tests;

public class TaskQueryTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static StudyTask Item(int id, int subjectId = 1, string title = "Assignment",
        int days = 0, TaskPriority priority = TaskPriority.Medium, bool completed = false, string notes = "") =>
        new(id, subjectId, title, Today.AddDays(days), 60, priority, completed, notes);

    [Fact]
    public void OverdueIncludesOnlyUnfinishedTasksBeforeToday()
    {
        StudyTask[] tasks = [
            Item(1, days: -1),
            Item(2, days: -2, completed: true),
            Item(3, days: 0),
            Item(4, days: 1)
        ];

        var result = TaskQuery.Apply(tasks, "", null, CompletionFilter.Overdue, TaskSort.DueDate, Today);

        Assert.Equal(new[] { 1 }, result.Select(t => t.Id));
    }

    [Fact]
    public void CombinesSearchSubjectAndCompletionFilters()
    {
        StudyTask[] tasks = [
            Item(1, subjectId: 1, title: "Prepare C# defence"),
            Item(2, subjectId: 2, title: "Prepare C# defence"),
            Item(3, subjectId: 1, title: "Prepare C# defence", completed: true),
            Item(4, subjectId: 1, title: "Write report")
        ];

        var result = TaskQuery.Apply(tasks, "DEFENCE", 1, CompletionFilter.Open, TaskSort.DueDate, Today);

        Assert.Equal(new[] { 1 }, result.Select(t => t.Id));
    }

    [Fact]
    public void SearchAlsoFindsTextInNotesIgnoringCase()
    {
        StudyTask[] tasks = [Item(1, notes: "Include UML diagram"), Item(2, notes: "No attachments")];

        var result = TaskQuery.Apply(tasks, "uml", null, CompletionFilter.All, TaskSort.DueDate, Today);

        Assert.Equal(new[] { 1 }, result.Select(t => t.Id));
    }

    [Fact]
    public void PrioritySortPutsHighPriorityFirstAndHasDeterministicTies()
    {
        StudyTask[] tasks = [
            Item(4, priority: TaskPriority.High),
            Item(1, priority: TaskPriority.Low),
            Item(3, priority: TaskPriority.Medium),
            Item(2, priority: TaskPriority.High)
        ];

        var result = TaskQuery.Apply(tasks, "", null, CompletionFilter.All, TaskSort.Priority, Today);

        Assert.Equal(new[] { 2, 4, 3, 1 }, result.Select(t => t.Id));
    }

    [Fact]
    public void DueDateSortPlacesEarlierDeadlinesFirst()
    {
        StudyTask[] tasks = [Item(3, days: 2), Item(2, days: -1), Item(1, days: -1)];

        var result = TaskQuery.Apply(tasks, "", null, CompletionFilter.All, TaskSort.DueDate, Today);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(t => t.Id));
    }

    [Fact]
    public void CompletedFilterAndTitleSortWorkTogether()
    {
        StudyTask[] tasks = [
            Item(1, title: "Zebra", completed: true),
            Item(2, title: "Alpha", completed: true),
            Item(3, title: "Before everything", completed: false)
        ];

        var result = TaskQuery.Apply(tasks, "", null, CompletionFilter.Completed, TaskSort.Title, Today);

        Assert.Equal(new[] { 2, 1 }, result.Select(t => t.Id));
    }

    [Fact]
    public void QueryDoesNotChangeTheInputAndCanBeRepeated()
    {
        StudyTask[] tasks = [Item(3, title: "Second", days: 1), Item(1, title: "First", days: -1)];
        StudyTask[] original = tasks.ToArray();

        var first = TaskQuery.Apply(tasks, "", null, CompletionFilter.All, TaskSort.DueDate, Today);
        var second = TaskQuery.Apply(tasks, "", null, CompletionFilter.All, TaskSort.DueDate, Today);

        Assert.Equal(original, tasks);
        Assert.Equal(first, second);
        Assert.Equal(new[] { 1, 3 }, first.Select(t => t.Id));
    }

    [Fact]
    public void EmptyInputAndNoMatchesReturnEmptyResults()
    {
        Assert.Empty(TaskQuery.Apply([], "", null, CompletionFilter.All, TaskSort.DueDate, Today));
        Assert.Empty(TaskQuery.Apply([Item(1)], "missing", null, CompletionFilter.All, TaskSort.DueDate, Today));
    }
}
