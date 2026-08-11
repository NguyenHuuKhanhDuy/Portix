import * as signalR from '@microsoft/signalr'
import type { CapturedRequestSummary, Tunnel } from './types'

export function connectDashboardHub(handlers: {
  onTunnelStatusChanged: (tunnel: Tunnel) => void
  onRequestCaptured: (request: CapturedRequestSummary) => void
}): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/dashboard')
    .withAutomaticReconnect()
    .build()

  connection.on('TunnelStatusChanged', handlers.onTunnelStatusChanged)
  connection.on('RequestCaptured', handlers.onRequestCaptured)

  connection.start().catch((err) => console.error('SignalR connection failed:', err))

  return connection
}
