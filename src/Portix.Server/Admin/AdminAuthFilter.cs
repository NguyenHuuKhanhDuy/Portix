namespace Portix.Server.Admin;

/// <summary>Requires a valid <c>Authorization: Bearer &lt;Portix:AdminToken&gt;</c> header on every /admin/* route.</summary>
public sealed class AdminAuthFilter(AdminTokenAuth adminTokenAuth) : IEndpointFilter
{
    private const string BearerPrefix = "Bearer ";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        var presentedToken = header.StartsWith(BearerPrefix, StringComparison.Ordinal)
            ? header[BearerPrefix.Length..]
            : null;

        if (!adminTokenAuth.IsValid(presentedToken))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        return next(context);
    }
}
