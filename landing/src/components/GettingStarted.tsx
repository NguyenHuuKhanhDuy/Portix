import { ArrowRight } from 'lucide-react'
import { README_URL } from '../lib/constants'

const STEPS = [
  { title: 'Run the Server', description: 'Start Portix.Server on a machine with a public IP or domain.' },
  { title: 'Log in with the Client', description: 'Connect the `portix` CLI to your server using an API token.' },
  { title: 'Open a tunnel', description: 'Run `portix http <port>` and share the public URL it prints.' },
]

export default function GettingStarted() {
  return (
    <section className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <div className="rounded-2xl border border-border bg-card px-6 py-12 text-center sm:px-12">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">Ready to expose your first app?</h2>
        <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
          Follow the full setup guide in the README to run the Server, connect the Client, and open your first
          tunnel.
        </p>
        <ol className="mx-auto mt-8 grid gap-6 text-left sm:grid-cols-3">
          {STEPS.map((step, index) => (
            <li key={step.title}>
              <span className="inline-flex size-8 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground">
                {index + 1}
              </span>
              <p className="mt-3 font-medium">{step.title}</p>
              <p className="mt-1 text-sm text-muted-foreground">{step.description}</p>
            </li>
          ))}
        </ol>
        <a
          href={README_URL}
          className="mt-10 inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-6 py-3 font-medium text-primary-foreground transition-opacity hover:opacity-90"
        >
          Read the setup guide
          <ArrowRight className="size-4" />
        </a>
      </div>
    </section>
  )
}
