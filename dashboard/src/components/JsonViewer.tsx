import { useState } from 'react'
import { ChevronRight } from 'lucide-react'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { cn } from '@/lib/utils'

type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

function isPlainObject(value: JsonValue): value is { [key: string]: JsonValue } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function JsonPrimitive({ value }: { value: string | number | boolean | null }) {
  if (value === null) return <span className="text-muted-foreground">null</span>
  if (typeof value === 'boolean') {
    return <span className="text-amber-600 dark:text-amber-400">{String(value)}</span>
  }
  if (typeof value === 'number') {
    return <span className="text-emerald-600 dark:text-emerald-400">{value}</span>
  }
  return <span className="text-rose-600 dark:text-rose-400">{JSON.stringify(value)}</span>
}

interface JsonNodeProps {
  value: JsonValue
  name?: string
  depth: number
}

function JsonNode({ value, name, depth }: JsonNodeProps) {
  const isContainer = isPlainObject(value) || Array.isArray(value)
  // Auto-expand the first couple of levels so small payloads are readable at a glance;
  // deeper nodes start collapsed to avoid dumping huge trees open by default.
  const [open, setOpen] = useState(depth < 2)

  const keyLabel = name !== undefined && (
    <span className="text-sky-600 dark:text-sky-400">{JSON.stringify(name)}: </span>
  )

  if (!isContainer) {
    return (
      <div className="py-0.5" style={{ paddingLeft: depth * 16 }}>
        {keyLabel}
        <JsonPrimitive value={value} />
      </div>
    )
  }

  const isArray = Array.isArray(value)
  const entries = isArray ? value.map((v, i) => [String(i), v] as const) : Object.entries(value)
  const [openBracket, closeBracket] = isArray ? ['[', ']'] : ['{', '}']

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <div style={{ paddingLeft: depth * 16 }}>
        <CollapsibleTrigger className="-mx-1 flex items-center gap-1 rounded px-1 hover:bg-muted">
          <ChevronRight className={cn('h-3 w-3 shrink-0 transition-transform', open && 'rotate-90')} />
          {keyLabel}
          <span className="text-muted-foreground">{openBracket}</span>
          {!open && (
            <span className="text-muted-foreground text-xs">
              {entries.length} item{entries.length === 1 ? '' : 's'} {closeBracket}
            </span>
          )}
        </CollapsibleTrigger>
        <CollapsibleContent>
          {entries.map(([key, childValue]) => (
            <JsonNode key={key} name={isArray ? undefined : key} value={childValue} depth={depth + 1} />
          ))}
          <div className="text-muted-foreground" style={{ paddingLeft: 16 }}>
            {closeBracket}
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  )
}

interface JsonViewerProps {
  text: string
  className?: string
}

/** Renders `text` as a collapsible JSON tree if it parses as JSON; otherwise falls back to plain text. */
export function JsonViewer({ text, className }: JsonViewerProps) {
  let parsed: JsonValue
  try {
    parsed = JSON.parse(text)
  } catch {
    return <pre className={cn('whitespace-pre-wrap break-all text-sm', className)}>{text}</pre>
  }

  return (
    <div className={cn('font-mono text-sm', className)}>
      <JsonNode value={parsed} depth={0} />
    </div>
  )
}
