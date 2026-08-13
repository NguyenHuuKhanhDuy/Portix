import { Activity, Globe, ListTree, Server, Terminal } from 'lucide-react'

const FEATURES = [
  {
    icon: Server,
    title: 'Self-hosted',
    description: 'Run your own tunnel server on your own domain and infrastructure — no third-party relay in between.',
  },
  {
    icon: Activity,
    title: 'Live traffic inspector',
    description: 'Watch every request hit your tunnel in real time from a local web dashboard, no extra setup required.',
  },
  {
    icon: Terminal,
    title: 'Familiar CLI',
    description: 'Simple `portix http <port>` commands with a live status panel for every open tunnel.',
  },
  {
    icon: Globe,
    title: 'HTTP & HTTPS tunneling',
    description: 'Expose HTTP or HTTPS local apps through your tunnel, including WebSocket upgrades.',
  },
  {
    icon: ListTree,
    title: 'Simple tunnel management',
    description: 'List and close open tunnels at any time with `portix ls` and `portix rm`.',
  },
]

export default function Features() {
  return (
    <section className="mx-auto max-w-6xl px-6 py-16 sm:py-20">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">Everything you need to expose local apps</h2>
        <p className="mt-3 text-muted-foreground">
          A simple CLI workflow for opening tunnels, running entirely on infrastructure you control.
        </p>
      </div>
      <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {FEATURES.map(({ icon: Icon, title, description }) => (
          <div key={title} className="rounded-xl border border-border bg-card p-6">
            <Icon className="size-6 text-primary" />
            <h3 className="mt-4 font-medium">{title}</h3>
            <p className="mt-2 text-sm text-muted-foreground">{description}</p>
          </div>
        ))}
      </div>
    </section>
  )
}
