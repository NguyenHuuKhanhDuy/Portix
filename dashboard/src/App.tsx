import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { NewTunnelForm } from './components/NewTunnelForm'
import { TrafficView } from './components/TrafficView'
import { TunnelList } from './components/TunnelList'
import { connectDashboardHub } from './signalr'
import type { CapturedRequestSummary, Tunnel } from './types'

function App() {
  const [tunnels, setTunnels] = useState<Tunnel[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [latestCapture, setLatestCapture] = useState<CapturedRequestSummary | null>(null)
  const tunnelsRef = useRef<Tunnel[]>([])
  tunnelsRef.current = tunnels

  const refresh = useCallback(() => {
    api.listTunnels().then(setTunnels).catch(console.error)
  }, [])

  useEffect(() => {
    refresh()

    const connection = connectDashboardHub({
      onTunnelStatusChanged: (tunnel) => {
        setTunnels((prev) => {
          if (tunnel.status === 'Closed') {
            return prev.filter((t) => t.id !== tunnel.id)
          }
          const existing = prev.findIndex((t) => t.id === tunnel.id)
          if (existing === -1) return [...prev, tunnel]
          const copy = [...prev]
          copy[existing] = tunnel
          return copy
        })
      },
      onRequestCaptured: (request) => setLatestCapture(request),
    })

    return () => {
      connection.stop()
    }
  }, [refresh])

  const selectedTunnel = tunnels.find((t) => t.id === selectedId) ?? null

  async function closeTunnel(id: string) {
    await api.closeTunnel(id)
    setTunnels((prev) => prev.filter((t) => t.id !== id))
    if (selectedId === id) setSelectedId(null)
  }

  return (
    <div className="flex h-screen bg-background text-foreground">
      <aside className="flex w-80 flex-col gap-4 overflow-y-auto border-r p-4">
        <h1 className="text-lg font-semibold">Portix</h1>
        <NewTunnelForm onOpened={refresh} />
        <TunnelList tunnels={tunnels} selectedId={selectedId} onSelect={setSelectedId} onClose={closeTunnel} />
      </aside>
      <main className="flex-1 overflow-y-auto p-6">
        {selectedTunnel ? (
          <TrafficView
            tunnel={selectedTunnel}
            latestCapture={latestCapture}
            onCaptureConsumed={() => setLatestCapture(null)}
          />
        ) : (
          <p className="text-sm text-muted-foreground">Select a tunnel to view its traffic.</p>
        )}
      </main>
    </div>
  )
}

export default App
