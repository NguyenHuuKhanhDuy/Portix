using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Portix.Server.Admin;
using Portix.Server.Auth;
using Portix.Server.Data;
using Portix.Server.Forwarding;
using Portix.Server.Sessions;

var builder = WebApplication.CreateBuilder(args);

var controlPort = builder.Configuration.GetValue("Portix:ControlPort", 5100);
var publicPort = builder.Configuration.GetValue("Portix:PublicPort", 8080);
var adminPort = builder.Configuration.GetValue("Portix:AdminPort", 5101);
var databasePath = builder.Configuration["Portix:Database:Path"] ?? "portix.db";
var adminToken = builder.Configuration["Portix:AdminToken"];
var adminEnabled = !string.IsNullOrWhiteSpace(adminToken);

builder.WebHost.ConfigureKestrel(options =>
{
    // Control/data channel: client dials HTTP/2 with prior knowledge (no TLS, no ALPN), so only Http2 is offered here.
    // NOTE: an earlier attempt (add-swagger-docs) widened this to Http1AndHttp2 so a browser could
    // reach /admin and Swagger UI here too — that broke the control channel outright (Kestrel can't
    // multiplex HTTP/1.1 and h2c on one cleartext port). /admin and Swagger UI now get their own
    // dedicated listener (adminPort) below instead, so this port stays Http2-only.
    options.Listen(System.Net.IPAddress.Any, controlPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);

    // Public listener: ordinary browsers/curl speak HTTP/1.1; also accept HTTP/2 for completeness.
    options.Listen(System.Net.IPAddress.Any, publicPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http1AndHttp2);

    // Admin listener: dedicated port for /admin/* and Swagger UI, both of which need plain HTTP/1.1
    // (browsers, curl, Postman can't speak the control port's h2c prior-knowledge protocol). Only
    // opened when the admin API itself is enabled.
    if (adminEnabled)
    {
        options.Listen(System.Net.IPAddress.Any, adminPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http1AndHttp2);
    }
});

builder.Services.AddDbContext<PortixDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddSingleton<StreamBroker>();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ControlEndpoint>();
builder.Services.AddSingleton<DataEndpoint>();
builder.Services.AddSingleton<PublicForwardingMiddleware>();
builder.Services.AddHostedService<HeartbeatSweepService>();

if (adminEnabled)
{
    builder.Services.AddSingleton(new AdminTokenAuth(adminToken!));
    builder.Services.AddSingleton<AdminAuthFilter>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Portix Admin API", Version = "v1" });

        const string bearerScheme = "Bearer";
        options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Portix admin token (Portix:AdminToken).",
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = bearerScheme } }] = [],
        });
    });
}

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    startupScope.ServiceProvider.GetRequiredService<PortixDbContext>().Database.Migrate();
}

// Trusted only from loopback (the default KnownProxies/KnownNetworks) — i.e. only honored when a
// reverse proxy runs on this same host, which is the deployment this header support exists for.
// An unconfigured/untrusted X-Forwarded-For would otherwise let a public caller spoof their own
// captured source IP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
});

// Requests arriving on the public port are always tunnel traffic for some subdomain,
// regardless of path, so they're routed before the control/data endpoints below.
app.Use(async (context, next) =>
{
    if (context.Connection.LocalPort == publicPort)
    {
        var forwarder = context.RequestServices.GetRequiredService<PublicForwardingMiddleware>();
        await forwarder.HandleAsync(context);
        return;
    }

    // /admin and Swagger UI are only served on the dedicated admin listener (see design.md for
    // admin-dedicated-port) — endpoint routing itself isn't port-aware, so this guard keeps them
    // out of reach on the control port even though they're mapped there too.
    if (context.Connection.LocalPort != adminPort
        && (context.Request.Path.StartsWithSegments("/admin") || context.Request.Path.StartsWithSegments("/swagger")))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next().ConfigureAwait(false);
});

app.MapPost("/control", (HttpContext context, ControlEndpoint endpoint, AuthService authService) => endpoint.HandleAsync(context, authService))
    .ExcludeFromDescription();
app.MapPost("/data/{streamId}", (HttpContext context, DataEndpoint endpoint, string streamId) => endpoint.HandleAsync(context, streamId))
    .ExcludeFromDescription();

if (adminEnabled)
{
    app.MapAdminEndpoints();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.Logger.LogWarning("Portix:AdminToken is not configured; admin endpoints are disabled.");
}

if (adminEnabled)
{
    app.Logger.LogInformation("Portix.Server starting: control port {ControlPort}, public port {PublicPort}, admin port {AdminPort}", controlPort, publicPort, adminPort);
}
else
{
    app.Logger.LogInformation("Portix.Server starting: control port {ControlPort}, public port {PublicPort}", controlPort, publicPort);
}

app.Run();
