using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The AwJson options for the files the launcher writes for a session: the plan file and control.json. Same wire as the host's context, which reads them.</summary>
internal static class SandboxFilesJson
{
    internal static readonly JsonSerializerOptions Options = AwJson.CreateOptions(SandboxFilesJsonContext.Default);
}
