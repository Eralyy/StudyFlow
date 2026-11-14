# Using StudyFlow

Start the app from the repository root with `dotnet run --project src/StudyFlow.Web`, then open http://localhost:5188. Keep the terminal running.

## Add your coursework

1. Open **Subjects**. Enter a name, choose a colour, and save it.
2. Open **My tasks**, then **New task**.
3. Enter a title, choose the subject, and set a deadline.
4. Estimate the work in minutes, choose a priority, and add useful notes.
5. Save. The task appears in the list.

Task estimates must be 15–1,440 minutes. Subject names must be unique, even if letter case differs. A past deadline is allowed so overdue coursework can still be recorded.

Search checks task titles and notes. Filter by subject or status and sort by deadline, priority, or title. Mark a task complete with its check box. Clear it to reopen the task. **Edit** changes details while keeping the completion state.

Deleting a task asks for confirmation. A subject with related tasks cannot be deleted. Move its tasks to another subject or delete them first.

## Plan your study time

1. Open **Study planner**.
2. Enter available minutes for every weekday. Use 0 for a day off. Each day allows up to 720 minutes.
3. Select **Save availability**. These settings remain after a restart.
4. Choose a start date today or later and a plan length of 7, 14, or 30 days.
5. Select **Generate plan**.

The planner loads current unfinished tasks. Earlier deadlines receive time first. Priority breaks ties between equal deadlines, and task ID provides a stable final order. A task can be split between days. No day exceeds its available minutes, and no allocation falls after the task's deadline.

The start date and window control the current calculation. The plan is a suggestion, not a saved appointment calendar or a record of hours already studied.

## Understand unscheduled work

- **Deadline before the plan start:** the task has no eligible day. Change its deadline or select an earlier allowed start.
- **Not enough time before the deadline:** some estimated work does not fit. Add availability, correct an inaccurate estimate, or adjust the deadline if appropriate.
- **Outside the planning window:** work remains for a task due after the selected window. Generate a longer plan to inspect more days.

A warning does not delete or complete a task. Remaining minutes stay visible. Save changes and generate the plan again.

## Read progress

**Progress** shows total, open, completed, and overdue tasks, completion percentage, estimated minutes, and subject results. It also lists the nearest upcoming deadlines.

Completion percentage is completed tasks divided by all tasks. An empty workspace shows 0%. Completed estimated minutes sum the estimates of completed tasks. They do not measure actual time spent.

## Sample data and backup

**Try sample tasks** is available when no tasks exist. It adds editable examples and reuses matching subjects. The insert uses one transaction, so failure does not leave a partial sample set.

To back up your work, stop the app with `Ctrl+C` and copy `src/StudyFlow.Web/App_Data/studyflow.db`. Do not replace it while the app is running. Weekly availability is in the same database.

## If something goes wrong

- If the browser cannot connect, check that the terminal is running and use the address it reports.
- If port 5188 is occupied, run `dotnet run --project src/StudyFlow.Web --urls http://localhost:5190`.
- If a form will not save, read the feedback and correct the highlighted input.
- If database loading fails, keep the saved database and use the retry action. Check the terminal for the underlying error.
- A fresh Git clone has no personal tasks because the database is excluded from Git.


## Run a published local build

From the repository root:

```sh
dotnet publish src/StudyFlow.Web -c Release -o artifacts/publish
cd artifacts/publish
dotnet StudyFlow.Web.dll --urls http://127.0.0.1:5192
```

Open http://127.0.0.1:5192. This is a local publish folder, not a public deployment. It uses its own `App_Data/studyflow.db` under that folder unless you supply a connection-string override. The published app applies migrations automatically. Stop it before copying a database backup into the folder.
