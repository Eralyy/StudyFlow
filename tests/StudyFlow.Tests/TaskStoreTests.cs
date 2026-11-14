using Microsoft.EntityFrameworkCore;
using StudyFlow.Core;
using StudyFlow.Web.Data;
using Xunit;

namespace StudyFlow.Tests;

public sealed class TaskStoreTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"StudyFlowTests-{Guid.NewGuid():N}");
    private readonly TestDbContextFactory factory;
    private readonly TaskStore store;

    public TaskStoreTests()
    {
        Directory.CreateDirectory(directory);
        var options = new DbContextOptionsBuilder<StudyFlowDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Pooling=False")
            .Options;
        factory = new TestDbContextFactory(options);
        store = new TaskStore(factory);
    }

    public async Task InitializeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            File.Delete(file);
        }
        Directory.Delete(directory);
        return Task.CompletedTask;
    }

    private async Task<int> CreateSubject(string name = "Programming")
    {
        var result = await store.SaveSubjectAsync(null, new SubjectInput(name, "#28604A"));
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!.Id;
    }

    private static StudyTaskInput TaskInput(int subjectId, string title = "Write unit tests") =>
        new(subjectId, title, new DateOnly(2026, 11, 20), 90, TaskPriority.High, "Cover error cases");

    [Fact]
    public async Task SubjectAndTaskPersistWhenReadThroughANewStore()
    {
        int subjectId = await CreateSubject();
        var saved = await store.SaveTaskAsync(null, TaskInput(subjectId));
        Assert.True(saved.IsSuccess, saved.Error);

        var reopenedStore = new TaskStore(factory);
        var subjects = await reopenedStore.GetSubjectsAsync();
        var tasks = await reopenedStore.GetTasksAsync();

        Assert.Equal("Programming", Assert.Single(subjects).Name);
        var task = Assert.Single(tasks);
        Assert.Equal(subjectId, task.SubjectId);
        Assert.Equal("Write unit tests", task.Title);
        Assert.Equal(90, task.EstimatedMinutes);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal("Cover error cases", task.Notes);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public async Task EditingTaskPreservesCompletionAndChangesItsSubject()
    {
        int firstSubject = await CreateSubject("Programming");
        int secondSubject = await CreateSubject("Web development");
        var saved = await store.SaveTaskAsync(null, TaskInput(firstSubject));
        int id = saved.Value!.Id;
        Assert.True((await store.SetCompletedAsync(id, true)).IsSuccess);

        var updated = await store.SaveTaskAsync(id, TaskInput(secondSubject, "Update the report") with { EstimatedMinutes = 120 });

        Assert.True(updated.IsSuccess, updated.Error);
        var task = Assert.Single(await new TaskStore(factory).GetTasksAsync());
        Assert.Equal(id, task.Id);
        Assert.Equal(secondSubject, task.SubjectId);
        Assert.Equal("Update the report", task.Title);
        Assert.Equal(120, task.EstimatedMinutes);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task CompletionCanBeReopened()
    {
        int subjectId = await CreateSubject();
        var saved = await store.SaveTaskAsync(null, TaskInput(subjectId));
        int id = saved.Value!.Id;

        Assert.True((await store.SetCompletedAsync(id, true)).IsSuccess);
        Assert.True(Assert.Single(await store.GetTasksAsync()).IsCompleted);
        Assert.True((await store.SetCompletedAsync(id, false)).IsSuccess);
        Assert.False(Assert.Single(await store.GetTasksAsync()).IsCompleted);
    }

    [Fact]
    public async Task CannotDeleteSubjectUntilItsTasksHaveBeenDeleted()
    {
        int subjectId = await CreateSubject();
        var saved = await store.SaveTaskAsync(null, TaskInput(subjectId));

        var blocked = await store.DeleteSubjectAsync(subjectId);

        Assert.False(blocked.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Error));
        Assert.Single(await store.GetSubjectsAsync());
        Assert.Single(await store.GetTasksAsync());

        Assert.True((await store.DeleteTaskAsync(saved.Value!.Id)).IsSuccess);
        Assert.True((await store.DeleteSubjectAsync(subjectId)).IsSuccess);
        Assert.Empty(await new TaskStore(factory).GetTasksAsync());
        Assert.Empty(await new TaskStore(factory).GetSubjectsAsync());
    }

    [Fact]
    public async Task DuplicateSubjectNamesAreRejectedIgnoringCaseAndWhitespace()
    {
        await CreateSubject("Programming");

        var duplicate = await store.SaveSubjectAsync(null, new SubjectInput("  programming  ", "#AABBCC"));

        Assert.False(duplicate.IsSuccess);
        Assert.Single(await store.GetSubjectsAsync());
    }

    [Fact]
    public async Task SubjectCanBeRenamedButNotToAnotherSubjectsName()
    {
        int first = await CreateSubject("Programming");
        int second = await CreateSubject("Web");

        var renamed = await store.SaveSubjectAsync(first, new SubjectInput("C#", "#123456"));
        var duplicate = await store.SaveSubjectAsync(second, new SubjectInput("c#", "#AABBCC"));

        Assert.True(renamed.IsSuccess, renamed.Error);
        Assert.False(duplicate.IsSuccess);
        var subjects = await store.GetSubjectsAsync();
        Assert.Equal("C#", subjects.Single(s => s.Id == first).Name);
        Assert.Equal("#123456", subjects.Single(s => s.Id == first).Colour);
        Assert.Equal("Web", subjects.Single(s => s.Id == second).Name);
    }

    [Fact]
    public async Task InvalidTaskCannotBeSavedAndDoesNotOverwriteExistingTask()
    {
        int subjectId = await CreateSubject();
        var saved = await store.SaveTaskAsync(null, TaskInput(subjectId));

        var invalidNew = await store.SaveTaskAsync(null, TaskInput(subjectId) with { Title = " " });
        var invalidUpdate = await store.SaveTaskAsync(saved.Value!.Id, TaskInput(subjectId) with { EstimatedMinutes = -30 });

        Assert.False(invalidNew.IsSuccess);
        Assert.False(invalidUpdate.IsSuccess);
        var unchanged = Assert.Single(await store.GetTasksAsync());
        Assert.Equal("Write unit tests", unchanged.Title);
        Assert.Equal(90, unchanged.EstimatedMinutes);
    }

    [Fact]
    public async Task UnknownSubjectCannotCreateAnOrphanTask()
    {
        var result = await store.SaveTaskAsync(null, TaskInput(999));

        Assert.False(result.IsSuccess);
        Assert.Empty(await store.GetTasksAsync());
    }

    [Fact]
    public async Task InvalidSubjectIsRejectedWithoutCreatingARow()
    {
        var result = await store.SaveSubjectAsync(null, new SubjectInput(" ", "#28604A"));

        Assert.False(result.IsSuccess);
        Assert.Empty(await store.GetSubjectsAsync());
    }

    [Fact]
    public async Task MissingRecordsReturnFailuresInsteadOfRecreatingOrSilentlyChangingData()
    {
        int subjectId = await CreateSubject();

        Assert.False((await store.SaveSubjectAsync(999, new SubjectInput("Missing", "#28604A"))).IsSuccess);
        Assert.False((await store.DeleteSubjectAsync(999)).IsSuccess);
        Assert.False((await store.SaveTaskAsync(999, TaskInput(subjectId))).IsSuccess);
        Assert.False((await store.DeleteTaskAsync(999)).IsSuccess);
        Assert.False((await store.SetCompletedAsync(999, true)).IsSuccess);
        Assert.Empty(await store.GetTasksAsync());
        Assert.Single(await store.GetSubjectsAsync());
    }

    [Fact]
    public async Task SampleDataCreatesAUsefulCompleteSetInAnEmptyDatabase()
    {
        DateOnly today = new(2026, 11, 15);

        var result = await store.AddSampleTasksAsync(today);

        Assert.True(result.IsSuccess, result.Error);
        var reopenedStore = new TaskStore(factory);
        var subjects = await reopenedStore.GetSubjectsAsync();
        var tasks = await reopenedStore.GetTasksAsync();
        Assert.Equal(3, subjects.Count);
        Assert.Equal(5, tasks.Count);
        Assert.Single(tasks.Where(task => task.IsCompleted));
        Assert.Single(tasks.Where(task => !task.IsCompleted && task.DueDate < today));
        Assert.All(tasks, task => Assert.Contains(subjects, subject => subject.Id == task.SubjectId));
    }

    [Fact]
    public async Task RepeatingSampleLoadDoesNotDuplicateOrChangeExistingData()
    {
        DateOnly today = new(2026, 11, 15);
        Assert.True((await store.AddSampleTasksAsync(today)).IsSuccess);
        var originalSubjects = await store.GetSubjectsAsync();
        var originalTasks = await store.GetTasksAsync();

        var repeated = await store.AddSampleTasksAsync(today.AddDays(1));

        Assert.False(repeated.IsSuccess);
        Assert.Equal(originalSubjects, await store.GetSubjectsAsync());
        Assert.Equal(originalTasks, await store.GetTasksAsync());
    }

    [Fact]
    public async Task SampleLoadPreservesAUsersNonEmptyTaskList()
    {
        int subjectId = await CreateSubject("My own subject");
        var saved = await store.SaveTaskAsync(null, TaskInput(subjectId, "My own task"));
        Assert.True(saved.IsSuccess, saved.Error);

        var result = await store.AddSampleTasksAsync(new DateOnly(2026, 11, 15));

        Assert.False(result.IsSuccess);
        Assert.Equal("My own subject", Assert.Single(await store.GetSubjectsAsync()).Name);
        Assert.Equal(saved.Value, Assert.Single(await store.GetTasksAsync()));
    }

    [Fact]
    public async Task SampleLoadReusesAMatchingSubjectWithoutChangingItsNameOrColour()
    {
        int existingId = await CreateSubject("  functional programming  ");
        var original = Assert.Single(await store.GetSubjectsAsync());

        var result = await store.AddSampleTasksAsync(new DateOnly(2026, 11, 15));

        Assert.True(result.IsSuccess, result.Error);
        var subjects = await store.GetSubjectsAsync();
        Assert.Equal(3, subjects.Count);
        Assert.Equal(original, subjects.Single(subject => subject.Id == existingId));
        var tasks = await store.GetTasksAsync();
        Assert.Equal(5, tasks.Count);
        Assert.Equal(2, tasks.Count(task => task.SubjectId == existingId));
    }

    [Fact]
    public async Task SampleLoadRollsBackAllNewRowsWhenAnInsertionFails()
    {
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectThirdSampleTask
                BEFORE INSERT ON Tasks
                WHEN NEW.Title = 'Review graph theory notes'
                BEGIN
                    SELECT RAISE(ABORT, 'Simulated write failure');
                END;
                """);
        }

        var result = await store.AddSampleTasksAsync(new DateOnly(2026, 11, 15));

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        var reopenedStore = new TaskStore(factory);
        Assert.Empty(await reopenedStore.GetTasksAsync());
        Assert.Empty(await reopenedStore.GetSubjectsAsync());
    }

    private sealed class TestDbContextFactory(DbContextOptions<StudyFlowDbContext> options)
        : IDbContextFactory<StudyFlowDbContext>
    {
        public StudyFlowDbContext CreateDbContext() => new(options);

        public Task<StudyFlowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
