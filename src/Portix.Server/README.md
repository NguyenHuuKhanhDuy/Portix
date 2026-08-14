# Portix.Server deployment notes

## Database

The server keeps its users, plans, and API tokens in a local SQLite file at `Portix:Database:Path`
(default `portix.db`, next to the executable). Migrations are applied automatically on startup.

**Back this file up.** Losing it means every issued API token stops working and every user/plan
assignment is gone — the same operational weight the old shared `Portix:Token` secret used to carry,
just persisted instead of a single config value.

## Admin API

`/admin/*` (create users, issue/revoke tokens, change plans) requires `Portix:AdminToken` to be set;
if it's left blank, the admin endpoints are not mapped at all. Set it via the `Portix__AdminToken`
environment variable or `Portix:AdminToken` in `appsettings.json`, and call the endpoints with
`Authorization: Bearer <Portix:AdminToken>`. Like `/control`, these routes only listen on the control
port and require HTTP/2 (no TLS/ALPN, so clients must use prior-knowledge h2c — e.g. .NET's
`HttpClient` with `SocketsHttpHandler.Http2UnencryptedSupport` enabled, or any other HTTP/2-capable
client).

Bootstrap your first user:

```
POST /admin/users
{ "name": "alice" }
```

returns `{ "userId": "...", "token": "..." }` — the raw token is shown once and is what that user's
`Portix.Client` should be configured with (`Portix:Token` in the client's `appsettings.json`). Omit
`planId` to default to the seeded `Free` plan (id `1`); pass another plan's id to assign it instead.

Other endpoints: `POST /admin/users/{id}/tokens` (issue an additional token), `POST
/admin/users/{id}/tokens/restore` with `{ "token": "<raw-token>" }` (register a specific raw token
value instead of generating one — for recovering a client that already has a token saved locally
after e.g. a database loss), `DELETE /admin/tokens/{id}` (revoke a token), `PUT
/admin/users/{id}/plan` with `{ "planId": 2 }` (change a user's plan).

Plans themselves are managed the same way: `GET /admin/plans` (list), `POST /admin/plans` with `{
"name": "Pro", "maxConcurrentTunnels": 5 }` (create), `PUT /admin/plans/{id}` with the same body
(rename / change the limit), `DELETE /admin/plans/{id}` (delete — rejected with a 409 if any user is
still assigned to it). A fresh database seeds `Free` (id `1`, 1 concurrent tunnel) and `Pro` (id `2`,
5 concurrent tunnels).

## HTTPS via a reverse proxy

`Portix.Server` itself only speaks plain HTTP — there's no TLS/certificate handling built in. To
serve tunnels at `https://<subdomain>.yourdomain.com` (no port in the URL), put a reverse proxy in
front that owns the domain and terminates TLS, forwarding plain HTTP to `Portix:PublicPort`. With
[Caddy](https://caddyserver.com/), which provisions and renews certificates automatically:

```
*.tunnel.mydomain.com {
    reverse_proxy localhost:8080
}
```

(replace `8080` with your `Portix:PublicPort`, and use a real wildcard DNS record for
`*.tunnel.mydomain.com` pointing at this host). Then set `Portix:PublicUrlScheme` to `https` so the
URLs the server hands back to clients match what the proxy actually serves:

```json
"Portix": {
  "PublicUrlScheme": "https"
}
```

With `Portix:PublicUrlPort` left unset, the advertised URL omits the port entirely (443 is assumed).
If the proxy listens on a non-standard HTTPS port instead, set `Portix:PublicUrlPort` explicitly and
it will be included.

This assumes the reverse proxy runs on the same host as `Portix.Server` (loopback) — the server only
trusts the real client IP from `X-Forwarded-For` when the immediate connection comes from loopback,
to prevent a public caller from spoofing their own captured IP. A proxy on a separate host isn't
supported by this configuration.

## Breaking change: shared `Portix:Token` is gone

Older deployments authenticated every client with one shared `Portix:Token` value. That config key no
longer does anything — every client now needs its own personal API token issued through the admin API
above. To upgrade an existing deployment: configure `Portix:AdminToken`, create a user (and token) for
each client via `POST /admin/users`, and update each client's `Portix:Token` with its personal token.
