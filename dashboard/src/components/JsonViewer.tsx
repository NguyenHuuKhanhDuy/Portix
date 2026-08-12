import { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronRight, Copy } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Input } from '@/components/ui/input'
import { copyToClipboard } from '@/lib/clipboard'
import { cn } from '@/lib/utils'

type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

function isPlainObject(value: JsonValue): value is { [key: string]: JsonValue } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isContainer(value: JsonValue): value is JsonValue[] | { [key: string]: JsonValue } {
  return isPlainObject(value) || Array.isArray(value)
}

function entriesOf(value: { [key: string]: JsonValue } | JsonValue[]): (readonly [string, JsonValue])[] {
  return Array.isArray(value) ? value.map((v, i) => [String(i), v] as const) : Object.entries(value)
}

/** Every container node's path at or below `depth < 2`, seeding the tree's default-open state. */
function collectDefaultOpenPaths(value: JsonValue, path: string, depth: number, out: Set<string>) {
  if (!isContainer(value)) return
  if (depth < 2) out.add(path)
  for (const [key, child] of entriesOf(value)) {
    collectDefaultOpenPaths(child, `${path}/${key}`, depth + 1, out)
  }
}

/** Every container node's path in the whole tree, for "expand all". */
function collectAllContainerPaths(value: JsonValue, path: string, out: Set<string>) {
  if (!isContainer(value)) return
  out.add(path)
  for (const [key, child] of entriesOf(value)) {
    collectAllContainerPaths(child, `${path}/${key}`, out)
  }
}

/** Paths of nodes whose key or primitive value contains `query` (already lowercased). */
function collectMatches(value: JsonValue, path: string, key: string | undefined, query: string, out: Set<string>) {
  const keyMatches = key !== undefined && key.toLowerCase().includes(query)
  if (isContainer(value)) {
    if (keyMatches) out.add(path)
    for (const [childKey, child] of entriesOf(value)) {
      collectMatches(child, `${path}/${childKey}`, childKey, query, out)
    }
  } else {
    const valueText = value === null ? 'null' : String(value)
    if (keyMatches || valueText.toLowerCase().includes(query)) out.add(path)
  }
}

/** Every ancestor path of `path` (not including `path` itself), so a deep match can force its parents open. */
function ancestorsOf(path: string): string[] {
  const segments = path.split('/')
  const result: string[] = []
  let acc = segments[0]
  for (let i = 1; i < segments.length - 1; i++) {
    result.push(acc)
    acc = `${acc}/${segments[i]}`
  }
  if (segments.length > 1) result.push(acc)
  return result
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
  path: string
  depth: number
  openPaths: Set<string>
  toggleNode: (path: string) => void
  matches: Set<string>
  highlight: boolean
}

function JsonNode({ value, name, path, depth, openPaths, toggleNode, matches, highlight }: JsonNodeProps) {
  const isMatch = highlight && matches.has(path)
  const highlightClass = isMatch && 'rounded-sm bg-yellow-300/50 dark:bg-yellow-500/30'

  const keyLabel = name !== undefined && (
    <span className={cn('text-sky-600 dark:text-sky-400', highlightClass)}>{JSON.stringify(name)}: </span>
  )

  if (!isContainer(value)) {
    return (
      <div className="py-0.5" style={{ paddingLeft: depth * 16 }}>
        {keyLabel}
        <span className={cn(highlightClass)}>
          <JsonPrimitive value={value} />
        </span>
      </div>
    )
  }

  const open = openPaths.has(path)
  const isArray = Array.isArray(value)
  const entries = entriesOf(value)
  const [openBracket, closeBracket] = isArray ? ['[', ']'] : ['{', '}']

  return (
    <Collapsible open={open} onOpenChange={() => toggleNode(path)}>
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
            <JsonNode
              key={key}
              name={isArray ? undefined : key}
              value={childValue}
              path={`${path}/${key}`}
              depth={depth + 1}
              openPaths={openPaths}
              toggleNode={toggleNode}
              matches={matches}
              highlight={highlight}
            />
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

/** Renders `text` as a collapsible, searchable JSON tree if it parses as JSON; otherwise falls back to plain text. */
export function JsonViewer({ text, className }: JsonViewerProps) {
  const [query, setQuery] = useState('')

  const parsed = useMemo((): { ok: true; value: JsonValue } | { ok: false } => {
    try {
      return { ok: true, value: JSON.parse(text) }
    } catch {
      return { ok: false }
    }
  }, [text])

  const [openPaths, setOpenPaths] = useState<Set<string>>(() => {
    const out = new Set<string>()
    if (parsed.ok) collectDefaultOpenPaths(parsed.value, '$', 0, out)
    return out
  })

  // A different payload (e.g. switching to another captured request) resets expand state and search.
  useEffect(() => {
    const out = new Set<string>()
    if (parsed.ok) collectDefaultOpenPaths(parsed.value, '$', 0, out)
    setOpenPaths(out)
    setQuery('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text])

  const toggleNode = useCallback((path: string) => {
    setOpenPaths((prev) => {
      const next = new Set(prev)
      if (next.has(path)) next.delete(path)
      else next.add(path)
      return next
    })
  }, [])

  const expandAll = useCallback(() => {
    if (!parsed.ok) return
    const out = new Set<string>()
    collectAllContainerPaths(parsed.value, '$', out)
    setOpenPaths(out)
  }, [parsed])

  const collapseAll = useCallback(() => setOpenPaths(new Set()), [])

  const matches = useMemo(() => {
    const trimmed = query.trim().toLowerCase()
    if (!parsed.ok || !trimmed) return new Set<string>()
    const out = new Set<string>()
    collectMatches(parsed.value, '$', undefined, trimmed, out)
    return out
  }, [parsed, query])

  // Search results stay visible even if their ancestors are currently collapsed, without
  // discarding the user's own manual expand/collapse state once the search is cleared.
  const effectiveOpenPaths = useMemo(() => {
    if (matches.size === 0) return openPaths
    const merged = new Set(openPaths)
    for (const path of matches) {
      for (const ancestor of ancestorsOf(path)) merged.add(ancestor)
    }
    return merged
  }, [openPaths, matches])

  if (!parsed.ok) {
    return (
      <div className={cn('flex flex-col gap-2', className)}>
        <Button variant="ghost" size="xs" onClick={() => copyToClipboard(text)} className="self-start">
          <Copy /> Copy
        </Button>
        <pre className="whitespace-pre-wrap break-all text-sm">{text}</pre>
      </div>
    )
  }

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className="flex flex-wrap items-center gap-1">
        <Button variant="ghost" size="xs" onClick={() => copyToClipboard(text)}>
          <Copy /> Copy
        </Button>
        <Button variant="ghost" size="xs" onClick={expandAll}>
          Expand all
        </Button>
        <Button variant="ghost" size="xs" onClick={collapseAll}>
          Collapse all
        </Button>
        <Input
          placeholder="Search..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          className="ml-auto h-6 max-w-40 text-xs"
        />
      </div>
      <div className="font-mono text-sm">
        <JsonNode
          value={parsed.value}
          path="$"
          depth={0}
          openPaths={effectiveOpenPaths}
          toggleNode={toggleNode}
          matches={matches}
          highlight={query.trim() !== ''}
        />
      </div>
    </div>
  )
}
