# Scope and shared contract

## Delivery boundaries

| Task management | Study planning, next delivery |
| --- | --- |
| Subject and task forms, validation, CRUD | Available study time by day |
| SQLite persistence and relationships | Allocation of unfinished work before deadlines |
| Search, subject/status filters, sorting | Unallocated-work and overload warnings |
| Complete and reopen tasks | Progress statistics and planning screens |
| Tests for rules and persistence | Tests for scheduling and its complete user flow |

This division describes development responsibilities, not proof of authorship. The Git history should reflect the actual changes made by each contributor.

## Shared data

All public data records are in `StudyFlow.Core`:

```csharp
Subject(int Id, string Name, string Colour)
StudyTask(int Id, int SubjectId, string Title, DateOnly DueDate,
    int EstimatedMinutes, TaskPriority Priority, bool IsCompleted, string Notes)
SubjectInput(string Name, string Colour)
StudyTaskInput(int SubjectId, string Title, DateOnly DueDate,
    int EstimatedMinutes, TaskPriority Priority, string Notes = "")
```

`TaskPriority` is `Low = 1`, `Medium = 2`, or `High = 3`. Durations use whole minutes. `EstimatedMinutes` describes the task's estimated duration; this part supports completion of a whole task, not partial time tracking. The planner should exclude completed tasks.

## Loading and saving

The pages use a scoped `WorkspaceState` to load and refresh the shared lists. The SQLite file defaults to `src/StudyFlow.Web/App_Data/studyflow.db`; `ConnectionStrings:StudyFlow` can override it for an isolated environment. Database migrations are applied at startup. Neither the database nor test data files belong in Git.

`StudyFlow.Web.Data.TaskStore` receives an `IDbContextFactory<StudyFlowDbContext>`. It exposes:

| Method | Awaited result |
| --- | --- |
| `GetSubjectsAsync()` | `IReadOnlyList<Subject>` |
| `GetTasksAsync()` | `IReadOnlyList<StudyTask>` |
| `SaveSubjectAsync(int? id, SubjectInput input)` | `OperationResult<Subject>` |
| `SaveTaskAsync(int? id, StudyTaskInput input)` | `OperationResult<StudyTask>` |
| `DeleteSubjectAsync(int id)` | `OperationResult<bool>` |
| `SetCompletedAsync(int id, bool isCompleted)` | `OperationResult<bool>` |
| `DeleteTaskAsync(int id)` | `OperationResult<bool>` |
| `AddSampleTasksAsync(DateOnly today)` | `OperationResult<bool>` |

A null ID creates a new record. An existing ID updates that record. Check `IsSuccess` before using `Value`; `Error` explains an expected failure. Editing a task preserves its completion status.

Sample tasks can only be added to a workspace with no tasks. The store reuses matching subjects and saves the complete sample in one transaction, so a failure leaves no partial sample data.

Subject names are trimmed and unique without regard to letter case. A subject with tasks cannot be deleted. Past deadlines are allowed, because users may need to record overdue work. The current validation limits are 60 characters for subject names, 120 for task titles, 1,000 for notes, and 15–1,440 minutes per task. Subject colours use `#RRGGBB`.

## Pure task queries

`TaskQuery.Apply(tasks, search, subjectId, status, sort, today)` returns a filtered and sorted snapshot without changing its input. Search checks titles and notes without regard to case. The status options are `All`, `Open`, `Completed`, and `Overdue`; sort options are `DueDate`, `Priority`, and `Title`.

For the scheduling feature, keep the same separation: tasks + availability + explicit start date → a new plan + unallocated work. Save settings outside the pure calculation. Agree any shared-model changes before changing existing screens or tests.

## What remains for the final assessment

This delivery establishes the subject/task foundation. It does not complete all 20 grading criteria. The remaining work includes the scheduling feature, its tests and user flow, final browser/device checks, a performance review, complete user documentation, presentation, and joint defence preparation. Authentication and public hosting are not part of this local first delivery; whether they are needed should follow the final agreed scope.
