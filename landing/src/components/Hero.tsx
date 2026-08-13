import { ArrowRight, ExternalLink } from 'lucide-react'
import { GITHUB_URL } from '../lib/constants'

export default function Hero() {
  return (
    <section className="mx-auto flex max-w-5xl flex-col items-center gap-6 px-6 pt-24 pb-20 text-center sm:pt-32 sm:pb-28">
      <span className="rounded-full border border-border bg-muted px-4 py-1 text-sm text-muted-foreground">
        Self-hosted tunnels
      </span>
      <h1 className="text-4xl font-semibold tracking-tight sm:text-6xl">Portix</h1>
      <p className="max-w-2xl text-lg text-muted-foreground sm:text-xl">
        Expose a local port through a public URL served by your own tunnel server, with a live
        traffic inspector built in.
      </p>
      <div className="flex flex-col gap-3 sm:flex-row">
        <a
          href="#download"
          className="inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-6 py-3 font-medium text-primary-foreground transition-opacity hover:opacity-90"
        >
          Download
          <ArrowRight className="size-4" />
        </a>
        <a
          href={GITHUB_URL}
          className="inline-flex items-center justify-center gap-2 rounded-lg border border-border px-6 py-3 font-medium transition-colors hover:bg-muted"
        >
          <ExternalLink className="size-4" />
          View on GitHub
        </a>
      </div>
    </section>
  )
}
