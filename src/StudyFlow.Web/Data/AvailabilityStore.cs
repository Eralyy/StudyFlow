using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Core;

namespace StudyFlow.Web.Data;

public sealed class AvailabilityStore(IDbContextFactory<StudyFlowDbContext> contextFactory)
{
    public async Task<IReadOnlyList<DailyAvailability>> GetAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        var saved = await db.Availability.AsNoTracking().ToDictionaryAsync(item => item.Day, item => item.Minutes);
        return Array.AsReadOnly(Enum.GetValues<DayOfWeek>().Select(day => new DailyAvailability(day,
            saved.TryGetValue(day, out int minutes) ? minutes : day switch
            {
                DayOfWeek.Sunday => 0,
                DayOfWeek.Saturday => 120,
                _ => 60
            })).ToArray());
    }

    public async Task<OperationResult<bool>> SaveAsync(IReadOnlyList<DailyAvailability> availability)
    {
        var snapshot = availability?.ToArray();
        string? error = AvailabilityRules.Validate(snapshot!);
        if (error is not null) return OperationResult<bool>.Fail(error);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var existing = await db.Availability.ToDictionaryAsync(item => item.Day);
            foreach (var item in snapshot!)
            {
                if (existing.TryGetValue(item.Day, out var entity)) entity.Minutes = item.Minutes;
                else db.Availability.Add(new AvailabilityEntity { Day = item.Day, Minutes = item.Minutes });
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return OperationResult<bool>.Ok(true);
        }
        catch (DbUpdateException)
        {
            return OperationResult<bool>.Fail("Your available time could not be saved. Please try again.");
        }
        catch (SqliteException)
        {
            return OperationResult<bool>.Fail("The database is unavailable. Please try again.");
        }
    }
}
