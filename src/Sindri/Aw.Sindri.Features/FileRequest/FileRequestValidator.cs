using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>The shape of a request: a known kind and 1 to 65,536 characters of text (SND-REQ-001).</summary>
internal sealed class FileRequestValidator : IValidator<FileRequestRequest>
{
    public Result<Unit> Validate(FileRequestRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RequestRules.Validate(value.Kind, value.Text);
    }
}
