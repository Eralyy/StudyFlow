namespace StudyFlow.Core;

public enum CompletionFilter
{
    All,
    Open,
    Completed,
    Overdue
}

public enum TaskSort
{
    DueDate,
    Priority,
    Title
}

public static class TaskQuery
{
    public static IReadOnlyList<StudyTask> Apply(
        IEnumerable<StudyTask> tasks,
        string search,
        int? subjectId,
        CompletionFilter status,
        TaskSort sort,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        IEnumerable<StudyTask> query = tasks;
        string term = search?.Trim() ?? "";

        if (term.Length > 0)
        {
            query = query.Where(task =>
                task.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                task.Notes.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (subjectId.HasValue)
            query = query.Where(task => task.SubjectId == subjectId.Value);

        query = status switch
        {
            CompletionFilter.All => query,
            CompletionFilter.Open => query.Where(task => !task.IsCompleted),
            CompletionFilter.Completed => query.Where(task => task.IsCompleted),
            CompletionFilter.Overdue => query.Where(task => !task.IsCompleted && task.DueDate < today),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        IOrderedEnumerable<StudyTask> ordered = sort switch
        {
            TaskSort.DueDate => query.OrderBy(task => task.DueDate)
                .ThenByDescending(task => task.Priority).ThenBy(task => task.Id),
            TaskSort.Priority => query.OrderByDescending(task => task.Priority)
                .ThenBy(task => task.DueDate).ThenBy(task => task.Id),
            TaskSort.Title => query.OrderBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(task => task.DueDate).ThenBy(task => task.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(sort))
        };

        return ordered.ToArray();
    }
}
