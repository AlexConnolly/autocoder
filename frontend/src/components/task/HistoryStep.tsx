import { useState } from 'react';
import ActionBadge from '../shared/ActionBadge';
import { relativeTime } from '../../utils/formatting';
import type { ContextEntry } from '../../types';

interface Props {
  entry: ContextEntry;
  defaultOpen?: boolean;
}

export default function HistoryStep({ entry, defaultOpen = false }: Props) {
  const [open, setOpen] = useState(defaultOpen);

  if (entry.kind === 'UserAnswer') {
    return (
      <div className="pl-3 border-l border-zinc-300 dark:border-zinc-700">
        <div className="text-[11px] text-zinc-500 mb-1">You replied</div>
        <p className="text-sm text-zinc-700 dark:text-zinc-300">{entry.content}</p>
      </div>
    );
  }

  if (entry.kind === 'SystemNote') {
    return (
      <div className="pl-3 border-l border-zinc-300 dark:border-zinc-700">
        <p className="text-[11px] text-zinc-500 italic">{entry.content}</p>
      </div>
    );
  }

  if (entry.kind === 'ShellOutput') {
    return (
      <div className="pl-3 border-l border-zinc-300 dark:border-zinc-700">
        <div className="text-[11px] text-zinc-500 mb-1 font-mono">$ shell commands</div>
        <p className="text-sm text-zinc-700 dark:text-zinc-300">{entry.content}</p>
      </div>
    );
  }

  // AgentOutput
  let summary: string | undefined;
  try {
    if (entry.structuredData) {
      const parsed = JSON.parse(entry.structuredData);
      summary = parsed?.summary;
    }
  } catch {}

  return (
    <div>
      <button
        onClick={() => setOpen(o => !o)}
        className="flex items-center gap-2 w-full text-left py-1.5 hover:bg-zinc-100/80 dark:hover:bg-zinc-800/50 rounded px-1 -mx-1 transition-colors"
      >
        <span className="text-zinc-600 text-xs w-3 flex-none">{open ? '▼' : '▶'}</span>
        <span className="text-sm text-zinc-800 dark:text-zinc-200 flex-1 truncate">{entry.columnName}</span>
        {entry.action && <ActionBadge action={entry.action} />}
        <span className="text-[11px] text-zinc-600 flex-none">{relativeTime(entry.createdAt)}</span>
      </button>

      {open && (
        <div className="mt-1 ml-3 border-l border-zinc-300 dark:border-zinc-700 pl-3 space-y-3 pb-2">
          {summary && (
            <p className="text-sm text-zinc-700 dark:text-zinc-300 leading-relaxed">{summary}</p>
          )}
          {entry.content && (
            <details className="group">
              <summary className="text-[11px] text-zinc-500 cursor-pointer hover:text-zinc-400 list-none flex items-center gap-1 select-none">
                <span className="group-open:rotate-90 inline-block transition-transform">▶</span>
                Full output
              </summary>
              <div className="mt-2 bg-[var(--color-bg)] rounded p-3 font-mono text-[12px] text-zinc-600 dark:text-zinc-400 whitespace-pre-wrap overflow-x-auto max-h-60 overflow-y-auto leading-relaxed">
                {entry.content}
              </div>
            </details>
          )}
        </div>
      )}
    </div>
  );
}
