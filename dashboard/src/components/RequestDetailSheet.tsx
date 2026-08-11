import { useEffect, useState } from 'react'
import { api } from '../api'
import type { CapturedRequestDetail, ReplayResult } from '../types'
import { JsonViewer } from './JsonViewer'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/sheet'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

interface Props {
  tunnelId: string
  /** null closes the sheet. */
  requestId: string | null
  onOpenChange: (open: boolean) => void
}

function decodeBase64Text(base64: string | null): string {
  if (!base64) return '(empty)'
  try {
    return new TextDecoder('utf-8', { fatal: false }).decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)))
  } catch {
    return '(binary data)'
  }
}

function HeaderList({ headers }: { headers: Record<string, string> }) {
  const entries = Object.entries(headers)
  if (entries.length === 0) {
    return <p className="text-sm text-muted-foreground">No headers.</p>
  }
  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
      {entries.map(([key, value]) => (
        <div key={key} className="contents">
          <dt className="whitespace-nowrap font-mono text-xs text-sky-600 dark:text-sky-400">{key}</dt>
          <dd className="break-all font-mono text-xs">{value}</dd>
        </div>
      ))}
    </dl>
  )
}

function BodySection({ label, base64, truncated }: { label: string; base64: string | null; truncated: boolean }) {
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
        <JsonViewer text={decodeBase64Text(base64)} />
      </div>
    </div>
  )
}

function RawSection({ label, base64 }: { label: string; base64: string | null }) {
  return (
    <div className="flex flex-col gap-1">
      <h4 className="text-sm font-medium">{label}</h4>
      <pre className="max-h-64 overflow-auto rounded-lg border bg-muted/30 p-2 text-xs whitespace-pre-wrap break-all">
        {decodeBase64Text(base64)}
      </pre>
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
              <SheetDescription>
                Status <strong>{detail.statusCode}</strong> · {detail.durationMs.toFixed(1)} ms ·{' '}
                {new Date(detail.timestamp).toLocaleString()}
                {detail.sourceIp && <> · from {detail.sourceIp}</>}
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
                    <HeaderList headers={detail.requestHeaders} />
                  </div>
                  <div>
                    <h4 className="mb-1 text-sm font-medium">Response Headers</h4>
                    <HeaderList headers={detail.responseHeaders} />
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
                  <HeaderList headers={replay.headers} />
                  <div className="rounded-lg border bg-muted/30 p-2">
                    <JsonViewer text={decodeBase64Text(replay.bodyBase64)} />
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
