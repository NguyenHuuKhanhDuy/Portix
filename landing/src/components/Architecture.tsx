import { Globe, Laptop, LayoutDashboard, Radio, Server } from "lucide-react";
import { Fragment } from "react";

const COMPONENTS = [
  {
    icon: Server,
    title: "Server",
    description:
      "The public-facing relay. Accepts client control connections, routes public traffic to the right tunnel by subdomain, and manages users and tokens.",
  },
  {
    icon: LayoutDashboard,
    title: "Client",
    description:
      "The `portix` CLI and local daemon. Registers tunnels with the Server, forwards traffic to your local app, and serves the traffic-inspector dashboard.",
  },
  {
    icon: Radio,
    title: "Shared",
    description:
      "The wire protocol code used by both Server and Client — message framing, types, and header handling.",
  },
];

const FLOW_NODES = [
  { icon: Laptop, label: "Your app" },
  { icon: LayoutDashboard, label: "Client" },
  { icon: Server, label: "Server" },
  { icon: Globe, label: "Visitor" },
];

function FlowDiagram() {
  return (
    <div
      className="relative mx-auto mt-10 flex w-full max-w-2xl items-center"
      role="img"
      aria-label="A request flows from your app through the Client and Server to a public visitor, and the response flows back."
    >
      {/* Positioning rail: sits at the icon circles' vertical center so the single dot travels in a straight line. */}
      <div className="absolute inset-x-0 top-5 sm:top-6">
        <span className="tunnel-dot size-1.5 rounded-full bg-primary sm:size-2" />
      </div>
      {FLOW_NODES.map(({ icon: Icon, label }, index) => (
        <Fragment key={label}>
          <div className="flex flex-col items-center gap-1.5">
            <div className="relative z-10 flex size-10 items-center justify-center rounded-full border border-border bg-card sm:size-12">
              <Icon className="size-4 text-primary sm:size-5" />
            </div>
            <span className="text-[10px] whitespace-nowrap text-muted-foreground sm:text-xs">
              {label}
            </span>
          </div>
          {index < FLOW_NODES.length - 1 && (
            <div className="mt-5 h-px min-w-4 flex-1 self-start bg-border sm:mt-6 sm:h-0.5 sm:min-w-8" />
          )}
        </Fragment>
      ))}
    </div>
  );
}

export default function Architecture() {
  return (
    <section className="mx-auto max-w-6xl px-6 py-16 sm:py-20">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">
          How a tunnel works
        </h2>
        <p className="mt-3 text-muted-foreground">
          The Client opens a long-lived control connection to the Server and
          registers a subdomain. When a public request arrives, the Server
          signals the Client, which opens a data connection back to relay bytes
          in both directions — including WebSocket upgrades.
        </p>
      </div>
      <FlowDiagram />
      <div className="mt-12 grid gap-6 sm:grid-cols-3">
        {COMPONENTS.map(({ icon: Icon, title, description }) => (
          <div
            key={title}
            className="rounded-xl border border-border bg-card p-6"
          >
            <Icon className="size-6 text-primary" />
            <h3 className="mt-4 font-medium">{title}</h3>
            <p className="mt-2 text-sm text-muted-foreground">{description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
