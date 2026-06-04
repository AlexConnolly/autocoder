import type { Column, WorkTask } from '../../types';
import { cn } from '../../utils/cn';
import { relativeTime } from '../../utils/formatting';

interface Props {
  task: WorkTask;
  column: Column;
  liveOutput?: string;
  onRetry: (id: string) => void;
  onClick: (id: string) => void;
}

function CheckIcon() {
  return (
    <svg className="w-3.5 h-3.5 text-green-500 flex-none" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M2.5 7l3.5 3.5 5.5-6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function LightningIcon() {
  return (
    <svg className="w-3.5 h-3.5 flex-none mt-0.5" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8.5 1.5L3.5 8H7l-1 4.5 5-6.5H8L8.5 1.5z" stroke="currentColor" strokeWidth="1.25" strokeLinejoin="round" fill="none" />
    </svg>
  );
}

export default function TaskCard({ task, column, liveOutput, onRetry, onClick }: Props) {
  const { status } = task;

  return (
    <div
      className={cn(
        'rounded-lg transition-all duration-150',
        status === 'Waiting'         && cardBase('no-border'),
        status === 'PendingApproval' && cardBase('no-border'),
        status === 'Running'         && cardBase('blue'),
        status === 'Asking'          && cardBase('amber'),
        status === 'Error'           && cardBase('red'),
        status === 'Done'            && cardBase('done'),
      )}
    >
      {status === 'Asking'                                   && <AskingCard   task={task} column={column} onClick={onClick} />}
      {(status === 'Waiting' || status === 'PendingApproval')&& <WaitingCard  task={task} column={column} onClick={onClick} />}
      {status === 'Running'                                  && <RunningCard  task={task} column={column} liveOutput={liveOutput} onClick={onClick} />}
      {status === 'Error'                                    && <ErrorCard    task={task} column={column} onRetry={onRetry} onClick={onClick} />}
      {status === 'Done'                                     && <DoneCard     task={task} onClick={onClick} />}
    </div>
  );
}

// ─── Variants ────────────────────────────────────────────────────────────────

function cardBase(variant: string): string {
  const base = 'bg-surface cursor-pointer shadow-card';
  const variants: Record<string, string> = {
    'no-border': `${base} border border-border hover:bg-surface-raised hover:border-zinc-600`,
    'blue':      `${base} border-l-2 border-blue-500 border-t border-r border-b border-border bg-blue-100/60 dark:bg-blue-950/20`,
    'amber':     `${base} border-l-2 border-amber-500 border-t border-r border-b border-amber-500/30 bg-amber-50/60 dark:bg-amber-950/30 ring-1 ring-amber-500/20`,
    'red':       `${base} border-l-2 border-red-500 border-t border-r border-b border-border bg-red-100/60 dark:bg-red-950/20`,
    'done':      'bg-surface/60 border border-zinc-200 dark:border-zinc-800/50 opacity-60 hover:opacity-80 cursor-pointer',
  };
  return variants[variant] ?? base;
}

// ─── Waiting ─────────────────────────────────────────────────────────────────

function WaitingCard({ task, column, onClick }: { task: WorkTask; column: Column; onClick: (id: string) => void }) {
  return (
    <div className="p-3" onClick={() => onClick(task.id)}>
      <p className="text-sm text-zinc-800 dark:text-zinc-200 font-medium leading-snug">{task.title}</p>
      <div className="flex items-center gap-1.5 mt-1.5">
        <span className="text-[11px] text-zinc-500">{column.name}</span>
        <span className="text-zinc-400 dark:text-zinc-700">·</span>
        <span className="text-[11px] text-zinc-600">{relativeTime(task.updatedAt)}</span>
      </div>
    </div>
  );
}

// ─── Running ─────────────────────────────────────────────────────────────────

function RunningCard({ task, column, liveOutput, onClick }: { task: WorkTask; column: Column; liveOutput?: string; onClick: (id: string) => void }) {
  const lines = liveOutput?.split('\n').filter(Boolean) ?? [];
  const lastLine = lines[lines.length - 1];
  return (
    <div className="p-3" onClick={() => onClick(task.id)}>
      <div className="flex items-start gap-2">
        <span className="mt-1 w-2 h-2 rounded-full bg-blue-500 animate-pulse flex-none" />
        <div className="flex-1 min-w-0">
          <p className="text-sm text-zinc-900 dark:text-zinc-100 font-medium leading-snug">{task.title}</p>
          <span className="text-[11px] text-zinc-500">{column.name}</span>
        </div>
      </div>
      {lastLine && (
        <p className="mt-2 text-[12px] font-mono text-zinc-500 dark:text-zinc-400 truncate">{lastLine}</p>
      )}
    </div>
  );
}

// ─── Asking ──────────────────────────────────────────────────────────────────

function AskingCard({ task, column, onClick }: { task: WorkTask; column: Column; onClick: (id: string) => void }) {
  return (
    <div className="p-3 cursor-pointer" onClick={() => onClick(task.id)}>
      <div className="flex items-start gap-2 mb-2">
        <span className="text-amber-400"><LightningIcon /></span>
        <div className="flex-1 min-w-0">
          <p className="text-sm text-zinc-900 dark:text-zinc-100 font-medium leading-snug">{task.title}</p>
          <span className="text-[11px] text-amber-500/80">{column.name} — needs your input</span>
        </div>
      </div>
      {task.pendingQuestion && (
        <>
          <div className="border-t border-amber-500/20 my-2" />
          <p className="text-[13px] text-zinc-600 dark:text-zinc-400 italic leading-relaxed line-clamp-3">
            {task.pendingQuestion}
          </p>
          <div className="mt-2.5">
            <span className="text-[11px] text-brand-400 bg-brand-500/10 border border-brand-500/30 rounded-full px-2 py-0.5">
              Answer →
            </span>
          </div>
        </>
      )}
    </div>
  );
}

// ─── Error ───────────────────────────────────────────────────────────────────

function ErrorCard({ task, column, onRetry, onClick }: { task: WorkTask; column: Column; onRetry: (id: string) => void; onClick: (id: string) => void }) {
  return (
    <div className="p-3" onClick={() => onClick(task.id)}>
      <div className="flex items-start gap-2 mb-2">
        <span className="text-red-400 text-sm flex-none mt-0.5">✕</span>
        <div className="flex-1 min-w-0">
          <p className="text-sm text-zinc-900 dark:text-zinc-100 font-medium leading-snug">{task.title}</p>
          <span className="text-[11px] text-zinc-500">{column.name}</span>
        </div>
      </div>
      {task.errorMessage && (
        <p className="text-[12px] text-red-400/80 line-clamp-2 mb-2">{task.errorMessage}</p>
      )}
      <button
        onClick={(e) => { e.stopPropagation(); onRetry(task.id); }}
        className="text-xs text-red-500 dark:text-red-400 border border-red-300/60 dark:border-red-800/50 bg-red-50/60 dark:bg-red-950/20 hover:bg-red-100/60 dark:hover:bg-red-950/40 rounded-md px-2 py-1 transition-colors"
      >
        Retry
      </button>
    </div>
  );
}

// ─── Done ────────────────────────────────────────────────────────────────────

function DoneCard({ task, onClick }: { task: WorkTask; onClick: (id: string) => void }) {
  return (
    <div className="px-3 py-2 flex items-center gap-2" onClick={() => onClick(task.id)}>
      <p className="text-sm text-zinc-500 flex-1 truncate">{task.title}</p>
      <CheckIcon />
    </div>
  );
}
