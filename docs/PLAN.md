# Architecture and developer contracts

## Structure

`StudyFlow.Core` contains the data records and pure rules. It has no database or browser dependency. `StudyFlow.Web` contains the Blazor interface, circuit state, and persistence. `StudyFlow.Tests` checks both the pure rules and database behaviour.

The editable architecture diagram is slide 5 of `StudyFlow_Presentation.pptx`. Pages call storage services for writes and pure Core functions for calculations. Database entities stay internal to the data layer. Public methods return record snapshots.

## Records and task storage

```csharp
Subject(int Id, string Name, string Colour)
StudyTask(int Id, int SubjectId, string Title, DateOnly DueDate,
    int EstimatedMinutes, TaskPriority Priority, bool IsCompleted, string Notes)
SubjectInput(string Name, string Colour)
StudyTaskInput(int SubjectId, string Title, DateOnly DueDate,
    int EstimatedMinutes, TaskPriority Priority, string Notes = "")
DailyAvailability(DayOfWeek Day, int Minutes)
```

`TaskPriority` is Low = 1, Medium = 2, or High = 3. Durations are whole minutes. EstimatedMinutes is the whole task estimate, not remaining tracked time.

`TaskStore` receives an `IDbContextFactory<StudyFlowDbContext>` and creates a new context for every operation.

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

A null ID creates a record. An existing ID edits it. Check IsSuccess before using Value. Error describes an expected failure. Editing a task preserves completion.

Names are trimmed and unique without regard to case. Subjects with tasks cannot be deleted. Limits are 60 characters for subject names, 120 for task titles, 1,000 for notes, and 15–1,440 minutes per task. Colours use `#RRGGBB`. Past deadlines are allowed.

The sample loader requires an empty task list, reuses matching subjects, and inserts all new rows in one transaction.

## Availability

`AvailabilityStore.GetAsync()` returns seven DailyAvailability records. For unsaved days, the defaults are 60 minutes on weekdays, 120 on Saturday, and 0 on Sunday.

`AvailabilityStore.SaveAsync(IReadOnlyList<DailyAvailability>)` returns `OperationResult<bool>`. Validation requires each weekday exactly once and 0–720 minutes per day. All seven rows are saved atomically.

## Pure calculations

```csharp
TaskQuery.Apply(tasks, search, subjectId, status, sort, today)
StudyPlanner.Create(tasks, availability, startDate, days = 14)
TaskStatistics.Calculate(tasks, subjects, today)
```

TaskQuery returns a filtered and sorted snapshot. Search checks titles and notes without regard to case. Status options are All, Open, Completed, and Overdue. Sort options are DueDate, Priority, and Title. Overdue means unfinished with a deadline before the supplied today.

StudyPlanner validates the input and considers only unfinished tasks. It orders by deadline, descending priority, then ID. A monotone day cursor assigns minutes up to daily capacity, including the deadline day. A task may span days. The Core API accepts 1–90 days; the interface offers 7, 14, or 30.

The result is a StudyPlan with read-only PlannedDay and UnscheduledTask collections. Each day reports capacity and StudyAllocation records. Unscheduled reasons are:

| Reason | Exact meaning |
| --- | --- |
| Overdue | The deadline is before the chosen plan start, which may differ from today. |
| InsufficientTime | Work remains for a deadline inside the planning window. |
| BeyondHorizon | Work remains and the deadline is after the planning window. |

The planner does not mutate inputs or save allocations. Its main cost is sorting tasks, followed by a forward allocation pass: O(n log n + d + allocations), with O(n + d) storage for typical output.

TaskStatistics returns overall and per-subject totals, completion percentage, and estimated minutes. It uses an explicit reference date. Empty completion is 0%. Estimates for completed tasks are not measured study time. Totals use long integers for accumulated minutes.

## Persistence and state

SQLite defaults to `src/StudyFlow.Web/App_Data/studyflow.db`. `ConnectionStrings:StudyFlow` overrides it for an isolated environment. Startup applies EF Core migrations. The availability migration adds its own table and preserves existing subjects and tasks.

`WorkspaceState` is scoped to the Blazor circuit. Pages reload data after writes. Availability settings are stored; the selected start/window and generated plan are current-view state. A plan is recalculated from current open tasks.

No personal database or generated build files belong in Git. To add a schema change:

```sh
dotnet tool restore
dotnet ef migrations add YourChangeName --project src/StudyFlow.Web --output-dir Data/Migrations
```

## Boundaries

The app serves one local workspace without authentication. Public multi-user operation needs account isolation and a separate hosting design. Weekly availability has no dated exceptions. Tasks are completed as a whole, without a study timer. These are scope choices, not hidden features.
