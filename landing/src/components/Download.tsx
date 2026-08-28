import { motion } from 'motion/react'
import { Apple, Download as DownloadIcon, Laptop, ShieldAlert } from 'lucide-react'
import { DOWNLOAD_OSX_ARM64, DOWNLOAD_OSX_X64, DOWNLOAD_WIN_X64, RELEASES_URL } from '../lib/constants'
import Collapsible from './Collapsible'
import Reveal from './Reveal'
import { useLocale } from '../lib/i18n/LocaleContext'

export default function Download() {
  const { t } = useLocale()
  const { windowsInstructions: win, macInstructions: mac } = t.download

  return (
    <section id="download" className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <Reveal className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">{t.download.heading}</h2>
        <p className="mt-3 text-muted-foreground">{t.download.subheading}</p>
      </Reveal>
      <div className="mx-auto mt-10 grid max-w-2xl gap-4 sm:grid-cols-2">
        <motion.div
          whileHover={{ y: -4 }}
          transition={{ duration: 0.2 }}
          className="flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-6 text-center"
        >
          <Laptop className="size-8 text-primary" />
          <div>
            <h3 className="font-medium">{t.download.windowsTitle}</h3>
            <p className="text-sm text-muted-foreground">{t.download.windowsBit}</p>
          </div>
          <a
            href={DOWNLOAD_WIN_X64}
            className="mt-2 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
          >
            <DownloadIcon className="size-4" />
            {t.download.downloadLabel}
          </a>
        </motion.div>
        <motion.div
          whileHover={{ y: -4 }}
          transition={{ duration: 0.2 }}
          className="flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-6 text-center"
        >
          <Apple className="size-8 text-primary" />
          <div>
            <h3 className="font-medium">{t.download.macosTitle}</h3>
            <p className="text-sm text-muted-foreground">{t.download.macosPlatforms}</p>
          </div>
          <div className="mt-2 flex gap-2">
            <a
              href={DOWNLOAD_OSX_ARM64}
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-3 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
            >
              <DownloadIcon className="size-4" />
              {t.download.appleSiliconLabel}
            </a>
            <a
              href={DOWNLOAD_OSX_X64}
              className="inline-flex items-center gap-1.5 rounded-lg border border-border px-4 py-3 text-sm font-medium transition-colors hover:bg-muted"
            >
              {t.download.intelLabel}
            </a>
          </div>
        </motion.div>
      </div>
      <Collapsible
        className="mx-auto mt-8 max-w-2xl bg-card"
        summary={
          <span className="flex items-center gap-2">
            <Laptop className="size-4 text-primary" />
            {win.summary}
          </span>
        }
      >
        <div className="space-y-5 border-t border-border p-6 pt-5 text-sm text-muted-foreground">
          <div>
            <p className="font-medium text-foreground">{win.step1Title}</p>
            <p className="mt-1">
              {win.step1TextBefore}{' '}
              <code className="rounded bg-muted px-1.5 py-0.5 text-xs">C:\Portix</code>
              {win.step1TextAfter}
            </p>
          </div>
          <div>
            <p className="font-medium text-foreground">{win.step2Title}</p>
            <p className="mt-1">
              {win.step2TextBefore} <code className="rounded bg-muted px-1.5 py-0.5 text-xs">portix</code>{' '}
              {win.step2TextAfter}
            </p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">
              [Environment]::SetEnvironmentVariable('Path', $env:Path + ';C:\Portix', 'User')
            </pre>
            <p className="mt-2">
              {win.step2ManualBefore}{' '}
              <code className="rounded bg-muted px-1.5 py-0.5 text-xs">Path</code> {win.step2ManualMid}{' '}
              {win.step2ManualAfter}
            </p>
          </div>
          <div>
            <p className="font-medium text-foreground">{win.step3Title}</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">portix --help</pre>
          </div>
          <div className="rounded-lg border border-accent/30 bg-accent/10 p-4">
            <p className="flex items-center gap-2 font-medium text-foreground">
              <ShieldAlert className="size-4 text-accent" />
              {win.smartScreenTitle}
            </p>
            <p className="mt-2">{win.smartScreenIntro}</p>
            <ol className="mt-2 list-decimal space-y-1 pl-5">
              <li>{win.smartScreenStep1}</li>
              <li>{win.smartScreenStep2}</li>
            </ol>
          </div>
        </div>
      </Collapsible>
      <Collapsible
        className="mx-auto mt-4 max-w-2xl bg-card"
        summary={
          <span className="flex items-center gap-2">
            <Apple className="size-4 text-primary" />
            {mac.summary}
          </span>
        }
      >
        <div className="space-y-5 border-t border-border p-6 pt-5 text-sm text-muted-foreground">
          <div>
            <p className="font-medium text-foreground">{mac.step1Title}</p>
            <p className="mt-1">
              {mac.step1TextBefore} <code className="rounded bg-muted px-1.5 py-0.5 text-xs">/usr/local/bin</code>:
            </p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">
              sudo unzip ~/Downloads/portix-osx-arm64.zip -d /usr/local/bin/
            </pre>
          </div>
          <div>
            <p className="font-medium text-foreground">{mac.step2Title}</p>
            <p className="mt-1">{mac.step2Text}</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">sudo chmod 755 /usr/local/bin/portix</pre>
          </div>
          <div>
            <p className="font-medium text-foreground">{mac.step3Title}</p>
            <pre className="mt-2 overflow-x-auto rounded-lg bg-muted p-3 font-mono text-xs text-foreground">portix --help</pre>
          </div>
          <div className="rounded-lg border border-accent/30 bg-accent/10 p-4">
            <p className="flex items-center gap-2 font-medium text-foreground">
              <ShieldAlert className="size-4 text-accent" />
              {mac.securityTitle}
            </p>
            <p className="mt-2">{mac.securityIntro}</p>
            <ol className="mt-2 list-decimal space-y-1 pl-5">
              <li>{mac.securityStep1}</li>
              <li>{mac.securityStep2}</li>
              <li>{mac.securityStep3}</li>
              <li>{mac.securityStep4}</li>
            </ol>
          </div>
        </div>
      </Collapsible>

      <p className="mt-6 text-center text-sm text-muted-foreground">
        {t.download.seeAllVersionsPrefix}{' '}
        <a href={RELEASES_URL} className="underline hover:text-foreground">
          {t.download.githubReleasesLabel}
        </a>
        .
      </p>
    </section>
  )
}
