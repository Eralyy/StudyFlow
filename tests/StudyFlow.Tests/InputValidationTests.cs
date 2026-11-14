using StudyFlow.Core;
using Xunit;

namespace StudyFlow.Tests;

public class InputValidationTests
{
    private static readonly StudyTaskInput ValidTask = new(
        1, "Finish C# exercise", new DateOnly(2026, 11, 20), 60, TaskPriority.Medium, "Include tests");

    [Fact]
    public void SubjectNeedsANameAndAValidColour()
    {
        Assert.Null(InputValidation.Validate(new SubjectInput("Programming", "#28604A")));
        Assert.NotNull(InputValidation.Validate(new SubjectInput("   ", "#28604A")));
        Assert.NotNull(InputValidation.Validate(new SubjectInput("Programming", "not-a-colour")));
        Assert.NotNull(InputValidation.Validate(new SubjectInput(new string('a', 61), "#28604A")));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(1440)]
    public void AcceptsTheMinimumAndMaximumTaskDuration(int minutes)
    {
        Assert.Null(InputValidation.Validate(ValidTask with { EstimatedMinutes = minutes }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(1441)]
    public void RejectsTaskDurationOutsideTheAllowedRange(int minutes)
    {
        Assert.NotNull(InputValidation.Validate(ValidTask with { EstimatedMinutes = minutes }));
    }

    [Fact]
    public void RejectsIncompleteOrMalformedTaskData()
    {
        Assert.NotNull(InputValidation.Validate(ValidTask with { Title = " " }));
        Assert.NotNull(InputValidation.Validate(ValidTask with { Title = new string('a', 121) }));
        Assert.NotNull(InputValidation.Validate(ValidTask with { SubjectId = 0 }));
        Assert.NotNull(InputValidation.Validate(ValidTask with { DueDate = DateOnly.MinValue }));
        Assert.NotNull(InputValidation.Validate(ValidTask with { Priority = (TaskPriority)99 }));
        Assert.NotNull(InputValidation.Validate(ValidTask with { Notes = new string('a', 1001) }));
    }

    [Fact]
    public void AllowsAnOverdueTaskToBeRecorded()
    {
        Assert.Null(InputValidation.Validate(ValidTask with { DueDate = new DateOnly(2020, 1, 1) }));
    }
}
