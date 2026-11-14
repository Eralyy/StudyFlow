using System.Text.RegularExpressions;

namespace StudyFlow.Core;

public static class InputValidation
{
    public static string? Validate(SubjectInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return "Enter a subject name.";
        if (input.Name.Trim().Length > 60)
            return "The subject name must be 60 characters or fewer.";

        string colour = input.Colour?.Trim() ?? "";
        if (colour.Length != 7 || !Regex.IsMatch(colour, @"\A#[0-9A-Fa-f]{6}\z"))
            return "Choose a colour in #RRGGBB format.";

        return null;
    }

    public static string? Validate(StudyTaskInput input)
    {
        if (input.SubjectId <= 0)
            return "Choose a subject for the task.";
        if (string.IsNullOrWhiteSpace(input.Title))
            return "Enter a task title.";
        if (input.Title.Trim().Length > 120)
            return "The task title must be 120 characters or fewer.";
        if (input.DueDate == default)
            return "Choose a valid due date.";
        if (input.EstimatedMinutes is < 15 or > 1440)
            return "Estimated time must be between 15 and 1440 minutes.";
        if (!Enum.IsDefined(input.Priority))
            return "Choose a valid task priority.";
        if ((input.Notes?.Trim().Length ?? 0) > 1000)
            return "Notes must be 1000 characters or fewer.";

        return null;
    }
}
