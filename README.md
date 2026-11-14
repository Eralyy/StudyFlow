# StudyFlow

A student workload and deadline planner built with C#, Blazor, and SQLite.

**Current delivery:** subject and task management. Available-time settings, automatic daily planning, overload warnings, and detailed progress statistics are the next team member's part. This branch is a working foundation, not the completed team final.

## Run

Install the .NET 8 SDK, open this folder in VS Code, and run:

```sh
dotnet restore StudyFlow.sln
dotnet run --project src/StudyFlow.Web
```

Open **http://localhost:5188**. Keep the terminal open while using the app; press `Ctrl+C` to stop it. If the port is occupied, use `dotnet run --project src/StudyFlow.Web --urls http://localhost:5190` and open that address.

No separate database server, API key, paid service, or account is required. The app creates its SQLite database and applies migrations automatically. **Try sample tasks** adds a sample semester only to an empty task list. The samples can be edited or deleted normally.

## What works

- Create, rename, recolour, and delete subjects.
- Create, edit, delete, complete, and reopen tasks.
- Record deadlines, estimated minutes, priorities, and notes.
- Search titles and notes, filter by subject/status, and sort by deadline, priority, or title.
- Highlight overdue unfinished tasks and show task counts.
- Validate input and prevent deleting subjects that still have tasks.
- Warn before leaving unsaved forms; confirm deletions.
- Add sample data atomically, with rollback if saving fails.
- Keep records after closing and restarting the app.
- Use the responsive interface on desktop and mobile widths.

## How the code connects

```text
Blazor page / form
    → Core validation and immutable input records
    → TaskStore
    → a new EF Core DbContext for the operation
    → SQLite

Saved records → immutable snapshots → pure TaskQuery filters → task cards
```

| Location | Responsibility |
| --- | --- |
| `src/StudyFlow.Core` | Immutable records, pure validation, and deterministic filtering/sorting |
| `src/StudyFlow.Web/Components` | Pages, reusable forms/cards, navigation, and feedback |
| `src/StudyFlow.Web/Services` | Form models and per-browser workspace/filter state |
| `src/StudyFlow.Web/Data` | EF Core model, migrations, and database operations |
| `tests/StudyFlow.Tests` | Unit tests and integration tests against real temporary SQLite files |
| `tests/e2e` | Browser scenarios using isolated databases |

`Subject` and `StudyTask` records do not expose mutable database entities. Pure functions receive data and return a result without writing to the database or modifying the input. Database writes are deliberately kept in `TaskStore`.

## Checks

```sh
dotnet build StudyFlow.sln
dotnet test StudyFlow.sln
```

For browser tests, install Node.js 20 or newer:

```sh
npm ci
npx playwright install chromium
npm run test:e2e
```

Browser tests create separate temporary databases; they do not clear the application's saved tasks. GitHub Actions builds and tests the code on Windows and Linux and runs browser scenarios on Linux.

## Data and scope

The local database is `src/StudyFlow.Web/App_Data/studyflow.db`. It and generated build files are excluded from Git. To back up your tasks, stop the application and copy the database file. A fresh clone starts with an empty workspace.

This stage is a **single local workspace** without user accounts. Different browser tabs connect to the same stored data. It is intended to run locally; authenticated multi-user hosting would require a separate design and is not claimed by this delivery.

The target is .NET 8 to match the course environment. For a future public deployment, review framework support, authentication, hosting, and database backup requirements first.

To extend the database during development:

```sh
dotnet tool restore
dotnet ef migrations add YourChangeName --project src/StudyFlow.Web --output-dir Data/Migrations
```

## Team handoff and defence

- [Easy English defence notes](docs/DEFENCE.md)
- [Task-management report with screenshots](docs/StudyFlow_Part1_Report.docx)
- [Russian run and Git handoff guide](docs/HANDOFF.md)
- [Shared contracts and remaining work](docs/PLAN.md)

The completed first part is in `main`, with its development branch retained as `feature/task-management`. The next part starts from `main` in `feature/study-planner`. Work on feature branches and review changes before merging. Each participant must understand the full application for the defence.
