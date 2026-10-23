using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>Reads the body of a "fact:prStatus" fact into its typed record.</summary>
internal static class PrStatusReader
{
    /// <summary>The fact's key.</summary>
    internal const string Key = "fact:prStatus";

    /// <summary>Reads the fact.</summary>
    /// <param name="fact">The fact as the API served it.</param>
    /// <returns>The pull request's status, or the read's error when the body is not one.</returns>
    internal static Result<PrStatusView> Of(FactView fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return AwJson.Deserialize<PrStatusView>(System.Text.Encoding.UTF8.GetBytes(fact.Body.GetRawText()), SindriApiClient.Json);
    }
}
