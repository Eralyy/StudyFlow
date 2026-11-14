# StudyFlow

A student workload and deadline planner built with C#, Blazor, and SQLite.

Create coursework tasks, record the time you can study each weekday, and generate a daily plan. StudyFlow keeps work that cannot fit visible, so you can change the deadline, estimate, or available time.

## Run

Install the .NET 8 SDK, open the repository in VS Code, and run:

```sh
dotnet restore StudyFlow.sln
dotnet run --project src/StudyFlow.Web
```

Open **http://localhost:5188**. Keep the terminal open while using the app. Press `Ctrl+C` to stop it. If the port is occupied, run `dotnet run --project src/StudyFlow.Web --urls http://localhost:5190` and open that address.

The app creates its SQLite database and applies migrations automatically. No separate database server, API key, paid service, or application account is needed. **Try sample tasks** adds a sample semester only when the task list is empty.

## Main features

- Manage subjects and tasks, including deadlines, priorities, estimates, and notes.
- Search, filter, sort, complete, and reopen tasks.
- Save available study minutes for each weekday.
- Generate a 7, 14, or 30 day plan that respects daily capacity and deadlines.
- Split a task across days and explain why work remains unscheduled.
- View completion, estimated workload, subject progress, and upcoming deadlines.
- Validate forms, confirm deletions, and warn before leaving unsaved changes.
- Preserve records after restarting and adapt the interface to narrow screens.

## How the code connects

| Location | Responsibility |
| --- | --- |
| `src/StudyFlow.Core` | Immutable records and pure validation, queries, scheduling, and statistics |
| `src/StudyFlow.Web/Components` | Pages, reusable forms, task cards, day plans, and progress bars |
| `src/StudyFlow.Web/Services` | Form models and state for the current browser circuit |
| `src/StudyFlow.Web/Data` | Short-lived EF Core contexts, migrations, and SQLite operations |
| `tests/StudyFlow.Tests` | Unit and integration tests against temporary databases |
| `tests/e2e` | Browser scenarios with isolated application data |

Forms send validated requests to `TaskStore` or `AvailabilityStore`. These services save records with EF Core. The pages then refresh their data. `StudyPlanner.Create` receives tasks, availability, a start date, and a window length, and returns a new plan without changing the database. `TaskStatistics.Calculate` also works only on supplied data.

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

Tests use temporary databases and do not clear the application's saved tasks. See [assessment evidence](docs/ASSESSMENT.md) for recorded results and the limits of the checks.

## Saved data

The database defaults to `src/StudyFlow.Web/App_Data/studyflow.db`. It and generated build files are excluded from Git. To back up your data, stop the app and copy the database file. A fresh clone starts with an empty workspace.

Tasks, subjects, and weekly availability are stored. Plans are calculated from these records and are not stored as separate bookings. Completing a task removes it from the next plan. Statistics use whole tasks and their estimates, not a timer or partly logged study sessions.

StudyFlow is one local workspace without accounts. Tabs share the database. Public multi-user hosting would need authentication, per-user data isolation, and a separate hosting and security design.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [Architecture and developer contracts](docs/PLAN.md)
- [English defence notes](docs/DEFENCE.md)
- [Russian run and team workflow guide](docs/HANDOFF.md)
- [Assessment evidence](docs/ASSESSMENT.md)
- [Final report](docs/StudyFlow_Report.docx)
- [Editable presentation](docs/StudyFlow_Presentation.pptx)
- [Historical first-part report](docs/StudyFlow_Part1_Report.docx)

Yerali Karkinbayev presents the task and data foundation. Galymzhankyzy Aknur presents availability, scheduling, and progress. Both teammates should understand the full application and be ready to answer questions about any part.
