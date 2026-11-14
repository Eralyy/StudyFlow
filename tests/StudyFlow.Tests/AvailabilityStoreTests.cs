using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyFlow.Core;
using StudyFlow.Web.Data;
using Xunit;

namespace StudyFlow.Tests;

public sealed class AvailabilityStoreTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"StudyFlowAvailability-{Guid.NewGuid():N}");
    private readonly TestFactory factory;
    private readonly AvailabilityStore store;
    public AvailabilityStoreTests()
    {
        Directory.CreateDirectory(directory);
        factory = new(new DbContextOptionsBuilder<StudyFlowDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Pooling=False").Options);
        store = new(factory);
    }
    public async Task InitializeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync()
    {
        foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
        return Task.CompletedTask;
    }
    private static DailyAvailability[] Week(int minutes) => Enum.GetValues<DayOfWeek>()
        .Select(day => new DailyAvailability(day, minutes)).ToArray();

    [Fact]
    public async Task EmptyDatabaseReturnsDefaultsWithoutWritingThem()
    {
        var defaults = await store.GetAsync();
        Assert.Equal(7, defaults.Count);
        Assert.Equal(120, defaults.Single(item => item.Day == DayOfWeek.Saturday).Minutes);
        Assert.Equal(0, defaults.Single(item => item.Day == DayOfWeek.Sunday).Minutes);
        Assert.All(defaults.Where(item => item.Day is not DayOfWeek.Saturday and not DayOfWeek.Sunday),
            item => Assert.Equal(60, item.Minutes));
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Availability").SingleAsync());
    }

    [Fact]
    public async Task AllSevenDaysPersistAndCanBeUpdatedThroughANewStore()
    {
        var first = await store.SaveAsync(Week(30));
        Assert.True(first.IsSuccess, first.Error);
        var reopened = new AvailabilityStore(factory);
        Assert.All(await reopened.GetAsync(), item => Assert.Equal(30, item.Minutes));
        var replacement = Week(0); replacement[6] = new(DayOfWeek.Saturday, 720);
        Assert.True((await reopened.SaveAsync(replacement)).IsSuccess);
        Assert.Equal(replacement, (await new AvailabilityStore(factory).GetAsync()).ToArray());
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(7, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Availability").SingleAsync());
    }

    [Fact]
    public async Task InvalidWeeksLeavePreviouslySavedSettingsUnchanged()
    {
        Assert.True((await store.SaveAsync(Week(45))).IsSuccess);
        var duplicate = Week(60); duplicate[0] = duplicate[1];
        foreach (var invalid in new[] { Week(721), Week(-1), Week(60)[..6], duplicate })
        {
            var result = await store.SaveAsync(invalid);
            Assert.False(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        }
        Assert.All(await store.GetAsync(), item => Assert.Equal(45, item.Minutes));
    }

    [Fact]
    public async Task MidSaveFailureRollsBackEarlierDayUpdates()
    {
        Assert.True((await store.SaveAsync(Week(45))).IsSuccess);
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectWednesdayUpdate BEFORE UPDATE ON Availability
                WHEN NEW.Day = 3 BEGIN SELECT RAISE(ABORT, 'Simulated failure'); END;
                """);
        var result = await store.SaveAsync(Week(90));
        Assert.False(result.IsSuccess);
        Assert.All(await new AvailabilityStore(factory).GetAsync(), item => Assert.Equal(45, item.Minutes));
    }

    [Fact]
    public async Task FreshInsertFailureDoesNotLeavePartialWeek()
    {
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectWednesdayInsert BEFORE INSERT ON Availability
                WHEN NEW.Day = 3 BEGIN SELECT RAISE(ABORT, 'Simulated failure'); END;
                """);
        Assert.False((await store.SaveAsync(Week(90))).IsSuccess);
        await using var check = await factory.CreateDbContextAsync();
        Assert.Equal(0, await check.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Availability").SingleAsync());
    }

    [Fact]
    public async Task MigrationFromTaskManagementPreservesExistingUserData()
    {
        await using (var db = await factory.CreateDbContextAsync())
            await db.GetService<IMigrator>().MigrateAsync("20261114190816_InitialTaskManagement");
        var tasks = new TaskStore(factory);
        var subject = await tasks.SaveSubjectAsync(null, new("Programming", "#123456"));
        Assert.True(subject.IsSuccess);
        var task = await tasks.SaveTaskAsync(null, new(subject.Value!.Id, "Keep this task", new(2026, 11, 20), 90, TaskPriority.High));
        Assert.True(task.IsSuccess);
        await using (var db = await factory.CreateDbContextAsync()) await db.Database.MigrateAsync();
        Assert.Equal(task.Value, Assert.Single(await tasks.GetTasksAsync()));
        Assert.Equal(subject.Value, Assert.Single(await tasks.GetSubjectsAsync()));
        Assert.True((await store.SaveAsync(Week(60))).IsSuccess);
    }

    private sealed class TestFactory(DbContextOptions<StudyFlowDbContext> options) : IDbContextFactory<StudyFlowDbContext>
    {
        public StudyFlowDbContext CreateDbContext() => new(options);
        public Task<StudyFlowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
