import { useLocale } from '../lib/i18n/LocaleContext'

export default function LanguageToggle() {
  const { locale, t, setLocale } = useLocale()
  const next = locale === 'en' ? 'vi' : 'en'

  return (
    <button
      type="button"
      onClick={() => setLocale(next)}
      aria-label={locale === 'en' ? t.languageToggle.switchToVietnamese : t.languageToggle.switchToEnglish}
      className="inline-flex size-11 items-center justify-center rounded-full border border-border bg-card text-sm font-medium text-foreground transition-colors hover:bg-muted cursor-pointer"
    >
      {next.toUpperCase()}
    </button>
  )
}
