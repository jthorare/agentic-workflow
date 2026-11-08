namespace Aw.Kernel.Dates;

/// <summary>The error the due-word parser answers with. Its code belongs to the todo catalogue (todo spec D7); it lives here because the parser does.</summary>
public static class DueErrors
{
    /// <summary>SND-TODO-003: a due word that looks like a date and isn't one.</summary>
    public static readonly ErrorDefinition BadDue = new("SND-TODO-003", ErrorKind.Invalid, "Not a date Sindri can read");
}
