namespace StudyFlow.Core;

public sealed record SubjectProgress(int SubjectId, string SubjectName, string Colour,
    int TotalTasks, int CompletedTasks, int OpenTasks, int OverdueTasks,
    long TotalEstimatedMinutes, long CompletedEstimatedMinutes, long RemainingEstimatedMinutes,
    double CompletionPercentage);

public sealed record TaskStatisticsSummary(int TotalTasks, int CompletedTasks, int OpenTasks, int OverdueTasks,
    long TotalEstimatedMinutes, long CompletedEstimatedMinutes, long RemainingEstimatedMinutes,
    double CompletionPercentage, IReadOnlyList<SubjectProgress> Subjects);

public static class TaskStatistics
{
    public static TaskStatisticsSummary Calculate(IEnumerable<StudyTask> tasks, IEnumerable<Subject> subjects, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(subjects);
        if (today == default) throw new ArgumentException("Choose a valid reference date.", nameof(today));
        StudyTask[] snapshot = TaskDataValidation.Snapshot(tasks);
        Subject[] subjectSnapshot = subjects.ToArray();
        var subjectIds = new HashSet<int>();
        foreach (var subject in subjectSnapshot)
            if (subject is null || subject.Id <= 0 || !subjectIds.Add(subject.Id))
                throw new ArgumentException("Subjects must have unique positive IDs.", nameof(subjects));
        if (snapshot.Any(task => !subjectIds.Contains(task.SubjectId)))
            throw new ArgumentException("Every task must belong to a supplied subject.", nameof(subjects));

        var grouped = snapshot.ToLookup(task => task.SubjectId);
        var progress = subjectSnapshot.OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(subject => subject.Id).Select(subject =>
            {
                var totals = Summarize(grouped[subject.Id], today);
                return new SubjectProgress(subject.Id, subject.Name, subject.Colour,
                    totals.TotalTasks, totals.CompletedTasks, totals.OpenTasks, totals.OverdueTasks,
                    totals.TotalEstimatedMinutes, totals.CompletedEstimatedMinutes, totals.RemainingEstimatedMinutes,
                    totals.CompletionPercentage);
            }).ToArray();
        var summary = Summarize(snapshot, today);
        return summary with { Subjects = Array.AsReadOnly(progress) };
    }

    private static TaskStatisticsSummary Summarize(IEnumerable<StudyTask> tasks, DateOnly today)
    {
        int total = 0, completed = 0, overdue = 0;
        long minutes = 0, completedMinutes = 0;
        foreach (var task in tasks)
        {
            total++;
            minutes += task.EstimatedMinutes;
            if (task.IsCompleted) { completed++; completedMinutes += task.EstimatedMinutes; }
            else if (task.DueDate < today) overdue++;
        }
        return new(total, completed, total - completed, overdue, minutes, completedMinutes,
            minutes - completedMinutes, total == 0 ? 0 : 100d * completed / total, Array.Empty<SubjectProgress>());
    }
}
