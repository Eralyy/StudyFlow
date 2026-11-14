using System.ComponentModel.DataAnnotations;
using StudyFlow.Core;

namespace StudyFlow.Web.Services;

public sealed class TaskFormModel
{
    [Required(ErrorMessage = "Enter a task title.")]
    [StringLength(120, ErrorMessage = "Use at most 120 characters.")]
    public string Title { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Choose a subject.")]
    public int SubjectId { get; set; }
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);
    [Range(15, 1440, ErrorMessage = "Estimated time must be between 15 and 1440 minutes.")]
    public int EstimatedMinutes { get; set; } = 60;
    [EnumDataType(typeof(TaskPriority))]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    [StringLength(1000, ErrorMessage = "Use at most 1000 characters.")]
    public string Notes { get; set; } = "";

    public StudyTaskInput ToInput() => new(SubjectId, Title, DateOnly.FromDateTime(DueDate), EstimatedMinutes, Priority, Notes);
    public static TaskFormModel FromTask(StudyTask task) => new()
    {
        Title = task.Title, SubjectId = task.SubjectId, DueDate = task.DueDate.ToDateTime(TimeOnly.MinValue),
        EstimatedMinutes = task.EstimatedMinutes, Priority = task.Priority, Notes = task.Notes
    };
}
