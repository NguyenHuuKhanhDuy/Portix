import { X } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { Tunnel } from '../types'

interface Props {
  tunnels: Tunnel[]
  selectedId: string | null
  onSelect: (id: string) => void
  onClose: (id: string) => void
}

// shadcn's built-in badge variants don't include a semantic "success"/"warning" color, so
// status-specific colors are applied as class overrides on top of the `outline` variant.
const statusClass: Record<Tunnel['status'], string> = {
  Connecting: 'border-amber-500/50 text-amber-600 dark:text-amber-400',
  Online: 'border-emerald-500/50 text-emerald-600 dark:text-emerald-400',
  Error: 'border-destructive/50 text-destructive',
  Closed: 'text-muted-foreground',
}

export function TunnelList({ tunnels, selectedId, onSelect, onClose }: Props) {
  if (tunnels.length === 0) {
    return <p className="px-2 py-4 text-sm text-muted-foreground">No tunnels open yet.</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {tunnels.map((t) => (
        <li key={t.id} className="group flex items-center gap-1">
          <button
            onClick={() => onSelect(t.id)}
            className={cn(
              'flex flex-1 items-center gap-2 overflow-hidden rounded-lg px-2 py-1.5 text-left transition-colors hover:bg-muted',
              t.id === selectedId && 'bg-muted',
            )}
          >
            <Badge variant="outline" className={cn('shrink-0', statusClass[t.status])}>
              {t.status}
            </Badge>
            <span className="shrink-0 font-medium">:{t.localPort}</span>
            <span className="truncate text-sm text-muted-foreground">{t.publicUrl ?? '(no url)'}</span>
          </button>
          <Button
            variant="ghost"
            size="icon-sm"
            title="Close tunnel"
            onClick={() => onClose(t.id)}
            className="shrink-0 opacity-0 group-hover:opacity-100"
          >
            <X className="size-3.5" />
          </Button>
        </li>
      ))}
    </ul>
  )
}
