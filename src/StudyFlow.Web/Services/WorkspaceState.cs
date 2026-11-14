using StudyFlow.Core;
using StudyFlow.Web.Data;

namespace StudyFlow.Web.Services;

// Each browser circuit keeps its own filters. Saved records live in SQLite.
public sealed class WorkspaceState(TaskStore store)
{
    public IReadOnlyList<Subject> Subjects { get; private set; } = [];
    public IReadOnlyList<StudyTask> Tasks { get; private set; } = [];
    public string Search { get; set; } = "";
    public int? SubjectFilter { get; set; }
    public CompletionFilter CompletionFilter { get; set; } = CompletionFilter.All;
    public TaskSort Sort { get; set; } = TaskSort.DueDate;

    public async Task RefreshAsync()
    {
        Subjects = await store.GetSubjectsAsync();
        Tasks = await store.GetTasksAsync();
        if (SubjectFilter.HasValue && Subjects.All(s => s.Id != SubjectFilter.Value))
            SubjectFilter = null;
    }

    public void ResetFilters()
    {
        Search = "";
        SubjectFilter = null;
        CompletionFilter = CompletionFilter.All;
        Sort = TaskSort.DueDate;
    }
}
