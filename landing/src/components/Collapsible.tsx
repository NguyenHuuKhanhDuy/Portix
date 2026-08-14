import { useState, type ReactNode } from 'react'
import { ChevronDown } from 'lucide-react'

interface CollapsibleProps {
  summary: ReactNode
  children: ReactNode
  className?: string
}

export default function Collapsible({ summary, children, className = '' }: CollapsibleProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className={`rounded-xl border border-border ${className}`}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        className="flex w-full cursor-pointer items-center justify-between gap-2 p-4 text-left font-medium"
      >
        {summary}
        <ChevronDown
          className={`size-4 shrink-0 text-muted-foreground transition-transform duration-300 ${open ? 'rotate-180' : ''}`}
        />
      </button>
      <div
        className="grid transition-[grid-template-rows] duration-300 ease-in-out"
        style={{ gridTemplateRows: open ? '1fr' : '0fr' }}
      >
        <div className="overflow-hidden">{children}</div>
      </div>
    </div>
  )
}
