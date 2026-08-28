import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import type { Dictionary } from './dictionary'
import { en } from './en'
import { vi } from './vi'

export type Locale = 'en' | 'vi'

const DICTIONARIES: Record<Locale, Dictionary> = { en, vi }

function resolveInitialLocale(): Locale {
  try {
    const stored = localStorage.getItem('lang')
    if (stored === 'en' || stored === 'vi') return stored
  } catch {
    // localStorage unavailable — fall through to browser-language detection
  }

  const languages = navigator.languages ?? [navigator.language]
  return languages.some((lang) => lang.toLowerCase().startsWith('vi')) ? 'vi' : 'en'
}

interface LocaleContextValue {
  locale: Locale
  t: Dictionary
  setLocale: (locale: Locale) => void
}

const LocaleContext = createContext<LocaleContextValue | null>(null)

function setMetaContent(selector: string, content: string) {
  document.querySelector(selector)?.setAttribute('content', content)
}

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(resolveInitialLocale)

  useEffect(() => {
    const t = DICTIONARIES[locale]
    document.documentElement.lang = locale
    document.title = t.meta.title
    setMetaContent('meta[name="description"]', t.meta.description)
    setMetaContent('meta[property="og:title"]', t.meta.title)
    setMetaContent('meta[property="og:description"]', t.meta.description)
    setMetaContent('meta[name="twitter:title"]', t.meta.title)
    setMetaContent('meta[name="twitter:description"]', t.meta.description)
  }, [locale])

  function setLocale(next: Locale) {
    try {
      localStorage.setItem('lang', next)
    } catch {
      // localStorage unavailable — locale still applies for this session
    }
    setLocaleState(next)
  }

  return (
    <LocaleContext.Provider value={{ locale, t: DICTIONARIES[locale], setLocale }}>
      {children}
    </LocaleContext.Provider>
  )
}

export function useLocale() {
  const context = useContext(LocaleContext)
  if (!context) throw new Error('useLocale must be used within a LocaleProvider')
  return context
}
