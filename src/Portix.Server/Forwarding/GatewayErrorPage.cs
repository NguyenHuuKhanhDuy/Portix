using System.Net;

namespace Portix.Server.Forwarding;

public static class GatewayErrorPage
{
    private const string Style = """
        :root {
            color-scheme: light;

            --background: #f8fafc;
            --card: #ffffff;
            --text: #0f172a;
            --muted: #64748b;
            --muted-light: #94a3b8;
            --border: #e2e8f0;

            --danger: #dc2626;
            --danger-light: #fef2f2;
            --danger-border: #fecaca;

            --code-background: #f8fafc;
        }

        * {
            box-sizing: border-box;
        }

        html,
        body {
            margin: 0;
            min-height: 100%;
        }

        body {
            font-family:
                Inter,
                -apple-system,
                BlinkMacSystemFont,
                "Segoe UI",
                Helvetica,
                Arial,
                sans-serif;

            background:
                radial-gradient(
                    circle at 50% -10%,
                    rgba(239, 68, 68, 0.06),
                    transparent 40%
                ),
                var(--background);

            color: var(--text);

            display: flex;
            align-items: center;
            justify-content: center;

            min-height: 100vh;
            padding: 24px;
        }

        .container {
            width: 100%;
            max-width: 520px;
        }

        .card {
            background: var(--card);
            border: 1px solid var(--border);
            border-radius: 20px;

            padding: 40px;

            box-shadow:
                0 1px 2px rgba(15, 23, 42, 0.03),
                0 12px 40px rgba(15, 23, 42, 0.06);

            text-align: center;
        }

        .icon-wrapper {
            width: 64px;
            height: 64px;

            margin: 0 auto 24px;

            display: flex;
            align-items: center;
            justify-content: center;

            border-radius: 16px;

            background: var(--danger-light);
            border: 1px solid var(--danger-border);

            color: var(--danger);
        }

        .icon {
            width: 30px;
            height: 30px;
        }

        .badge {
            display: inline-flex;
            align-items: center;
            gap: 7px;

            padding: 6px 10px;

            border-radius: 999px;

            background: var(--danger-light);
            border: 1px solid var(--danger-border);

            color: var(--danger);

            font-size: 11px;
            font-weight: 700;

            letter-spacing: 0.06em;
            text-transform: uppercase;
        }

        .badge-dot {
            width: 6px;
            height: 6px;

            border-radius: 50%;
            background: currentColor;
        }

        h1 {
            margin: 18px 0 10px;

            color: var(--text);

            font-size: 24px;
            line-height: 1.3;
            font-weight: 700;

            letter-spacing: -0.02em;
        }

        .reason {
            margin: 0 auto;

            max-width: 420px;

            color: var(--muted);

            font-size: 15px;
            line-height: 1.65;
        }

        .detail {
            margin: 24px 0 0;
            padding: 14px 16px;

            text-align: left;

            border: 1px solid var(--border);
            border-radius: 12px;

            background: var(--code-background);

            color: #475569;

            font-family:
                ui-monospace,
                SFMono-Regular,
                Menlo,
                Monaco,
                Consolas,
                "Liberation Mono",
                monospace;

            font-size: 12px;
            line-height: 1.6;

            word-break: break-word;
        }

        .footer {
            margin-top: 24px;

            color: var(--muted-light);

            font-size: 12px;
        }

        .brand {
            color: var(--muted);
            font-weight: 600;
        }

        @media (max-width: 480px) {
            body {
                padding: 16px;
            }

            .card {
                padding: 32px 24px;
                border-radius: 16px;
            }

            h1 {
                font-size: 21px;
            }

            .icon-wrapper {
                width: 56px;
                height: 56px;
                border-radius: 14px;
            }

            .icon {
                width: 26px;
                height: 26px;
            }
        }
        """;

    public static string Render(string title, string reason, string? detail)
    {
        var detailBlock = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : $"""
                <p class="detail">{WebUtility.HtmlEncode(detail)}</p>
                """;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta
                    name="viewport"
                    content="width=device-width, initial-scale=1"
                />

                <title>
                    {WebUtility.HtmlEncode(title)} · Portix
                </title>

                <style>
            {Style}
                </style>
            </head>

            <body>
                <main class="container">
                    <section class="card">

                        <div class="icon-wrapper" aria-hidden="true">
                            <svg
                                class="icon"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                stroke-width="1.8"
                                stroke-linecap="round"
                                stroke-linejoin="round"
                            >
                                <path d="M12 3L21 20H3L12 3Z" />
                                <path d="M12 9V13" />
                                <circle cx="12" cy="16.5" r=".8" fill="currentColor" stroke="none" />
                            </svg>
                        </div>

                        <span class="badge">
                            <span class="badge-dot"></span>
                            Local server unreachable
                        </span>

                        <h1>
                            {WebUtility.HtmlEncode(title)}
                        </h1>

                        <p class="reason">
                            {WebUtility.HtmlEncode(reason)}
                        </p>

                        {detailBlock}

                        <div class="footer">
                            Powered by <span class="brand">Portix</span>
                        </div>

                    </section>
                </main>
            </body>
            </html>
            """;
    }
}