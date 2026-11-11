using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Toolkits;
using ModelContextProtocol;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>Which SND-TK error an MCP failure on a call becomes (agent-first PR 16): the JSON-RPC code decides, never words in the message.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolkitMcpClientFailureTests
{
    [Fact]
    public void An_unknown_tool_is_SND_TK_007_by_its_code()
    {
        var methodNotFound = ToolkitMcpClient.Failure("ping", new McpProtocolException("no such method", McpErrorCode.MethodNotFound));
        var sdkUnknownTool = ToolkitMcpClient.Failure("ping", new McpProtocolException("Unknown tool: 'ping'", McpErrorCode.InvalidParams));

        Assert.Equal(SindriErrors.ToolkitToolUnknown.Code, methodNotFound.Error.Code);
        Assert.Equal(SindriErrors.ToolkitToolUnknown.Code, sdkUnknownTool.Error.Code);
    }

    [Fact]
    public void A_server_error_that_says_not_found_stays_a_tool_error_and_a_dropped_connection_is_the_process_ending()
    {
        var own = ToolkitMcpClient.Failure("read", new McpProtocolException("file not found: notes.txt", McpErrorCode.InternalError));
        var dropped = ToolkitMcpClient.Failure("read", new McpException("transport closed"));

        Assert.Equal(SindriErrors.ToolkitToolFailed.Code, own.Error.Code);
        Assert.Equal(SindriErrors.ToolkitProcessDied.Code, dropped.Error.Code);
    }
}
