import { MotionConfig } from 'motion/react'
import Hero from './components/Hero'
import Features from './components/Features'
import CliDemo from './components/CliDemo'
import Architecture from './components/Architecture'
import Download from './components/Download'
import GettingStarted from './components/GettingStarted'
import Footer from './components/Footer'
import ThemeToggle from './components/ThemeToggle'

function App() {
  return (
    <MotionConfig reducedMotion="user">
      <div className="min-h-screen">
        <div className="fixed top-4 right-4 z-50">
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
  )
}

export default App
