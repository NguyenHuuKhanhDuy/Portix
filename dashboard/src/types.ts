export interface Tunnel {
  id: string
  localPort: number
  subdomain: string | null
  publicUrl: string | null
  status: 'Connecting' | 'Online' | 'Error' | 'Closed'
  lastError: string | null
  captureBodiesEnabled: boolean
}

export interface CapturedRequestSummary {
  id: string
  tunnelId: string
  method: string
  path: string
  statusCode: number
  durationMs: number
  timestamp: string
  sourceIp: string | null
}

export interface CapturedRequestDetail {
  id: string
  tunnelId: string
  method: string
  path: string
  requestHeaders: Record<string, string>
  requestBodyPreviewBase64: string | null
  requestBodyTruncated: boolean
  statusCode: number
  responseHeaders: Record<string, string>
  responseBodyPreviewBase64: string | null
  responseBodyTruncated: boolean
  durationMs: number
  timestamp: string
  sourceIp: string | null
}

export interface ReplayResult {
  statusCode: number
  headers: Record<string, string>
  bodyBase64: string
}
