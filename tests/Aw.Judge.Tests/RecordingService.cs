using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Tests;

/// <summary>A service that records its requests and answers a fixed response: stands in for a stub (or a service another unit builds) in a routing test.</summary>
/// <typeparam name="TRequest">The request.</typeparam>
/// <typeparam name="TResponse">The response.</typeparam>
/// <param name="response">What it answers.</param>
internal sealed class RecordingService<TRequest, TResponse>(TResponse response) : IService<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>The requests it was given, in order.</summary>
    public List<TRequest> Requests { get; } = [];

    public ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Results.Success(response));
    }
}
