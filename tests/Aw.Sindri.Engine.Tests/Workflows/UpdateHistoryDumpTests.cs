using Aw.Kernel.Testing;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.Update.V1;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What a timed-out Update wait prints for the run's history (#2080): every event on a line, the update ones with the id and what became of it.</summary>
[Lane(TestLane.Unit)]
public sealed class UpdateHistoryDumpTests
{
    [Fact]
    public void Each_event_is_a_line_and_an_update_event_names_its_id_and_outcome()
    {
        var events = new[]
        {
            new HistoryEvent { EventId = 1, EventType = EventType.WorkflowExecutionStarted },
            new HistoryEvent { EventId = 2, EventType = EventType.WorkflowExecutionUpdateAdmitted, WorkflowExecutionUpdateAdmittedEventAttributes = new() { Request = new Request { Meta = new Meta { UpdateId = "u-admitted" } } } },
            new HistoryEvent { EventId = 3, EventType = EventType.WorkflowExecutionUpdateAccepted, WorkflowExecutionUpdateAcceptedEventAttributes = new() { AcceptedRequest = new Request { Meta = new Meta { UpdateId = "u-accepted" } } } },
            new HistoryEvent { EventId = 4, EventType = EventType.WorkflowExecutionUpdateCompleted, WorkflowExecutionUpdateCompletedEventAttributes = new() { Meta = new Meta { UpdateId = "u-done" }, Outcome = new Outcome { Success = new() } } },
            new HistoryEvent { EventId = 5, EventType = EventType.WorkflowExecutionUpdateCompleted, WorkflowExecutionUpdateCompletedEventAttributes = new() { Meta = new Meta { UpdateId = "u-failed" }, Outcome = new Outcome { Failure = new Failure { Message = "no" } } } },
        };

        var lines = UpdateHistoryDump.Describe(events).ToList();

        Assert.Equal(
            [
                "  #1 WorkflowExecutionStarted",
                "  #2 WorkflowExecutionUpdateAdmitted update u-admitted admitted",
                "  #3 WorkflowExecutionUpdateAccepted update u-accepted accepted",
                "  #4 WorkflowExecutionUpdateCompleted update u-done completed",
                "  #5 WorkflowExecutionUpdateCompleted update u-failed failed",
            ],
            lines);
    }
}
