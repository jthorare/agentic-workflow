using Aw.Sindri.Core;
using Vogen;

namespace Aw.Sindri.Host;

/// <summary>
/// Maps Vogen's validation exception (an uninitialized id from an empty route or query value) to a 400 ProblemDetails
/// with SND-CLI-003 and Vogen's message.
/// </summary>
internal static class ValueObjectValidation
{
    internal static WebApplication UseValueObjectValidation(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ValueObjectValidationException e) when (!context.Response.HasStarted)
            {
                await ApiProblems.Write(context, SindriErrors.InvalidInput.Code, StatusCodes.Status400BadRequest, e.Message).ConfigureAwait(false);
            }
        });
        return app;
    }
}
