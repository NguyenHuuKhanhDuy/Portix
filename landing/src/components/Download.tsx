import { Apple, ChevronDown, Download as DownloadIcon, Laptop, ShieldAlert } from 'lucide-react'
import { DOWNLOAD_OSX_ARM64, DOWNLOAD_OSX_X64, DOWNLOAD_WIN_X64, RELEASES_URL } from '../lib/constants'

export default function Download() {
  return (
    <section id="download" className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">Download Portix</h2>
        <p className="mt-3 text-muted-foreground">Grab the latest build for your platform — extract and run, no install needed.</p>
      </div>
      <div className="mx-auto mt-10 grid max-w-2xl gap-4 sm:grid-cols-2">
        <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-6 text-center">
          <Laptop className="size-8 text-primary" />
          <div>
            <h3 className="font-medium">Windows</h3>
            <p className="text-sm text-muted-foreground">64-bit</p>
          </div>
          <a
            href={DOWNLOAD_WIN_X64}
            className="mt-2 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
          >
            <DownloadIcon className="size-4" />
            Download
          </a>
        </div>
        <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-6 text-center">
          <Apple className="size-8 text-primary" />
          <div>
            <h3 className="font-medium">macOS</h3>
            <p className="text-sm text-muted-foreground">Apple Silicon or Intel</p>
          </div>
          <div className="mt-2 flex gap-2">
            <a
              href={DOWNLOAD_OSX_ARM64}
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
            >
              <DownloadIcon className="size-4" />
              Apple Silicon
            </a>
            <a
              href={DOWNLOAD_OSX_X64}
              className="inline-flex items-center gap-1.5 rounded-lg border border-border px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
            >
              Intel
            </a>
          </div>
        </div>
      </div>
      <details className="group mx-auto mt-8 max-w-2xl rounded-xl border border-border bg-card">
        <summary className="flex cursor-pointer list-none items-center justify-between gap-2 p-4 font-medium marker:content-none">
          <span className="flex items-center gap-2">
            <Apple className="size-4 text-primary" />
            macOS install instructions
          </span>
          <ChevronDown className="size-4 text-muted-foreground transition-transform group-open:rotate-180" />
        </summary>
        <div className="space-y-5 border-t border-border p-6 pt-5 text-sm text-muted-foreground">
          <div>
            <p className="font-medium text-foreground">1. Extract the binary</p>
            <p className="mt-1">Unzip the downloaded file to <code className="rounded bg-muted px-1.5 py-0.5 text-xs">/usr/local/bin</code>:</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">
              sudo unzip ~/Downloads/portix-osx-arm64.zip -d /usr/local/bin/
            </pre>
          </div>
          <div>
            <p className="font-medium text-foreground">2. Set execute permission</p>
            <p className="mt-1">Make the binary executable:</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">sudo chmod 755 /usr/local/bin/portix</pre>
          </div>
          <div>
            <p className="font-medium text-foreground">3. Run Portix</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">portix --help</pre>
          </div>
          <div className="rounded-lg border border-accent/30 bg-accent/10 p-4">
            <p className="flex items-center gap-2 font-medium text-foreground">
              <ShieldAlert className="size-4 text-accent" />
              macOS security notice
            </p>
            <p className="mt-2">
              If you see <em>"portix" was blocked to protect your Mac</em>:
            </p>
            <ol className="mt-2 list-decimal space-y-1 pl-5">
              <li>Go to System Settings → Privacy & Security</li>
              <li>Scroll down to Security</li>
              <li>Click "Open Anyway"</li>
              <li>Confirm to run the app</li>
            </ol>
          </div>
        </div>
      </details>

      <p className="mt-6 text-center text-sm text-muted-foreground">
        See all versions on{' '}
        <a href={RELEASES_URL} className="underline hover:text-foreground">
          GitHub Releases
        </a>
        .
      </p>
    </section>
  )
}
