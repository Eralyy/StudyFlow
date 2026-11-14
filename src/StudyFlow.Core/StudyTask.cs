namespace StudyFlow.Core;

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3
}

public sealed record StudyTask(
    int Id,
    int SubjectId,
    string Title,
    DateOnly DueDate,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool IsCompleted,
    string Notes);

public sealed record StudyTaskInput(
    int SubjectId,
    string Title,
    DateOnly DueDate,
    int EstimatedMinutes,
    TaskPriority Priority,
    string Notes = "");
