namespace Aw.Context.Infrastructure.Linear;

/// <summary>The workspace in the Organization operation.</summary>
/// <param name="UrlKey">The UrlKey Linear serves ('acme' in linear.app/acme).</param>
internal sealed record LinearOrganization(string? UrlKey = null);
