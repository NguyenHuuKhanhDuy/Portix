import type { CapturedRequestDetail, CapturedRequestSummary, ReplayResult, Tunnel } from './types'

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const text = await response.text().catch(() => '')
    throw new Error(text || `Request failed with status ${response.status}`)
  }
  return (await response.json()) as T
}

export const api = {
  listTunnels: (): Promise<Tunnel[]> => fetch('/api/tunnels').then((r) => json<Tunnel[]>(r)),

  openTunnel: (localPort: number, subdomain?: string): Promise<Tunnel> =>
    fetch('/api/tunnels', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ localPort, subdomain: subdomain || null }),
    }).then((r) => json<Tunnel>(r)),

  closeTunnel: (id: string): Promise<void> =>
    fetch(`/api/tunnels/${id}`, { method: 'DELETE' }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`Failed to close tunnel (${r.status})`)
    }),

  setCapture: (id: string, enabled: boolean): Promise<Tunnel> =>
    fetch(`/api/tunnels/${id}/capture?enabled=${enabled}`, { method: 'POST' }).then((r) => json<Tunnel>(r)),

  listRequests: (tunnelId: string): Promise<CapturedRequestSummary[]> =>
    fetch(`/api/tunnels/${tunnelId}/requests`).then((r) => json<CapturedRequestSummary[]>(r)),

  getRequest: (tunnelId: string, requestId: string): Promise<CapturedRequestDetail> =>
    fetch(`/api/tunnels/${tunnelId}/requests/${requestId}`).then((r) => json<CapturedRequestDetail>(r)),

  clearRequests: (tunnelId: string): Promise<void> =>
    fetch(`/api/tunnels/${tunnelId}/requests`, { method: 'DELETE' }).then((r) => {
      if (!r.ok) throw new Error(`Failed to clear requests (${r.status})`)
    }),

  replay: (tunnelId: string, requestId: string): Promise<ReplayResult> =>
    fetch(`/api/tunnels/${tunnelId}/requests/${requestId}/replay`, { method: 'POST' }).then((r) => json<ReplayResult>(r)),
}
