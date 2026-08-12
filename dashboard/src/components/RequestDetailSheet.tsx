import { useEffect, useState } from 'react'
import { Copy, Eye, EyeOff } from 'lucide-react'
import { api } from '../api'
import type { CapturedRequestDetail, ReplayResult } from '../types'
import { JsonViewer } from './JsonViewer'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/sheet'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { copyToClipboard } from '@/lib/clipboard'
import { durationTone, durationToneTextClass, statusTone, statusToneBadgeClass } from '@/lib/tone'
import { cn } from '@/lib/utils'

interface Props {
  tunnelId: string
  /** null closes the sheet. */
  requestId: string | null
  onOpenChange: (open: boolean) => void
}

// Case-insensitive: exact names known to carry credentials, plus any header ending in a
// key/token/secret-shaped suffix (e.g. X-Api-Key).
const SENSITIVE_HEADER_PATTERN = /^(authorization|cookie|set-cookie)$|(-key|-token|-secret)$/i

function decodeBase64Text(base64: string | null): string | null {
  if (!base64) return null
  try {
    return new TextDecoder('utf-8', { fatal: false }).decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)))
  } catch {
    return '(binary data)'
  }
}

function HeaderTable({ headers }: { headers: Record<string, string> }) {
  const entries = Object.entries(headers)
  const [revealed, setRevealed] = useState<Set<string>>(new Set())

  if (entries.length === 0) {
    return <p className="text-sm text-muted-foreground">No headers.</p>
  }

  function toggleReveal(key: string) {
    setRevealed((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function copyAll() {
    copyToClipboard(entries.map(([key, value]) => `${key}: ${value}`).join('\n'))
  }

  return (
    <div className="flex flex-col gap-1">
      <div className="flex justify-end">
        <Button variant="ghost" size="xs" onClick={copyAll}>
          <Copy /> Copy all
        </Button>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Key</TableHead>
            <TableHead>Value</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.map(([key, value]) => {
            const sensitive = SENSITIVE_HEADER_PATTERN.test(key)
            const masked = sensitive && !revealed.has(key)
            return (
              <TableRow key={key}>
                <TableCell className="whitespace-nowrap font-mono text-xs text-sky-600 dark:text-sky-400">
                  {key}
                </TableCell>
                <TableCell className="whitespace-normal font-mono text-xs break-all">
                  <span className="inline-flex items-center gap-1.5">
                    {masked ? '••••••••' : value}
                    {sensitive && (
                      <button
                        type="button"
                        title={masked ? 'Reveal value' : 'Hide value'}
                        onClick={() => toggleReveal(key)}
                        className="text-muted-foreground hover:text-foreground"
                      >
                        {masked ? <Eye className="size-3" /> : <EyeOff className="size-3" />}
                      </button>
                    )}
                  </span>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}

function BodySection({ label, base64, truncated }: { label: string; base64: string | null; truncated: boolean }) {
  const text = decodeBase64Text(base64)
  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <h4 className="text-sm font-medium">{label}</h4>
        {truncated && (
          <Badge variant="outline" className="border-amber-500/50 text-amber-600 dark:text-amber-400">
            truncated
          </Badge>
        )}
      </div>
      <div className="rounded-lg border bg-muted/30 p-2">
        {text === null ? <p className="text-sm text-muted-foreground">No {label.toLowerCase()}</p> : <JsonViewer text={text} />}
      </div>
    </div>
  )
}

function RawSection({ label, base64 }: { label: string; base64: string | null }) {
  const [wrap, setWrap] = useState(true)
  const text = decodeBase64Text(base64)

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-medium">{label}</h4>
        <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
          Wrap
          <Switch checked={wrap} onCheckedChange={setWrap} />
        </label>
      </div>
      {text === null ? (
        <p className="text-sm text-muted-foreground">No {label.toLowerCase()}</p>
      ) : (
        <pre
          className={cn(
            'max-h-64 rounded-lg border bg-muted/30 p-2 text-xs',
            wrap ? 'overflow-auto whitespace-pre-wrap break-all' : 'overflow-auto whitespace-pre',
          )}
        >
          {text}
        </pre>
      )}
    </div>
  )
}

export function RequestDetailSheet({ tunnelId, requestId, onOpenChange }: Props) {
  const [detail, setDetail] = useState<CapturedRequestDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [replay, setReplay] = useState<ReplayResult | null>(null)
  const [replaying, setReplaying] = useState(false)

  useEffect(() => {
    if (!requestId) return
    setDetail(null)
    setReplay(null)
    setError(null)
    api.getRequest(tunnelId, requestId).then(setDetail).catch((err) => setError(String(err)))
  }, [tunnelId, requestId])

  async function doReplay() {
    if (!requestId) return
    setReplaying(true)
    setError(null)
    try {
      setReplay(await api.replay(tunnelId, requestId))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setReplaying(false)
    }
  }

  return (
    <Sheet open={requestId !== null} onOpenChange={onOpenChange}>
      <SheetContent className="w-full gap-0 overflow-y-auto sm:max-w-4xl!">
        {error ? (
          <p className="p-4 text-sm text-destructive">{error}</p>
        ) : !detail ? (
          <p className="p-4 text-sm text-muted-foreground">Loading...</p>
        ) : (
          <>
            <SheetHeader>
              <SheetTitle>
                {detail.method} {detail.path}
              </SheetTitle>
              <SheetDescription className="flex flex-wrap items-center gap-1.5">
                <Badge variant="outline" className={statusToneBadgeClass[statusTone(detail.statusCode)]}>
                  {detail.statusCode}
                </Badge>
                <span>·</span>
                <span className={durationToneTextClass[durationTone(detail.durationMs)]}>
                  {detail.durationMs.toFixed(1)} ms
                </span>
                <span>· {new Date(detail.timestamp).toLocaleString()}</span>
                {detail.sourceIp && <span>· {detail.sourceIp}</span>}
              </SheetDescription>
            </SheetHeader>

            <div className="flex flex-col gap-4 px-4 pb-4">
              <Button onClick={doReplay} disabled={replaying} className="self-start">
                {replaying ? 'Replaying...' : 'Replay'}
              </Button>

              <Tabs defaultValue="body">
                <TabsList>
                  <TabsTrigger value="headers">Headers</TabsTrigger>
                  <TabsTrigger value="body">Body</TabsTrigger>
                  <TabsTrigger value="raw">Raw</TabsTrigger>
                </TabsList>

                <TabsContent value="headers" className="flex flex-col gap-4">
                  <div>
                    <h4 className="mb-1 text-sm font-medium">Request Headers</h4>
                    <HeaderTable headers={detail.requestHeaders} />
                  </div>
                  <div>
                    <h4 className="mb-1 text-sm font-medium">Response Headers</h4>
                    <HeaderTable headers={detail.responseHeaders} />
                  </div>
                </TabsContent>

                <TabsContent value="body" className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                  <BodySection
                    label="Request Body"
                    base64={detail.requestBodyPreviewBase64}
                    truncated={detail.requestBodyTruncated}
                  />
                  <BodySection
                    label="Response Body"
                    base64={detail.responseBodyPreviewBase64}
                    truncated={detail.responseBodyTruncated}
                  />
                </TabsContent>

                <TabsContent value="raw" className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                  <RawSection label="Request Body" base64={detail.requestBodyPreviewBase64} />
                  <RawSection label="Response Body" base64={detail.responseBodyPreviewBase64} />
                </TabsContent>
              </Tabs>

              {replay && (
                <div className="flex flex-col gap-1 rounded-lg border p-3">
                  <h4 className="text-sm font-medium">Replay Result</h4>
                  <p className="text-sm text-muted-foreground">Status {replay.statusCode}</p>
                  <HeaderTable headers={replay.headers} />
                  <div className="rounded-lg border bg-muted/30 p-2">
                    <JsonViewer text={decodeBase64Text(replay.bodyBase64) ?? 'No response body'} />
                  </div>
                </div>
              )}
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  )
}
