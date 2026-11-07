using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>A `skillWaiting` row's `ownerWaiting` as the server writes it (skill flows spec §5.4): null fields are left out, and the list still reads.</summary>
[Lane(TestLane.Unit)]
public sealed class OwnerWaitingWireTests
{
    // A noResult row: the server leaves out 'text' and 'detail' because both are null.
    private const string NoResultList =
        """{ "rows": [ { "id": "skill:WFI-1", "kind": "skillWaiting", "label": "skill · waiting on you", "subject": { "kind": "instance", "id": "WFI-1" }, "item": "ITM-1", "title": "Ship it", "reason": "Shape the idea stopped without giving an answer. Open the session to see why.", "repo": null, "urgency": "pulse", "since": "2026-01-01T00:05:00.0000000Z", "recordedOnly": false, "notice": null, "answerKind": null, "primaryAction": "ApplyCommand:takeover", "actions": [ "ApplyCommand:takeover" ], "cli": "sindri takeover ITM-1/step/1", "open": { "route": "workflow/ITM-1/step/1", "label": "Open the run" }, "ownerWaiting": { "launchId": "LCH-1", "reason": "noResult", "resultPath": "/run/result-1.json", "instance": "ITM-1/step/1", "state": "review", "attempt": 1 } } ], "empty": null, "asOf": { "cursor": 7, "ledgerHead": 7, "projectedAt": "2026-01-01T00:05:00.0000000Z", "rebuilding": false, "stalled": false } }""";

    [Fact]
    public void A_row_without_text_and_detail_reads_with_both_null()
    {
        var read = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(NoResultList), SindriApiClient.Json);

        Assert.False(read.IsError, read.IsError ? read.Error.Message : "");
        var waiting = Assert.Single(read.Value.Rows).OwnerWaiting!;
        Assert.Equal(("noResult", null, null, "review", 1), (waiting.Reason, waiting.Text, waiting.Detail, waiting.State, waiting.Attempt));
    }
}
