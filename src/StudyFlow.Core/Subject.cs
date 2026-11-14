namespace StudyFlow.Core;

public sealed record Subject(int Id, string Name, string Colour);

public sealed record SubjectInput(string Name, string Colour);
