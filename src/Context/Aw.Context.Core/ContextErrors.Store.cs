using Aw.Kernel;

namespace Aw.Context.Core;

public static partial class ContextErrors
{
    /// <summary>The hint a store failure's message ends with for every caller but doctor (doctor IS the fix).</summary>
    public const string StoreHint = " (fix: run sindri doctor; never delete context.db)";

    /// <summary>A store failure as a doctor row shows it: code and message, without <see cref="StoreHint"/>, so doctor never tells you to run doctor.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The row's detail.</returns>
    public static string DoctorDetail(Error error) =>
        $"{error.Code}: {(error.Message.EndsWith(StoreHint, StringComparison.Ordinal) ? error.Message[..^StoreHint.Length] : error.Message)}";
}
