using System.Globalization;

namespace Aw.Meta.Tests.MergeRun;

/// <summary>What `gh api graphql` prints for the merge-queue query, in the shapes GitHub gives.</summary>
internal static class QueueAnswers
{
    /// <summary>The base branch has no merge queue.</summary>
    internal const string Off = """{"data":{"repository":{"pullRequest":{"isMergeQueueEnabled":false,"isInMergeQueue":false,"mergeQueueEntry":null,"timelineItems":{"nodes":[]}}}}}""";

    /// <summary>A merge queue with the pull request not in it (just merged, or never enqueued).</summary>
    internal const string OnNotQueued = """{"data":{"repository":{"pullRequest":{"isMergeQueueEnabled":true,"isInMergeQueue":false,"mergeQueueEntry":null,"timelineItems":{"nodes":[]}}}}}""";

    /// <summary>The pull request is part of a stack (gh stack), with or without a merge queue on its base branch.</summary>
    internal static string Stacked(bool queue = false) =>
        """{"data":{"repository":{"pullRequest":{"isMergeQueueEnabled":QUEUE,"isInMergeQueue":false,"mergeQueueEntry":null,"stack":{"number":7},"timelineItems":{"nodes":[]}}}}}"""
            .Replace("QUEUE", queue ? "true" : "false", StringComparison.Ordinal);

    /// <summary>A merge queue holding the pull request at a position.</summary>
    internal static string Queued(int position, string state = "AWAITING_CHECKS") =>
        """{"data":{"repository":{"pullRequest":{"isMergeQueueEnabled":true,"isInMergeQueue":true,"mergeQueueEntry":{"position":{position},"state":"{state}"},"timelineItems":{"nodes":[]}}}}}"""
            .Replace("{position}", position.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{state}", state, StringComparison.Ordinal);

    /// <summary>A merge queue that removed the pull request, with GitHub's reason.</summary>
    internal static string Removed(string reason) =>
        """{"data":{"repository":{"pullRequest":{"isMergeQueueEnabled":true,"isInMergeQueue":false,"mergeQueueEntry":null,"timelineItems":{"nodes":[{"reason":"{reason}"}]}}}}}""".Replace("{reason}", reason, StringComparison.Ordinal);
}
