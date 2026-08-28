import { ArrowRight, Terminal } from 'lucide-react'
import { EXAMPLE_API_TOKEN, EXAMPLE_LOCAL_PORT, EXAMPLE_SERVER_URL, README_URL } from '../lib/constants'
import Collapsible from './Collapsible'
import Reveal from './Reveal'
import { useLocale } from '../lib/i18n/LocaleContext'

const STEP_COMMANDS = [
  'cd path/to/portix',
  `portix login <${EXAMPLE_API_TOKEN}> --server ${EXAMPLE_SERVER_URL}`,
  `portix http ${EXAMPLE_LOCAL_PORT}`,
]

const CLI_COMMAND_STRINGS = [
  'portix http <port> [--subdomain <name>]',
  'portix https <port> [--subdomain <name>]',
  'portix ls',
  'portix rm <id>',
  'portix login <token> [--server <url>]',
  'portix logout',
  'portix --help',
]

export default function GettingStarted() {
  const { t } = useLocale()
  const steps = t.gettingStarted.steps.map((step, index) => ({ ...step, command: STEP_COMMANDS[index] }))
  const cliCommands = t.gettingStarted.cliCommands.map((item, index) => ({
    ...item,
    command: CLI_COMMAND_STRINGS[index],
  }))

  return (
    <section className="mx-auto max-w-4xl px-6 py-16 sm:py-20">
      <Reveal className="rounded-2xl border border-border bg-card px-6 py-12 text-center sm:px-12">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">{t.gettingStarted.heading}</h2>
        <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
          {t.gettingStarted.subheading}
        </p>
        <ol className="mx-auto mt-8 grid gap-6 text-left sm:grid-cols-3">
          {steps.map((step, index) => (
            <li key={step.title}>
              <span className="inline-flex size-8 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground">
                {index + 1}
              </span>
              <p className="mt-3 font-medium">{step.title}</p>
              <p className="mt-1 text-sm text-muted-foreground">{step.description}</p>
              <pre className="mt-3 overflow-x-auto whitespace-pre-wrap rounded-lg bg-muted p-3 text-left font-mono text-xs text-foreground">
                {step.command}
              </pre>
            </li>
          ))}
        </ol>

        <Collapsible
          className="mx-auto mt-10 max-w-2xl bg-background text-left"
          summary={
            <span className="flex items-center gap-2">
              <Terminal className="size-4 text-primary" />
              {t.gettingStarted.cliReferenceSummary}
            </span>
          }
        >
          <div className="space-y-3 border-t border-border p-6 pt-5 text-sm">
            {cliCommands.map((item) => (
              <div key={item.command} className="grid gap-1 sm:grid-cols-[1fr_1.2fr] sm:gap-4">
                <code className="overflow-x-auto rounded bg-muted px-2 py-1 font-mono text-xs text-foreground">
                  {item.command}
                </code>
                <p className="text-muted-foreground">{item.description}</p>
              </div>
            ))}
          </div>
        </Collapsible>

        <a
          href={README_URL}
          className="mt-10 inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-6 py-3 font-medium text-primary-foreground transition-opacity hover:opacity-90"
        >
          {t.gettingStarted.readSetupGuideCta}
          <ArrowRight className="size-4" />
        </a>
      </Reveal>
    </section>
  )
}
