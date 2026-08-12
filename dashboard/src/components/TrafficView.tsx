import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { CapturedRequestSummary, Tunnel } from '../types'
import { RequestDetailSheet } from './RequestDetailSheet'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { durationTone, durationToneTextClass, statusTone, statusToneBadgeClass } from '@/lib/tone'

const METHOD_OPTIONS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const
const STATUS_CLASS_OPTIONS = ['2xx', '3xx', '4xx', '5xx'] as const
type StatusClassFilter = (typeof STATUS_CLASS_OPTIONS)[number]

// Every captured request retained by RequestStore (Portix.Client/Inspector/RequestStore.cs) is
// fetched up front so search/filter can run entirely client-side over a small, bounded array.
const RETENTION_LIMIT = 200

function matchesStatusClass(statusCode: number, statusClass: StatusClassFilter): boolean {
  const base = Number(statusClass[0]) * 100
  return statusCode >= base && statusCode < base + 100
}

interface Props {
  tunnel: Tunnel
  latestCapture: CapturedRequestSummary | null
  onCaptureConsumed: () => void
}

export function TrafficView({ tunnel, latestCapture, onCaptureConsumed }: Props) {
  const [requests, setRequests] = useState<CapturedRequestSummary[] | null>(null)
  const [selectedRequestId, setSelectedRequestId] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [methodFilter, setMethodFilter] = useState('ALL')
  const [statusFilter, setStatusFilter] = useState('ALL')

  useEffect(() => {
    setRequests(null)
    setSelectedRequestId(null)
    setSearch('')
    setMethodFilter('ALL')
    setStatusFilter('ALL')
    api.listRequests(tunnel.id, RETENTION_LIMIT).then(setRequests)
  }, [tunnel.id])

  useEffect(() => {
    if (latestCapture && latestCapture.tunnelId === tunnel.id) {
      setRequests((prev) => [latestCapture, ...(prev ?? [])])
      onCaptureConsumed()
    }
  }, [latestCapture, tunnel.id, onCaptureConsumed])

  const filtered = useMemo(() => {
    if (!requests) return []
    const query = search.trim().toLowerCase()
    return requests.filter((r) => {
      if (query && !r.path.toLowerCase().includes(query)) return false
      if (methodFilter !== 'ALL' && r.method !== methodFilter) return false
      if (statusFilter !== 'ALL' && !matchesStatusClass(r.statusCode, statusFilter as StatusClassFilter)) return false
      return true
    })
  }, [requests, search, methodFilter, statusFilter])

  const hasAnyRequests = (requests?.length ?? 0) > 0

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
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="outline" size="sm" disabled={!hasAnyRequests}>
              Clear
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Clear all captured requests?</AlertDialogTitle>
              <AlertDialogDescription>
                This deletes every request captured for this tunnel. This cannot be undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={clearLog}>Clear</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>

      {hasAnyRequests && (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            placeholder="Search path..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="max-w-56"
          />
          <Select value={methodFilter} onValueChange={setMethodFilter}>
            <SelectTrigger size="sm">
              <SelectValue placeholder="All Methods" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Methods</SelectItem>
              {METHOD_OPTIONS.map((m) => (
                <SelectItem key={m} value={m}>
                  {m}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger size="sm">
              <SelectValue placeholder="All Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Status</SelectItem>
              {STATUS_CLASS_OPTIONS.map((s) => (
                <SelectItem key={s} value={s}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <span className="ml-auto text-sm text-muted-foreground">Requests ({filtered.length})</span>
        </div>
      )}

      {requests === null ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : !hasAnyRequests ? (
        <div className="flex flex-col items-center gap-1 rounded-lg border border-dashed py-10 text-center">
          <p className="text-sm font-medium">No requests yet</p>
          <p className="text-sm text-muted-foreground">Waiting for incoming traffic...</p>
          {tunnel.publicUrl && (
            <p className="mt-1 text-sm text-muted-foreground">
              Send a request to <span className="font-mono text-foreground">{tunnel.publicUrl}</span>
            </p>
          )}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center gap-1 rounded-lg border border-dashed py-10 text-center">
          <p className="text-sm font-medium">No requests match your filters</p>
          <p className="text-sm text-muted-foreground">Try clearing the search or filters above.</p>
        </div>
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
            {filtered.map((r) => (
              <TableRow
                key={r.id}
                className="cursor-pointer"
                data-state={r.id === selectedRequestId ? 'selected' : undefined}
                onClick={() => setSelectedRequestId(r.id)}
              >
                <TableCell className="font-medium">{r.method}</TableCell>
                <TableCell className="max-w-xs truncate">{r.path}</TableCell>
                <TableCell>
                  <Badge variant="outline" className={statusToneBadgeClass[statusTone(r.statusCode)]}>
                    {r.statusCode}
                  </Badge>
                </TableCell>
                <TableCell className={durationToneTextClass[durationTone(r.durationMs)]}>
                  {r.durationMs.toFixed(0)} ms
                </TableCell>
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
