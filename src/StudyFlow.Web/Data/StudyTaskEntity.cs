using StudyFlow.Core;

namespace StudyFlow.Web.Data;

internal sealed class StudyTaskEntity
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Title { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public int EstimatedMinutes { get; set; }
    public TaskPriority Priority { get; set; }
    public bool IsCompleted { get; set; }
    public string Notes { get; set; } = "";
    public SubjectEntity Subject { get; set; } = null!;
}
