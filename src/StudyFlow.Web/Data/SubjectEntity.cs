namespace StudyFlow.Web.Data;

internal sealed class SubjectEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string Colour { get; set; } = "";
    public List<StudyTaskEntity> Tasks { get; set; } = [];
}
