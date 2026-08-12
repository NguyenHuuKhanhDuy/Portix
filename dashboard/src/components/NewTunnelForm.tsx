import { useState } from 'react'
import { api } from '../api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'

interface Props {
  onOpened: () => void
}

export function NewTunnelForm({ onOpened }: Props) {
  const [port, setPort] = useState('')
  const [subdomain, setSubdomain] = useState('')
  const [useHttps, setUseHttps] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    const localPort = Number(port)
    if (!Number.isInteger(localPort) || localPort <= 0) {
      setError('Enter a valid local port.')
      return
    }

    setBusy(true)
    setError(null)
    try {
      await api.openTunnel(localPort, subdomain.trim() || undefined, useHttps ? 'https' : 'http')
      setPort('')
      setSubdomain('')
      onOpened()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <div className="flex flex-col gap-1">
        <Label htmlFor="local-port" className="text-xs text-muted-foreground">
          Local port
        </Label>
        <Input
          id="local-port"
          type="number"
          placeholder="3000"
          value={port}
          onChange={(e) => setPort(e.target.value)}
          disabled={busy}
        />
      </div>
      <div className="flex flex-col gap-1">
        <Label htmlFor="subdomain" className="text-xs text-muted-foreground">
          Subdomain (optional)
        </Label>
        <Input
          id="subdomain"
          type="text"
          placeholder="random"
          value={subdomain}
          onChange={(e) => setSubdomain(e.target.value)}
          disabled={busy}
        />
      </div>
      <div className="flex items-center gap-2">
        <Switch id="use-https" checked={useHttps} onCheckedChange={setUseHttps} disabled={busy} />
        <Label htmlFor="use-https" className="text-xs text-muted-foreground">
          Local app uses HTTPS
        </Label>
      </div>
      <Button type="submit" disabled={busy}>
        {busy ? 'Opening...' : 'New Tunnel'}
      </Button>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </form>
  )
}
