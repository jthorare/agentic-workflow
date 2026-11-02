using System.Reflection;
using Aw.Kernel;
using Errors = Aw.Sindri.Core.SindriErrors;

namespace Aw.Sindri.Tests.ErrorCodeChecks;

/// <summary>
/// Every error SindriErrors declares is in the catalogue and no two share a code (#1131). ErrorCatalogue.Validate does reject a duplicate, but only for the list it is given, and
/// SindriErrors.All is a hand-kept list: a code left out of it was never checked, which is how SND-LAUNCH-025 came to name two errors.
/// </summary>
public sealed class ErrorCodesUniqueCheckTests
{
    private static List<ErrorDefinition> Declared() => [.. typeof(Errors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(static f => f.FieldType == typeof(ErrorDefinition))
        .Select(static f => (ErrorDefinition)f.GetValue(null)!)];   // why: a static readonly field of this type is set by its initializer

    [Fact, Trait("Lane", "unit")]
    public void Every_declared_error_is_in_the_catalogue()
    {
        var leftOut = Declared().Where(static d => !Errors.All.Any(a => ReferenceEquals(a, d))).Select(static d => d.Code).ToList();

        Assert.True(leftOut.Count == 0, $"not in SindriErrors.All: {string.Join(", ", leftOut)}");
    }

    [Fact, Trait("Lane", "unit")]
    public void No_two_declared_errors_share_a_code()
    {
        var checkedAll = ErrorCatalogue.Validate(Declared());

        Assert.True(checkedAll.IsSuccess, checkedAll.IsError ? checkedAll.Error.Message : null);
    }
}
