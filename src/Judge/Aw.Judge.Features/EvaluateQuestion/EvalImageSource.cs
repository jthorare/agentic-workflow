using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>What 'judge eval' needs to bring a stored row's images back: the reader that re-reads the files and the maker of the per-call folders the agent CLIs open them from.</summary>
/// <param name="Reader">Re-reads the files.</param>
/// <param name="Workspaces">Makes the per-call image folders.</param>
internal sealed record EvalImageSource(IImageReader Reader, IImageWorkspaces Workspaces);
