using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-LIB-404: no design doc the caller may read has that id (PR review page spec §9).</summary>
    public static readonly ErrorDefinition LibraryDocNotFound = new("SND-LIB-404", ErrorKind.NotFound, "No such design doc");
}
