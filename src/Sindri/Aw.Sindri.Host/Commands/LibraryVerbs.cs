namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri library sync [--detach] [--json] [--no-start]': the library feed's tick now.</summary>
/// <param name="index">The index verbs it runs through.</param>
#pragma warning disable MA0182 // why: dispatched by the command table in the next commit
internal sealed class LibraryVerbs(IndexVerbs index)
{
    internal const string Usage = "library sync [--detach] [--json] [--no-start]";

    internal Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        _ = (index, args, interrupt, again);
        throw new NotSupportedException();
    }
}
