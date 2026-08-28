import type { Dictionary } from './dictionary'

export const en: Dictionary = {
  meta: {
    title: 'Portix — Self-hosted tunnels for your local apps',
    description:
      'Portix lets you securely expose your local applications through public URLs using your own self-hosted tunnel server.',
  },
  themeToggle: {
    switchToLight: 'Switch to light theme',
    switchToDark: 'Switch to dark theme',
  },
  languageToggle: {
    switchToVietnamese: 'Switch to Vietnamese',
    switchToEnglish: 'Switch to English',
  },
  hero: {
    badge: 'Self-hosted tunnels',
    heading: 'Portix',
    subheading:
      'Expose a local port through a public URL served by your own tunnel server, with a live traffic inspector built in.',
    downloadCta: 'Download',
    githubCta: 'View on GitHub',
  },
  features: {
    heading: 'Everything you need to expose local apps',
    subheading: 'A simple CLI workflow for opening tunnels, running entirely on infrastructure you control.',
    items: [
      {
        title: 'Self-hosted',
        description: 'Run your own tunnel server on your own domain and infrastructure — no third-party relay in between.',
      },
      {
        title: 'Live traffic inspector',
        description: 'Watch every request hit your tunnel in real time from a local web dashboard, no extra setup required.',
      },
      {
        title: 'Familiar CLI',
        description: 'Simple `portix http <port>` commands with a live status panel for every open tunnel.',
      },
      {
        title: 'HTTP & HTTPS tunneling',
        description: 'Expose HTTP or HTTPS local apps through your tunnel, including WebSocket upgrades.',
      },
      {
        title: 'Simple tunnel management',
        description: 'List and close open tunnels at any time with `portix ls` and `portix rm`.',
      },
    ],
  },
  cliDemo: {
    heading: 'One command, live traffic',
    subheading: 'Open a tunnel and watch requests come through as they happen.',
    httpRequestsLabel: 'HTTP Requests',
    tableHeaders: {
      time: 'Time',
      method: 'Method',
      path: 'Path',
      status: 'Status',
    },
  },
  architecture: {
    heading: 'How a tunnel works',
    subheading:
      'The Client opens a long-lived control connection to the Server and registers a subdomain. When a public request arrives, the Server signals the Client, which opens a data connection back to relay bytes in both directions — including WebSocket upgrades.',
    flowDiagramAriaLabel:
      'A request flows from your app through the Client and Server to a public visitor, and the response flows back.',
    flowNodes: [
      { label: 'Your app' },
      { label: 'Client' },
      { label: 'Server' },
      { label: 'Visitor' },
    ],
    components: [
      {
        title: 'Server',
        description:
          'The public-facing relay. Accepts client control connections, routes public traffic to the right tunnel by subdomain, and manages users and tokens.',
      },
      {
        title: 'Client',
        description:
          'The `portix` CLI and local daemon. Registers tunnels with the Server, forwards traffic to your local app, and serves the traffic-inspector dashboard.',
      },
      {
        title: 'Shared',
        description:
          'The wire protocol code used by both Server and Client — message framing, types, and header handling.',
      },
    ],
  },
  download: {
    heading: 'Download Portix',
    subheading: 'Grab the latest build for your platform — extract and run, no install needed.',
    windowsTitle: 'Windows',
    windowsBit: '64-bit',
    macosTitle: 'macOS',
    macosPlatforms: 'Apple Silicon or Intel',
    downloadLabel: 'Download',
    appleSiliconLabel: 'Apple Silicon',
    intelLabel: 'Intel',
    seeAllVersionsPrefix: 'See all versions on',
    githubReleasesLabel: 'GitHub Releases',
    windowsInstructions: {
      summary: 'Windows install instructions',
      step1Title: '1. Extract the binary',
      step1TextBefore: "Unzip the downloaded file to a folder you'll keep around, e.g.",
      step1TextAfter: '.',
      step2Title: '2. (Optional) Add it to PATH',
      step2TextBefore: 'So you can run',
      step2TextAfter: 'from any terminal without typing the full path. In PowerShell:',
      step2ManualBefore: 'Or manually: Settings → System → About → Advanced system settings → Environment Variables → edit',
      step2ManualMid: 'under "User variables" → add the',
      step2ManualAfter: 'folder. Restart your terminal afterwards.',
      step3Title: '3. Run Portix',
      smartScreenTitle: 'Windows SmartScreen notice',
      smartScreenIntro: 'If you see "Windows protected your PC":',
      smartScreenStep1: 'Click "More info"',
      smartScreenStep2: 'Click "Run anyway"',
    },
    macInstructions: {
      summary: 'macOS install instructions',
      step1Title: '1. Extract the binary',
      step1TextBefore: 'Unzip the downloaded file to',
      step2Title: '2. Set execute permission',
      step2Text: 'Make the binary executable:',
      step3Title: '3. Run Portix',
      securityTitle: 'macOS security notice',
      securityIntro: 'If you see "portix" was blocked to protect your Mac:',
      securityStep1: 'Go to System Settings → Privacy & Security',
      securityStep2: 'Scroll down to Security',
      securityStep3: 'Click "Open Anyway"',
      securityStep4: 'Confirm to run the app',
    },
  },
  gettingStarted: {
    heading: 'Ready to expose your first app?',
    subheading: 'Three steps to get a public URL pointing at something running on your machine.',
    steps: [
      {
        title: 'Open a terminal',
        description: 'Go to the folder where you extracted the download above.',
      },
      {
        title: 'Log in',
        description: 'Use the API token and server address your admin gave you.',
      },
      {
        title: 'Open a tunnel',
        description: 'Expose a local port and share the public URL it prints.',
      },
    ],
    cliReferenceSummary: 'CLI reference',
    cliCommands: [
      { description: 'Expose a local HTTP port through a public tunnel.' },
      { description: 'Expose a local HTTPS port through a public tunnel.' },
      { description: 'List currently open tunnels.' },
      { description: 'Close a tunnel by id.' },
      { description: 'Save a personal API token for this machine.' },
      { description: 'Remove the saved API token.' },
      { description: 'Show usage.' },
    ],
    readSetupGuideCta: 'Read the full setup guide',
  },
  footer: {
    tagline: 'Portix — a self-hosted tunnel server.',
    madeBy: 'Made by',
    githubLabel: 'GitHub',
  },
}
