import { ArrowRight, Terminal } from 'lucide-react'
import { EXAMPLE_API_TOKEN, EXAMPLE_LOCAL_PORT, EXAMPLE_SERVER_URL, README_URL } from '../lib/constants'
import Collapsible from './Collapsible'
import Reveal from './Reveal'

const STEPS = [
  {
    title: 'Open a terminal',
    description: 'Go to the folder where you extracted the download above.',
    command: 'cd path/to/portix',
  },
  {
    title: 'Log in',
    description: 'Use the API token and server address your admin gave you.',
    command: `portix login <${EXAMPLE_API_TOKEN}> --server ${EXAMPLE_SERVER_URL}`,
  },
  {
    title: 'Open a tunnel',
    description: 'Expose a local port and share the public URL it prints.',
    command: `portix http ${EXAMPLE_LOCAL_PORT}`,
  },
]

const CLI_COMMANDS = [
  { command: 'portix http <port> [--subdomain <name>]', description: 'Expose a local HTTP port through a public tunnel.' },
  { command: 'portix https <port> [--subdomain <name>]', description: 'Expose a local HTTPS port through a public tunnel.' },
  { command: 'portix ls', description: 'List currently open tunnels.' },
  { command: 'portix rm <id>', description: 'Close a tunnel by id.' },
  { command: 'portix login <token> [--server <url>]', description: 'Save a personal API token for this machine.' },
  { command: 'portix logout', description: 'Remove the saved API token.' },
  { command: 'portix --help', description: 'Show usage.' },
]

export default function GettingStarted() {
  return (
    <section className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <Reveal className="rounded-2xl border border-border bg-card px-6 py-12 text-center sm:px-12">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">Ready to expose your first app?</h2>
        <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
          Three steps to get a public URL pointing at something running on your machine.
        </p>
        <ol className="mx-auto mt-8 grid gap-6 text-left sm:grid-cols-3">
          {STEPS.map((step, index) => (
            <li key={step.title}>
              <span className="inline-flex size-8 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground">
                {index + 1}
              </span>
              <p className="mt-3 font-medium">{step.title}</p>
              <p className="mt-1 text-sm text-muted-foreground">{step.description}</p>
              <pre className="mt-3 overflow-x-auto whitespace-pre-wrap rounded-lg bg-muted p-3 text-left font-mono text-xs text-foreground">
                {step.command}
              </pre>
            </li>
          ))}
        </ol>

        <Collapsible
          className="mx-auto mt-10 max-w-2xl bg-background text-left"
          summary={
            <span className="flex items-center gap-2">
              <Terminal className="size-4 text-primary" />
              CLI reference
            </span>
          }
        >
          <div className="space-y-3 border-t border-border p-6 pt-5 text-sm">
            {CLI_COMMANDS.map((item) => (
              <div key={item.command} className="grid gap-1 sm:grid-cols-[1fr_1.2fr] sm:gap-4">
                <code className="overflow-x-auto rounded bg-muted px-2 py-1 font-mono text-xs text-foreground">
                  {item.command}
                </code>
                <p className="text-muted-foreground">{item.description}</p>
              </div>
            ))}
          </div>
        </Collapsible>

        <a
          href={README_URL}
          className="mt-10 inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-6 py-3 font-medium text-primary-foreground transition-opacity hover:opacity-90"
        >
          Read the full setup guide
          <ArrowRight className="size-4" />
        </a>
      </Reveal>
    </section>
  )
}
