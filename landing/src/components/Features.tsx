import { motion } from 'motion/react'
import { Activity, Globe, ListTree, Server, Terminal } from 'lucide-react'
import Reveal from './Reveal'
import { useLocale } from '../lib/i18n/LocaleContext'

const ICONS = [Server, Activity, Terminal, Globe, ListTree]

export default function Features() {
  const { t } = useLocale()
  const features = t.features.items.map((item, index) => ({ ...item, icon: ICONS[index] }))

  return (
    <section className="mx-auto max-w-6xl px-6 py-16 sm:py-20">
      <Reveal className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">{t.features.heading}</h2>
        <p className="mt-3 text-muted-foreground">{t.features.subheading}</p>
      </Reveal>
      <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {features.map(({ icon: Icon, title, description }) => (
          <motion.div
            key={title}
            whileHover={{ y: -4 }}
            transition={{ duration: 0.2 }}
            className="rounded-xl border border-border bg-card p-6"
          >
            <Icon className="size-6 text-primary" />
            <h3 className="mt-4 font-medium">{title}</h3>
            <p className="mt-2 text-sm text-muted-foreground">{description}</p>
          </motion.div>
        ))}
      </div>
    </section>
  )
}
