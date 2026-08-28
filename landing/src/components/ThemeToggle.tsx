import { useState } from 'react'
import { Moon, Sun } from 'lucide-react'
import { useLocale } from '../lib/i18n/LocaleContext'

export default function ThemeToggle() {
  const { t } = useLocale()
  const [isDark, setIsDark] = useState(
    () => document.documentElement.classList.contains('dark'),
  )

  function toggleTheme() {
    const next = !isDark
    document.documentElement.classList.toggle('dark', next)
    try {
      localStorage.setItem('theme', next ? 'dark' : 'light')
    } catch {
      // localStorage unavailable — theme still applies for this session
    }
    setIsDark(next)
  }

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label={isDark ? t.themeToggle.switchToLight : t.themeToggle.switchToDark}
      className="inline-flex size-11 items-center justify-center rounded-full border border-border bg-card text-foreground transition-colors hover:bg-muted cursor-pointer"
    >
      {isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </button>
  )
}
