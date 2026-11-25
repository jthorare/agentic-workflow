using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ResumeToolkit;

/// <summary>Resumes a running toolkit: its tools are listed and callable again.</summary>
/// <param name="Name">The toolkit name.</param>
internal sealed record ResumeToolkitRequest(string Name) : ICommand<ResumeToolkitResponse>;
