import { motion } from 'motion/react'
import { ArrowRight, ExternalLink } from 'lucide-react'
import { GITHUB_URL } from '../lib/constants'

export default function Hero() {
  return (
    <section className="relative mx-auto flex max-w-5xl flex-col items-center gap-6 overflow-hidden px-6 pt-24 pb-20 text-center sm:pt-32 sm:pb-28">
      <div
        aria-hidden
        className="absolute -top-24 -left-24 -z-10 size-96 rounded-full bg-gradient-to-br from-primary/30 via-accent/20 to-transparent blur-3xl"
      />
      <div
        aria-hidden
        className="absolute -right-24 -bottom-24 -z-10 size-96 rounded-full bg-gradient-to-tl from-accent/20 via-primary/20 to-transparent blur-3xl"
      />
      <div
        aria-hidden
        className="bg-grid absolute inset-0 -z-10 [mask-image:radial-gradient(ellipse_60%_60%_at_50%_30%,black,transparent)]"
      />

      <motion.span
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, ease: 'easeOut' }}
        className="rounded-full border border-border bg-muted px-4 py-1 text-sm text-muted-foreground"
      >
        Self-hosted tunnels
      </motion.span>
      <motion.h1
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, delay: 0.05, ease: 'easeOut' }}
        className="text-4xl font-semibold tracking-tight sm:text-6xl"
      >
        Portix
      </motion.h1>
      <motion.p
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, delay: 0.1, ease: 'easeOut' }}
        className="max-w-2xl text-lg text-muted-foreground sm:text-xl"
      >
        Expose a local port through a public URL served by your own tunnel server, with a live
        traffic inspector built in.
      </motion.p>
      <motion.div
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, delay: 0.15, ease: 'easeOut' }}
        className="flex flex-col gap-3 sm:flex-row"
      >
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
      </motion.div>
    </section>
  )
}
