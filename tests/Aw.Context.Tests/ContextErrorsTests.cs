using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Tests;

public sealed class ContextErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ContextErrors.All).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void S4_allocates_040_and_042_to_048_and_leaves_041_retired_and_049_reserved()
    {
        var codes = ContextErrors.All.Select(e => e.Code).Where(c => string.CompareOrdinal(c, "SND-CONTEXT-040") >= 0 && string.CompareOrdinal(c, "SND-CONTEXT-050") < 0).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(
            ["SND-CONTEXT-040", "SND-CONTEXT-042", "SND-CONTEXT-043", "SND-CONTEXT-044", "SND-CONTEXT-045", "SND-CONTEXT-046", "SND-CONTEXT-047", "SND-CONTEXT-048"],
            codes);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_heavy_lock_and_an_unreadable_repo_are_tool_errors_and_the_rest_of_S4_are_attention()
    {
        Assert.DoesNotContain(ContextErrors.HeavyLockBusy.Code, ContextErrors.Attention);
        Assert.DoesNotContain(ContextErrors.ExtractionUnreadable.Code, ContextErrors.Attention);
        ErrorDefinition[] attention =
            [ContextErrors.LabelingUnavailable, ContextErrors.ReviewFileNoMatch, ContextErrors.NoDraftMap, ContextErrors.RepoOutsideScope, ContextErrors.PromoteRefused, ContextErrors.ReviewTargetStale];
        Assert.All(attention, e => Assert.Contains(e.Code, ContextErrors.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_page_concept_or_a_stale_page_key_at_write_is_attention() =>
        Assert.All([ContextErrors.PageConceptUnknown, ContextErrors.PageKeyUnknown], e => Assert.Contains(e.Code, ContextErrors.Attention));

    [Fact, Trait("Lane", "unit")]
    public void The_S5B_sync_codes_are_056_to_059_and_a_running_or_unknown_sync_is_attention()
    {
        Assert.Equal(["SND-CONTEXT-056", "SND-CONTEXT-057", "SND-CONTEXT-058", "SND-CONTEXT-059"],
            new[] { ContextErrors.SyncRunning, ContextErrors.ModelAnswerRefused, ContextErrors.SyncSourceUnknown, ContextErrors.SyncCapped }.Select(e => e.Code), StringComparer.Ordinal);
        Assert.Equal((ErrorKind.Conflict, ErrorKind.NotFound), (ContextErrors.SyncRunning.Kind, ContextErrors.SyncSourceUnknown.Kind));
        Assert.All([ContextErrors.SyncRunning, ContextErrors.SyncSourceUnknown], e => Assert.Contains(e.Code, ContextErrors.Attention));
        Assert.All([ContextErrors.ModelAnswerRefused, ContextErrors.SyncCapped], e => Assert.DoesNotContain(e.Code, ContextErrors.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_S6_Linear_codes_are_070_to_073_and_none_is_attention()
    {
        ErrorDefinition[] linear = [ContextErrors.LinearUrlRefused, ContextErrors.LinearKeyRefused, ContextErrors.LinearUnexpectedReply, ContextErrors.LinearRecordNotFound];

        Assert.Equal(["SND-CONTEXT-070", "SND-CONTEXT-071", "SND-CONTEXT-072", "SND-CONTEXT-073"], linear.Select(e => e.Code), StringComparer.Ordinal);
        Assert.Equal([ErrorKind.Invalid, ErrorKind.Forbidden, ErrorKind.Unavailable, ErrorKind.NotFound], linear.Select(e => e.Kind));
        Assert.All(linear, e => Assert.Contains(e, ContextErrors.All));
        Assert.All(linear, e => Assert.DoesNotContain(e.Code, ContextErrors.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_S_SK_recognizer_codes_are_130_and_131_and_132_stays_reserved()
    {
        Assert.Equal(["SND-CONTEXT-130", "SND-CONTEXT-131"], new[] { ContextErrors.RecognizerInvalid, ContextErrors.RecognizerOverCap }.Select(e => e.Code), StringComparer.Ordinal);
        Assert.DoesNotContain(ContextErrors.All, e => string.Equals(e.Code, "SND-CONTEXT-132", StringComparison.Ordinal));
        Assert.All([ContextErrors.RecognizerInvalid, ContextErrors.RecognizerOverCap], e => Assert.Equal(ErrorKind.Invalid, e.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_S_H_history_codes_are_160_and_161_are_searched_row_codes_and_never_attention()
    {
        Assert.Equal(["SND-CONTEXT-160", "SND-CONTEXT-161"], new[] { ContextErrors.HistoryDeadline, ContextErrors.IntentNotSynced }.Select(e => e.Code), StringComparer.Ordinal);
        Assert.Equal((ErrorKind.Timeout, ErrorKind.Unavailable), (ContextErrors.HistoryDeadline.Kind, ContextErrors.IntentNotSynced.Kind));
        Assert.All([ContextErrors.HistoryDeadline, ContextErrors.IntentNotSynced], e => Assert.DoesNotContain(e.Code, ContextErrors.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_graphify_limit_code_is_133_an_invalid_input()
    {
        Assert.Equal("SND-CONTEXT-133", ContextErrors.GraphifyLimitInvalid.Code);
        Assert.Equal(ErrorKind.Invalid, ContextErrors.GraphifyLimitInvalid.Kind);
        Assert.Contains(ContextErrors.GraphifyLimitInvalid, ContextErrors.All);
    }
}
