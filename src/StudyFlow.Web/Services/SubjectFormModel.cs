using System.ComponentModel.DataAnnotations;

namespace StudyFlow.Web.Services;

public sealed class SubjectFormModel
{
    [Required(ErrorMessage = "Enter a subject name.")]
    [StringLength(60, ErrorMessage = "Use at most 60 characters.")]
    public string Name { get; set; } = "";
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Choose a valid colour.")]
    public string Colour { get; set; } = "#2D6655";
}
