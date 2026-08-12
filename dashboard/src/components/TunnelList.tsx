import { Copy, ExternalLink, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { copyToClipboard } from '@/lib/clipboard'
import { cn } from '@/lib/utils'
import type { Tunnel } from '../types'

interface Props {
  tunnels: Tunnel[]
  selectedId: string | null
  onSelect: (id: string) => void
  onClose: (id: string) => void
}

const statusDotClass: Record<Tunnel['status'], string> = {
  Connecting: 'bg-amber-500',
  Online: 'bg-emerald-500',
  Error: 'bg-destructive',
  Closed: 'bg-muted-foreground',
}

const statusTextClass: Record<Tunnel['status'], string> = {
  Connecting: 'text-amber-600 dark:text-amber-400',
  Online: 'text-emerald-600 dark:text-emerald-400',
  Error: 'text-destructive',
  Closed: 'text-muted-foreground',
}

export function TunnelList({ tunnels, selectedId, onSelect, onClose }: Props) {
  if (tunnels.length === 0) {
    return <p className="px-2 py-4 text-sm text-muted-foreground">No tunnels open yet.</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {tunnels.map((t) => (
        <li
          key={t.id}
          className={cn(
            'flex flex-col gap-1.5 rounded-lg p-2 transition-colors',
            t.id === selectedId ? 'bg-muted' : 'hover:bg-muted/60',
          )}
        >
          <button onClick={() => onSelect(t.id)} className="flex flex-col gap-1 text-left">
            <span className="flex items-center gap-1.5">
              <span className={cn('inline-block size-1.5 rounded-full', statusDotClass[t.status])} />
              <span className={cn('text-xs font-medium', statusTextClass[t.status])}>{t.status}</span>
            </span>
            <span className="text-sm font-medium">
              {t.scheme}://localhost:{t.localPort}
            </span>
            <span className="truncate text-xs text-muted-foreground">{t.publicUrl ?? 'No public URL yet'}</span>
          </button>
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon-xs"
              title="Copy URL"
              disabled={!t.publicUrl}
              onClick={() => t.publicUrl && copyToClipboard(t.publicUrl)}
            >
              <Copy />
            </Button>
            <Button
              variant="ghost"
              size="icon-xs"
              title="Open URL"
              disabled={!t.publicUrl}
              onClick={() => t.publicUrl && window.open(t.publicUrl, '_blank', 'noopener,noreferrer')}
            >
              <ExternalLink />
            </Button>
            <Button
              variant="ghost"
              size="icon-xs"
              title="Disconnect tunnel"
              className="ml-auto text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={() => onClose(t.id)}
            >
              <X />
            </Button>
          </div>
        </li>
      ))}
    </ul>
  )
}
