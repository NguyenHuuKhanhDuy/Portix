import { ExternalLink, Mail } from "lucide-react";
import { AUTHOR_EMAIL, AUTHOR_NAME, GITHUB_URL } from "../lib/constants";

export default function Footer() {
  return (
    <footer className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 border-t border-border px-6 py-8 text-sm text-muted-foreground sm:flex-row">
      <p>Portix — a self-hosted tunnel server.</p>

      <div className="flex flex-col items-center gap-3 sm:flex-row sm:gap-6">
        <div className="flex items-center gap-1.5">
          <span>Made by {AUTHOR_NAME}</span>
          <span>·</span>
          <a
            href={`mailto:${AUTHOR_EMAIL}`}
            className="inline-flex items-center gap-1.5 underline-offset-2 transition-colors hover:text-foreground hover:underline"
          >
            <Mail className="size-4" />
            <span>{AUTHOR_EMAIL}</span>
          </a>
        </div>

        <a
          href={GITHUB_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 transition-colors hover:text-foreground hover:underline"
        >
          <ExternalLink className="size-4" />
          <span>GitHub</span>
        </a>
      </div>
    </footer>
  );
}
