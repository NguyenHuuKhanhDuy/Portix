import { useEffect, useState } from 'react'
import { api } from '../api'
import type { CapturedRequestSummary, Tunnel } from '../types'
import { RequestDetailSheet } from './RequestDetailSheet'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'

interface Props {
  tunnel: Tunnel
  latestCapture: CapturedRequestSummary | null
  onCaptureConsumed: () => void
}

export function TrafficView({ tunnel, latestCapture, onCaptureConsumed }: Props) {
  const [requests, setRequests] = useState<CapturedRequestSummary[] | null>(null)
  const [selectedRequestId, setSelectedRequestId] = useState<string | null>(null)

  useEffect(() => {
    setRequests(null)
    setSelectedRequestId(null)
    api.listRequests(tunnel.id).then(setRequests)
  }, [tunnel.id])

  useEffect(() => {
    if (latestCapture && latestCapture.tunnelId === tunnel.id) {
      setRequests((prev) => [latestCapture, ...(prev ?? [])])
      onCaptureConsumed()
    }
  }, [latestCapture, tunnel.id, onCaptureConsumed])

  async function clearLog() {
    await api.clearRequests(tunnel.id)
    setRequests([])
  }

  async function toggleCapture(checked: boolean) {
    await api.setCapture(tunnel.id, checked)
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-4">
        <h3 className="flex-1 truncate text-base font-medium">
          :{tunnel.localPort} — {tunnel.publicUrl}
        </h3>
        <div className="flex items-center gap-2">
          <Switch id="capture-bodies" checked={tunnel.captureBodiesEnabled} onCheckedChange={toggleCapture} />
          <Label htmlFor="capture-bodies" className="text-sm text-muted-foreground">
            Capture bodies
          </Label>
        </div>
        <Button variant="outline" size="sm" onClick={clearLog}>
          Clear
        </Button>
      </div>

      {requests === null ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : requests.length === 0 ? (
        <p className="text-sm text-muted-foreground">No traffic captured yet.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Method</TableHead>
              <TableHead>Path</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Duration</TableHead>
              <TableHead>Time</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {requests.map((r) => (
              <TableRow key={r.id} className="cursor-pointer" onClick={() => setSelectedRequestId(r.id)}>
                <TableCell className="font-medium">{r.method}</TableCell>
                <TableCell className="max-w-xs truncate">{r.path}</TableCell>
                <TableCell className={cn(r.statusCode >= 400 ? 'text-destructive' : 'text-emerald-600 dark:text-emerald-400')}>
                  {r.statusCode}
                </TableCell>
                <TableCell>{r.durationMs.toFixed(0)} ms</TableCell>
                <TableCell>{new Date(r.timestamp).toLocaleTimeString()}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <RequestDetailSheet
        tunnelId={tunnel.id}
        requestId={selectedRequestId}
        onOpenChange={(open) => {
          if (!open) setSelectedRequestId(null)
        }}
      />
    </div>
  )
}
