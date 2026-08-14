import { stagger, useAnimate, useReducedMotion } from "motion/react";
import { useEffect } from "react";
import {
  EXAMPLE_LOCAL_PORT,
  EXAMPLE_TUNNEL_DOMAIN,
  EXAMPLE_TUNNEL_SUBDOMAIN,
} from "../lib/constants";
import Reveal from "./Reveal";

const COMMAND = `$ portix http ${EXAMPLE_LOCAL_PORT}`;
const HOLD_MS = 2600;

const OUTPUT_LINES = [
  { label: "Session Status", value: "online" },
  { label: "Web Interface", value: "http://127.0.0.1:4040" },
  {
    label: "Forwarding",
    value: `https://${EXAMPLE_TUNNEL_SUBDOMAIN}.${EXAMPLE_TUNNEL_DOMAIN} -> http://localhost:${EXAMPLE_LOCAL_PORT}`,
  },
];

const REQUEST_ROWS = [
  { time: "14:02:11", method: "GET", path: "/", status: "200 OK" },
  { time: "14:02:12", method: "GET", path: "/api/health", status: "200 OK" },
];

export default function CliDemo() {
  const [scope, animate] = useAnimate();
  const prefersReducedMotion = useReducedMotion();

  useEffect(() => {
    if (prefersReducedMotion) {
      animate(".char", { opacity: 1 }, { duration: 0 });
      animate(".cursor", { opacity: 0 }, { duration: 0 });
      animate(".output-line", { opacity: 1, y: 0 }, { duration: 0 });
      animate(".table-label", { opacity: 1 }, { duration: 0 });
      animate(".request-row", { opacity: 1, y: 0 }, { duration: 0 });
      return;
    }

    let cancelled = false;

    async function loop() {
      while (!cancelled) {
        animate(".char", { opacity: 0 }, { duration: 0 });
        animate(".cursor", { opacity: 0 }, { duration: 0 });
        animate(".output-line", { opacity: 0, y: 4 }, { duration: 0 });
        animate(".table-label", { opacity: 0 }, { duration: 0 });
        animate(".request-row", { opacity: 0, y: 4 }, { duration: 0 });

        await animate(".cursor", { opacity: 1 }, { duration: 0.05 });
        if (cancelled) break;

        await animate(
          ".char",
          { opacity: 1 },
          { duration: 0.05, delay: stagger(0.03) },
        );
        if (cancelled) break;

        await animate(
          ".cursor",
          { opacity: [1, 1, 0] },
          { duration: 0.4, times: [0, 0.6, 1] },
        );
        if (cancelled) break;

        await animate(
          ".output-line",
          { opacity: 1, y: 0 },
          { duration: 0.3, delay: stagger(0.08) },
        );
        if (cancelled) break;

        await animate(".table-label", { opacity: 1 }, { duration: 0.2 });
        if (cancelled) break;

        await animate(
          ".request-row",
          { opacity: 1, y: 0 },
          { duration: 0.25, delay: stagger(0.06) },
        );
        if (cancelled) break;

        await new Promise((resolve) => setTimeout(resolve, HOLD_MS));
      }
    }

    loop();

    return () => {
      cancelled = true;
    };
  }, [animate, prefersReducedMotion]);

  return (
    <section className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <Reveal className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">
          One command, live traffic
        </h2>

        <p className="mt-3 text-muted-foreground">
          Open a tunnel and watch requests come through as they happen.
        </p>
      </Reveal>

      <div
        ref={scope}
        className="mt-10 overflow-x-auto rounded-xl border border-border bg-card p-6 font-mono text-sm"
      >
        <p className="text-primary">
          {COMMAND.split("").map((char, index) => (
            <span key={index} className="char opacity-0">
              {char}
            </span>
          ))}

          <span className="cursor ml-0.5 inline-block h-3.5 w-2 bg-primary align-middle opacity-0" />
        </p>

        <div className="mt-4 space-y-1">
          {OUTPUT_LINES.map(({ label, value }) => (
            <p
              key={label}
              className="output-line whitespace-nowrap text-muted-foreground opacity-0"
            >
              <span className="text-foreground">{label.padEnd(16, " ")}</span>
              {value}
            </p>
          ))}
        </div>

        <p className="table-label mt-4 text-foreground opacity-0">
          HTTP Requests
        </p>

        <div className="mt-2 min-w-[36rem]">
          <div className="request-row grid grid-cols-[100px_100px_minmax(180px,1fr)_100px] gap-6 border-b border-border pb-2 text-muted-foreground opacity-0">
            <span>Time</span>
            <span>Method</span>
            <span>Path</span>
            <span>Status</span>
          </div>

          {REQUEST_ROWS.map((row) => (
            <div
              key={row.time}
              className="request-row grid grid-cols-[100px_100px_minmax(180px,1fr)_100px] gap-6 py-2 text-muted-foreground opacity-0"
            >
              <span>{row.time}</span>
              <span>{row.method}</span>
              <span className="truncate">{row.path}</span>
              <span className="text-accent">{row.status}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
