# Explaining StudyFlow

## Opening

“StudyFlow helps students manage coursework and decide when to study. A user adds tasks with deadlines and estimates, enters available time for each weekday, and gets a daily plan. If work does not fit, the app shows what is left instead of hiding it.”

## Presentation timing

Aim for 16–18 minutes including the demonstration, leaving time for questions. The required defence is 15–20 minutes and either teammate may be asked about any part.

| Slides | Speaker | Approximate time |
| --- | --- | --- |
| 1–4: purpose, method, technology | Yerali | 4 minutes |
| 5–6: architecture and task flow | Yerali | 3 minutes |
| 7–9: planner and progress | Aknur | 5 minutes |
| 10: verification | Aknur | 1 minute |
| 11: live demonstration | Both | 3 minutes |
| 12: roles and next work | Both | 1 minute |

Use the short speaker notes inside the PowerPoint. Explain the idea in your own words rather than reading every line.

## What each part does

| Part | A simple explanation |
| --- | --- |
| Blazor pages | Display tasks, subjects, plans, and progress, and respond to clicks. |
| `TaskEditor` | Reuses the same form for creating and editing a task. |
| `TaskCard` and `DayPlanCard` | Show one task or one day's planned work. |
| `ProgressMeter` | Displays a labelled completion bar. |
| `WorkspaceState` | Keeps loaded tasks, subjects, and filters for the current browser circuit. |
| `TaskStore` | Validates and saves subjects and tasks. |
| `AvailabilityStore` | Loads and saves the seven weekday time limits. |
| `StudyFlowDbContext` | Maps C# entities to SQLite tables using EF Core. |
| `StudyFlow.Core` | Holds immutable records and calculations without database access. |
| `StudyPlanner` | Allocates unfinished work to days before deadlines. |
| `TaskStatistics` | Calculates counts, completion percentages, and estimated minutes. |

## The data flow

“A form collects input. The service validates it and opens a database context. EF Core saves the change in SQLite. The page refreshes its data. The planner receives those task records and calculates a new result without saving or modifying them.”

## The planner in easy terms

“Imagine Monday has 60 free minutes, Tuesday has 60, and Wednesday has 30. A task needing 60 minutes is placed on Monday. Another task needing 90 minutes uses Tuesday and Wednesday. If both must finish by Wednesday, there is no time left for a third task. The app reports its remaining minutes.”

The order is deadline first, then higher priority, then task ID. Completed tasks are skipped. Tasks due before the chosen plan start cannot be scheduled. Allocations respect the daily limit and deadline, including the deadline day itself.

## Demonstration

1. Create a subject named “Defence demo”. Add a task with a future deadline and a 60 minute estimate.
2. Edit its title, save, and reload to show persistence.
3. Open the planner. Set a small amount of availability and save it.
4. Generate a plan and point to a task's allocation.
5. Make a task exceed the capacity before its deadline. Regenerate and explain the warning.
6. Complete the task and open Progress. Explain the change in counts.

Do not reset the normal database for the demonstration. Remove the demo task and subject afterwards if needed.

## Questions to practise

**Where is functional programming used?**
Validation, queries, planning, and statistics are pure calculations. They return results without saving data or changing the supplied records.

**Why is the planner predictable?**
It receives the start date explicitly and uses a fixed order. The same tasks, availability, start date, and window produce the same plan.

**Is the whole program pure?**
No. Database writes are side effects. They stay in the storage services, separate from pure rules.

**Why records?**
They represent snapshots with properties fixed after creation. A calculation cannot accidentally edit a stored task through that snapshot.

**Why can a task appear on several days?**
Its estimate may exceed one day's free time. The planner uses part on one day and continues on the next eligible day.

**Does it always find the best possible schedule?**
It follows a deadline-first rule with deterministic tie-breaks. It does not claim to maximise grades or task value. Users can change priorities, estimates, and availability.

**What does progress measure?**
Whole-task completion and estimated minutes. It does not measure actual study time or partly completed sessions.

**What is saved?**
Subjects, tasks, and weekly availability. The plan is recalculated from them rather than saved as appointments.

**Why SQLite?**
It stores data in one local file without a separate server, making the project easy to run during the defence.

**What protects the data?**
Validation, unique subject names, foreign keys, protected subject deletion, and transactions for multi-row changes. Tests use temporary databases.

**Why a new database context per operation?**
A Blazor circuit can stay open for a long time. A short-lived context keeps operations separate and is disposed afterwards.

**What are the limits?**
One local workspace without accounts, repeating weekly availability, no timer, and no dated availability exceptions.

**What should both teammates understand?**
The full user flow, database relationships, pure and impure parts, scheduling order, warnings, and test evidence.
