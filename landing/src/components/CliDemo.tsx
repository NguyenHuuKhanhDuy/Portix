const OUTPUT_LINES = [
  { label: 'Session Status', value: 'online' },
  { label: 'Web Interface', value: 'http://127.0.0.1:4040' },
  { label: 'Forwarding', value: 'https://a1b2c3d4.tunnel.example.com -> http://localhost:3000' },
]

const REQUEST_ROWS = [
  { time: '14:02:11', method: 'GET', path: '/', status: '200 OK' },
  { time: '14:02:12', method: 'GET', path: '/api/health', status: '200 OK' },
]

export default function CliDemo() {
  return (
    <section className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">One command, live traffic</h2>
        <p className="mt-3 text-muted-foreground">Open a tunnel and watch requests come through as they happen.</p>
      </div>
      <div className="mt-10 overflow-x-auto rounded-xl border border-border bg-card p-6 font-mono text-sm">
        <p className="text-primary">$ portix http 3000</p>
        <div className="mt-4 space-y-1">
          {OUTPUT_LINES.map(({ label, value }) => (
            <p key={label} className="whitespace-nowrap text-muted-foreground">
              <span className="text-foreground">{label.padEnd(16, ' ')}</span>
              {value}
            </p>
          ))}
        </div>
        <p className="mt-4 text-foreground">HTTP Requests</p>
        <div className="mt-2 min-w-[26rem]">
          <div className="grid grid-cols-4 gap-4 border-b border-border pb-2 text-muted-foreground">
            <span>Time</span>
            <span>Method</span>
            <span>Path</span>
            <span>Status</span>
          </div>
          {REQUEST_ROWS.map((row) => (
            <div key={row.time} className="grid grid-cols-4 gap-4 py-2 text-muted-foreground">
              <span>{row.time}</span>
              <span>{row.method}</span>
              <span>{row.path}</span>
              <span className="text-accent">{row.status}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
