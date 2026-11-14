# Explaining the task-management part

## A short introduction

“StudyFlow helps students organise assignments and deadlines. This part lets the user manage subjects and tasks, save them in a database, find relevant work, and mark it as complete. The next part will use these tasks to generate a study plan.”

## What each part does

| Part | Simple explanation |
| --- | --- |
| `StudyFlow.Web` | Runs the website. Blazor displays the pages and handles clicks and form submissions. |
| Tasks and Subjects pages | Display the lists and connect user actions to the application services. |
| `TaskEditor`, `TaskCard`, and `Notice` | Reusable pieces for task forms, task summaries, and feedback messages. |
| `WorkspaceState` | Keeps the loaded subjects and tasks together for the current browser session and refreshes them after changes. |
| `StudyFlow.Core` | Defines the shared data and pure rules for validation, filtering, and sorting. It does not access the database. |
| `TaskStore` | Coordinates saving and loading. It checks the input and applies rules before changing stored data. |
| `StudyFlowDbContext` | Connects the C# database models to SQLite through Entity Framework Core. |
| SQLite | Keeps subjects and tasks after the application closes. |
| `StudyFlow.Tests` | Checks the pure rules and database behaviour automatically. |

The normal flow is: **form → validation → TaskStore → database → refreshed page**.

## A simple demonstration

1. Create a subject called “Programming”.
2. Add “Prepare final presentation”, select the subject, and enter its deadline, priority, and estimated duration.
3. Edit the title or duration. Reload the page to demonstrate that the change was saved.
4. Search for the task and filter the list by subject or completion status.
5. Mark it as complete and show the completed filter. Reopen it if necessary.
6. Try to delete its subject. The application explains that the subject still has tasks.

## Questions you should be ready for

**Where is functional programming used?**
Validation and task filtering are pure operations. They return a result without saving data or modifying the original tasks. The query also receives today's date explicitly, which makes overdue behaviour predictable in tests.

**Is the whole application pure?**
No. Saving to a database is a side effect. We keep that work in `TaskStore`, separate from the pure rules.

**Why are the shared models records?**
They describe values. Their properties are fixed after creation, so another part can use a task without accidentally changing that snapshot.

**How do two tasks belong to the same subject?**
Each task stores a `SubjectId`. The database uses that ID to connect it to a subject.

**Why cannot I delete a subject that has tasks?**
That would leave tasks without a valid subject. Move or delete those tasks first, then remove the subject.

**What counts as overdue?**
An unfinished task whose deadline is before today. A task due today is not overdue, and a completed task is excluded.

**Why use a new database context for each operation?**
A Blazor page can stay open for a long time. A short-lived context keeps each database operation separate and is disposed afterwards.

**What does a database migration do?**
It describes changes to the database structure. The application applies its migrations at startup, so the required tables exist before the user starts working.

**What happens with invalid input?**
The form shows feedback, and the storage service validates again. Expected failures return a useful message instead of silently changing the database.

**What does this part not do yet?**
Automatic scheduling, available-time settings, overload warnings, and progress statistics belong to the next delivery. The current app is a local shared workspace, without application user accounts.

Both teammates should understand this overall flow. The project requirements allow the examiner to ask either person about any part.
