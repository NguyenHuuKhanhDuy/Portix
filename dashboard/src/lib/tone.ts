export type StatusTone = 'success' | 'info' | 'warning' | 'danger'
export type DurationTone = 'normal' | 'warning' | 'slow'

/** Maps an HTTP status code to its class-level tone: 2xx/3xx/4xx/5xx. */
export function statusTone(statusCode: number): StatusTone {
  if (statusCode >= 200 && statusCode < 300) return 'success'
  if (statusCode >= 300 && statusCode < 400) return 'info'
  if (statusCode >= 400 && statusCode < 500) return 'warning'
  return 'danger'
}

/** Maps a request duration to a subtle slowness tone: <500ms, 500ms-1s, >1s. */
export function durationTone(ms: number): DurationTone {
  if (ms > 1000) return 'slow'
  if (ms >= 500) return 'warning'
  return 'normal'
}

export const statusToneBadgeClass: Record<StatusTone, string> = {
  success: 'border-emerald-500/50 text-emerald-600 dark:text-emerald-400',
  info: 'border-sky-500/50 text-sky-600 dark:text-sky-400',
  warning: 'border-orange-500/50 text-orange-600 dark:text-orange-400',
  danger: 'border-destructive/50 text-destructive',
}

export const durationToneTextClass: Record<DurationTone, string> = {
  normal: 'text-muted-foreground',
  warning: 'text-amber-600 dark:text-amber-400',
  slow: 'text-destructive',
}
