using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Core;

namespace StudyFlow.Web.Data;

public sealed class TaskStore(IDbContextFactory<StudyFlowDbContext> contextFactory)
{
    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        var subjects = await db.Subjects.AsNoTracking()
            .Select(subject => new Subject(subject.Id, subject.Name, subject.Colour)).ToListAsync();
        return subjects.OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(subject => subject.Id).ToArray();
    }

    public async Task<IReadOnlyList<StudyTask>> GetTasksAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        return await db.Tasks.AsNoTracking().OrderBy(task => task.DueDate)
            .ThenByDescending(task => task.Priority).ThenBy(task => task.Id)
            .Select(task => new StudyTask(task.Id, task.SubjectId, task.Title, task.DueDate,
                task.EstimatedMinutes, task.Priority, task.IsCompleted, task.Notes)).ToListAsync();
    }

    public async Task<OperationResult<Subject>> SaveSubjectAsync(int? id, SubjectInput input)
    {
        string? error = InputValidation.Validate(input);
        if (error is not null) return OperationResult<Subject>.Fail(error);
        if (id is <= 0) return OperationResult<Subject>.Fail("Choose a valid subject.");

        string name = input.Name.Trim();
        string nameKey = name.ToUpperInvariant();
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            if (await db.Subjects.AnyAsync(subject => subject.NameKey == nameKey && subject.Id != id.GetValueOrDefault()))
                return OperationResult<Subject>.Fail("A subject with that name already exists.");

            SubjectEntity subject;
            if (id.HasValue)
            {
                var existing = await db.Subjects.FindAsync(id.Value);
                if (existing is null) return OperationResult<Subject>.Fail("This subject no longer exists.");
                subject = existing;
            }
            else
            {
                subject = new SubjectEntity();
                db.Subjects.Add(subject);
            }

            subject.Name = name;
            subject.NameKey = nameKey;
            subject.Colour = input.Colour.Trim().ToUpperInvariant();
            await db.SaveChangesAsync();
            return OperationResult<Subject>.Ok(new Subject(subject.Id, subject.Name, subject.Colour));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            return OperationResult<Subject>.Fail("A subject with that name already exists.");
        }
        catch (DbUpdateException)
        {
            return OperationResult<Subject>.Fail("The subject could not be saved. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<Subject>.Fail("The database is unavailable. Please try again.");
        }
    }

    public async Task<OperationResult<bool>> DeleteSubjectAsync(int id)
    {
        if (id <= 0) return OperationResult<bool>.Fail("Choose a valid subject.");
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            var subject = await db.Subjects.FindAsync(id);
            if (subject is null) return OperationResult<bool>.Fail("This subject no longer exists.");
            if (await db.Tasks.AnyAsync(task => task.SubjectId == id))
                return OperationResult<bool>.Fail("This subject has tasks. Delete or move its tasks first.");

            db.Subjects.Remove(subject);
            await db.SaveChangesAsync();
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateException exception) when (IsForeignKeyConstraint(exception))
        {
            return OperationResult<bool>.Fail("This subject has tasks. Delete or move its tasks first.");
        }
        catch (DbUpdateException)
        {
            return OperationResult<bool>.Fail("The subject could not be deleted. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<bool>.Fail("The database is unavailable. Please try again.");
        }
    }

    public async Task<OperationResult<StudyTask>> SaveTaskAsync(int? id, StudyTaskInput input)
    {
        string? error = InputValidation.Validate(input);
        if (error is not null) return OperationResult<StudyTask>.Fail(error);
        if (id is <= 0) return OperationResult<StudyTask>.Fail("Choose a valid task.");

        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            if (!await db.Subjects.AnyAsync(subject => subject.Id == input.SubjectId))
                return OperationResult<StudyTask>.Fail("The selected subject no longer exists. Choose another subject.");

            StudyTaskEntity task;
            if (id.HasValue)
            {
                var existing = await db.Tasks.FindAsync(id.Value);
                if (existing is null) return OperationResult<StudyTask>.Fail("This task no longer exists.");
                task = existing;
            }
            else
            {
                task = new StudyTaskEntity();
                db.Tasks.Add(task);
            }

            task.SubjectId = input.SubjectId;
            task.Title = input.Title.Trim();
            task.DueDate = input.DueDate;
            task.EstimatedMinutes = input.EstimatedMinutes;
            task.Priority = input.Priority;
            task.Notes = input.Notes?.Trim() ?? "";
            await db.SaveChangesAsync();
            return OperationResult<StudyTask>.Ok(ToStudyTask(task));
        }
        catch (DbUpdateException exception) when (IsForeignKeyConstraint(exception))
        {
            return OperationResult<StudyTask>.Fail("The selected subject no longer exists. Choose another subject.");
        }
        catch (DbUpdateException)
        {
            return OperationResult<StudyTask>.Fail("The task could not be saved. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<StudyTask>.Fail("The database is unavailable. Please try again.");
        }
    }

    public async Task<OperationResult<bool>> SetCompletedAsync(int id, bool isCompleted)
    {
        if (id <= 0) return OperationResult<bool>.Fail("Choose a valid task.");
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            var task = await db.Tasks.FindAsync(id);
            if (task is null) return OperationResult<bool>.Fail("This task no longer exists.");

            task.IsCompleted = isCompleted;
            await db.SaveChangesAsync();
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateException)
        {
            return OperationResult<bool>.Fail("The task status could not be saved. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<bool>.Fail("The database is unavailable. Please try again.");
        }
    }

    public async Task<OperationResult<bool>> DeleteTaskAsync(int id)
    {
        if (id <= 0) return OperationResult<bool>.Fail("Choose a valid task.");
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            var task = await db.Tasks.FindAsync(id);
            if (task is null) return OperationResult<bool>.Fail("This task no longer exists.");

            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateException)
        {
            return OperationResult<bool>.Fail("The task could not be deleted. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<bool>.Fail("The database is unavailable. Please try again.");
        }
    }

    public async Task<OperationResult<bool>> AddSampleTasksAsync(DateOnly today)
    {
        if (today.DayNumber < 2 || today.DayNumber > DateOnly.MaxValue.DayNumber - 5)
            return OperationResult<bool>.Fail("Choose a valid date for the sample semester.");

        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (await db.Tasks.AnyAsync())
                return OperationResult<bool>.Fail("Sample tasks can only be added to an empty task list.");

            var definitions = new[]
            {
                (Name: "Functional Programming", Colour: "#2D6655"),
                (Name: "Web Technologies", Colour: "#A56827"),
                (Name: "Discrete Mathematics", Colour: "#7357A2")
            };
            var subjects = new List<SubjectEntity>();
            foreach (var (name, colour) in definitions)
            {
                string nameKey = name.ToUpperInvariant();
                var subject = await db.Subjects.SingleOrDefaultAsync(item => item.NameKey == nameKey);
                if (subject is null)
                {
                    subject = new SubjectEntity { Name = name, NameKey = nameKey, Colour = colour };
                    db.Subjects.Add(subject);
                }
                subjects.Add(subject);
            }

            db.Tasks.AddRange(
                new StudyTaskEntity
                {
                    Subject = subjects[0], Title = "Complete the LINQ practice", DueDate = today.AddDays(1),
                    EstimatedMinutes = 90, Priority = TaskPriority.High,
                    Notes = "Filter, group, and sort the sample collection. Explain each result."
                },
                new StudyTaskEntity
                {
                    Subject = subjects[1], Title = "Build the responsive portfolio", DueDate = today.AddDays(3),
                    EstimatedMinutes = 120, Priority = TaskPriority.Medium,
                    Notes = "Check the layout on a laptop and a phone."
                },
                new StudyTaskEntity
                {
                    Subject = subjects[2], Title = "Review graph theory notes", DueDate = today.AddDays(5),
                    EstimatedMinutes = 45, Priority = TaskPriority.Low,
                    Notes = "Revisit paths, cycles, and the worked examples."
                },
                new StudyTaskEntity
                {
                    Subject = subjects[0], Title = "Read the chapter on pure functions", DueDate = today.AddDays(-1),
                    EstimatedMinutes = 60, Priority = TaskPriority.High,
                    Notes = "Write two examples of functions without side effects."
                },
                new StudyTaskEntity
                {
                    Subject = subjects[1], Title = "Sketch the page layout", DueDate = today,
                    EstimatedMinutes = 30, Priority = TaskPriority.Low, IsCompleted = true,
                    Notes = "Outline the navigation and main content."
                });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateException)
        {
            return OperationResult<bool>.Fail("Sample tasks could not be added. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<bool>.Fail("The database is unavailable. Please try again.");
        }
    }

    private static StudyTask ToStudyTask(StudyTaskEntity task) => new(
        task.Id, task.SubjectId, task.Title, task.DueDate,
        task.EstimatedMinutes, task.Priority, task.IsCompleted, task.Notes);

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };

    private static bool IsForeignKeyConstraint(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 787 or 1811 };
}
