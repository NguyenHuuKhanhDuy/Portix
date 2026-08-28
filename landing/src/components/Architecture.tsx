import { Globe, Laptop, LayoutDashboard, Radio, Server } from "lucide-react";
import { motion } from "motion/react";
import { Fragment } from "react";
import Reveal from "./Reveal";
import { useLocale } from "../lib/i18n/LocaleContext";
import type { ArchitectureComponent, FlowNode } from "../lib/i18n/dictionary";

const COMPONENT_ICONS = [Server, LayoutDashboard, Radio];
const FLOW_NODE_ICONS = [Laptop, LayoutDashboard, Server, Globe];

function FlowDiagram({
  flowNodes,
  ariaLabel,
}: {
  flowNodes: FlowNode[];
  ariaLabel: string;
}) {
  return (
    <div
      className="relative mx-auto mt-10 flex w-full max-w-2xl items-center"
      role="img"
      aria-label={ariaLabel}
    >
      {/* Positioning rail: sits at the icon circles' vertical center so the single dot travels in a straight line. */}
      <div className="absolute inset-x-0 top-5 sm:top-6">
        <span className="tunnel-dot size-1.5 rounded-full bg-primary sm:size-2" />
      </div>
      {flowNodes.map(({ label }, index) => {
        const Icon = FLOW_NODE_ICONS[index];
        return (
          <Fragment key={label}>
            <div className="flex flex-col items-center gap-1.5">
              <div className="relative z-10 flex size-10 items-center justify-center rounded-full border border-border bg-card sm:size-12">
                <Icon className="size-4 text-primary sm:size-5" />
              </div>
              <span className="text-xs whitespace-nowrap text-muted-foreground">
                {label}
              </span>
            </div>
            {index < flowNodes.length - 1 && (
              <div className="mt-5 h-px min-w-4 flex-1 self-start bg-border sm:mt-6 sm:h-0.5 sm:min-w-8" />
            )}
          </Fragment>
        );
      })}
    </div>
  );
}

export default function Architecture() {
  const { t } = useLocale();
  const components: (ArchitectureComponent & { icon: (typeof COMPONENT_ICONS)[number] })[] =
    t.architecture.components.map((component, index) => ({
      ...component,
      icon: COMPONENT_ICONS[index],
    }));

  return (
    <section className="mx-auto max-w-6xl px-6 py-16 sm:py-20">
      <Reveal className="mx-auto max-w-2xl text-center">
        <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">
          {t.architecture.heading}
        </h2>
        <p className="mt-3 text-muted-foreground">
          {t.architecture.subheading}
        </p>
      </Reveal>
      <FlowDiagram flowNodes={t.architecture.flowNodes} ariaLabel={t.architecture.flowDiagramAriaLabel} />
      <div className="mt-12 grid gap-6 sm:grid-cols-3">
        {components.map(({ icon: Icon, title, description }) => (
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
  );
}
