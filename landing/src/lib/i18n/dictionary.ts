export interface FeatureItem {
  title: string
  description: string
}

export interface ArchitectureComponent {
  title: string
  description: string
}

export interface FlowNode {
  label: string
}

export interface Step {
  title: string
  description: string
}

export interface CliCommand {
  description: string
}

export interface Dictionary {
  meta: {
    title: string
    description: string
  }
  themeToggle: {
    switchToLight: string
    switchToDark: string
  }
  languageToggle: {
    switchToVietnamese: string
    switchToEnglish: string
  }
  hero: {
    badge: string
    heading: string
    subheading: string
    downloadCta: string
    githubCta: string
  }
  features: {
    heading: string
    subheading: string
    items: FeatureItem[]
  }
  cliDemo: {
    heading: string
    subheading: string
    httpRequestsLabel: string
    tableHeaders: {
      time: string
      method: string
      path: string
      status: string
    }
  }
  architecture: {
    heading: string
    subheading: string
    flowDiagramAriaLabel: string
    flowNodes: FlowNode[]
    components: ArchitectureComponent[]
  }
  download: {
    heading: string
    subheading: string
    windowsTitle: string
    windowsBit: string
    macosTitle: string
    macosPlatforms: string
    downloadLabel: string
    appleSiliconLabel: string
    intelLabel: string
    seeAllVersionsPrefix: string
    githubReleasesLabel: string
    windowsInstructions: {
      summary: string
      step1Title: string
      step1TextBefore: string
      step1TextAfter: string
      step2Title: string
      step2TextBefore: string
      step2TextAfter: string
      step2ManualBefore: string
      step2ManualMid: string
      step2ManualAfter: string
      step3Title: string
      smartScreenTitle: string
      smartScreenIntro: string
      smartScreenStep1: string
      smartScreenStep2: string
    }
    macInstructions: {
      summary: string
      step1Title: string
      step1TextBefore: string
      step2Title: string
      step2Text: string
      step3Title: string
      securityTitle: string
      securityIntro: string
      securityStep1: string
      securityStep2: string
      securityStep3: string
      securityStep4: string
    }
  }
  gettingStarted: {
    heading: string
    subheading: string
    steps: Step[]
    cliReferenceSummary: string
    cliCommands: CliCommand[]
    readSetupGuideCta: string
  }
  footer: {
    tagline: string
    madeBy: string
    githubLabel: string
  }
}
