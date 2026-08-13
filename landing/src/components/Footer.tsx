import { ExternalLink } from 'lucide-react'
import { GITHUB_URL } from '../lib/constants'

export default function Footer() {
  return (
    <footer className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 border-t border-border px-6 py-8 text-sm text-muted-foreground sm:flex-row">
      <p>Portix — a self-hosted tunnel server.</p>
      <a href={GITHUB_URL} className="inline-flex items-center gap-2 transition-colors hover:text-foreground">
        <ExternalLink className="size-4" />
        GitHub
      </a>
    </footer>
  )
}
