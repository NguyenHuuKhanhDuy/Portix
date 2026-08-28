import { MotionConfig } from 'motion/react'
import Hero from './components/Hero'
import Features from './components/Features'
import CliDemo from './components/CliDemo'
import Architecture from './components/Architecture'
import Download from './components/Download'
import GettingStarted from './components/GettingStarted'
import Footer from './components/Footer'
import ThemeToggle from './components/ThemeToggle'
import LanguageToggle from './components/LanguageToggle'
import { LocaleProvider } from './lib/i18n/LocaleContext'

function App() {
  return (
    <LocaleProvider>
      <MotionConfig reducedMotion="user">
        <div className="min-h-screen">
          <div className="fixed top-4 right-4 z-50 flex gap-2">
            <LanguageToggle />
            <ThemeToggle />
          </div>
          <Hero />
          <Features />
          <CliDemo />
          <Architecture />
          <Download />
          <GettingStarted />
          <Footer />
        </div>
      </MotionConfig>
    </LocaleProvider>
  )
}

export default App
