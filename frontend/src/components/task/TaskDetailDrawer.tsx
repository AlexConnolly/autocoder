import { useEffect, useRef, useState } from 'react';
import type { Board, ContextEntry, WorkTask } from '../../types';
import { cn } from '../../utils/cn';
import { relativeTime } from '../../utils/formatting';
import HistoryStep from './HistoryStep';
import LiveOutputPanel from './LiveOutputPanel';

interface Props {
  task: WorkTask | null;
  board: Board;
  liveOutput?: string;
  contextEntries: ContextEntry[];
  onClose: () => void;
  onAnswer: (taskId: string, answer: string) => void;
  onApprove: (taskId: string) => void;
}

function CloseIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

function LightningIcon() {
  return (
    <svg className="w-3.5 h-3.5 flex-none" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8.5 1.5L3.5 8H7l-1 4.5 5-6.5H8L8.5 1.5z" stroke="currentColor" strokeWidth="1.25" strokeLinejoin="round" fill="none" />
    </svg>
  );
}

export default function TaskDetailDrawer({ task, board, liveOutput, contextEntries, onClose, onAnswer, onApprove }: Props) {
  const isOpen = task !== null;
  const column = board.columns.find(c => c.id === task?.currentColumnId);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  return (
    <>
      {/* Backdrop (mobile) */}
      <div
        className={cn(
          'fixed inset-0 bg-black/60 z-20 md:hidden transition-opacity duration-300',
          isOpen ? 'opacity-100 pointer-events-auto' : 'opacity-0 pointer-events-none'
        )}
        onClick={onClose}
      />

      {/* Drawer */}
      <div className={cn(
        'fixed z-30 bg-[#18181b] border-zinc-800 transition-transform duration-300',
        'bottom-0 left-0 right-0 rounded-t-2xl border-t max-h-[90vh] overflow-y-auto',
        'md:bottom-auto md:top-0 md:right-0 md:left-auto md:w-[480px] md:h-full md:rounded-none md:border-t-0 md:border-l md:shadow-drawer',
        isOpen
          ? 'translate-y-0 md:translate-x-0'
          : 'translate-y-full md:translate-x-full'
      )}>
        {task && (
          <div className="flex flex-col h-full">
            {/* Header */}
            <div className="flex items-start gap-3 p-4 border-b border-zinc-800 sticky top-0 bg-[#18181b] z-10">
              <button
                onClick={onClose}
                className="mt-0.5 text-zinc-500 hover:text-zinc-100 hover:bg-zinc-800 rounded p-1 transition-colors flex-none"
                aria-label="Close"
              >
                <CloseIcon />
              </button>
              <div className="flex-1 min-w-0">
                <h2 className="text-base font-semibold text-zinc-100 truncate">{task.title}</h2>
                <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                  <StatusChip status={task.status} />
                  <span className="text-[11px] text-zinc-500">{column?.name}</span>
                  {task.branchName && (
                    <span className="text-[11px] text-indigo-400 font-mono truncate">{task.branchName}</span>
                  )}
                </div>
              </div>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-y-auto p-4 space-y-6">

              {/* ── Approve panel (PendingApproval state) ───────────────── */}
              {task.status === 'PendingApproval' && (
                <section className="rounded-lg border border-zinc-600/40 bg-zinc-800/30 p-4 space-y-3">
                  <div className="text-xs font-medium text-zinc-500 uppercase tracking-wider">Ready to proceed</div>
                  <p className="text-sm text-zinc-300">The agent finished this stage. Start the next stage?</p>
                  <button
                    onClick={() => onApprove(task.id)}
                    className="px-4 py-1.5 bg-brand-500 hover:bg-brand-600 text-white text-sm font-semibold rounded-lg transition-colors"
                  >
                    Start next stage →
                  </button>
                </section>
              )}

              {/* ── Answer panel (Asking state only) ─────────────────────── */}
              {task.status === 'Asking' && task.pendingQuestion && (
                <AnswerPanel
                  question={task.pendingQuestion}
                  onSubmit={(answer) => onAnswer(task.id, answer)}
                />
              )}

              {/* ── Live output ─────────────────────────────────────────── */}
              {liveOutput && (
                <section>
                  <SectionLabel>Live output</SectionLabel>
                  <LiveOutputPanel output={liveOutput} />
                </section>
              )}

              {/* ── History ─────────────────────────────────────────────── */}
              {contextEntries.length > 0 && (
                <section>
                  <SectionLabel>History</SectionLabel>
                  <div className="space-y-1">
                    {contextEntries.map((e, i) => (
                      <HistoryStep key={e.id} entry={e} defaultOpen={i === contextEntries.length - 1} />
                    ))}
                  </div>
                </section>
              )}

              {/* ── Metadata ────────────────────────────────────────────── */}
              <section>
                <SectionLabel>Details</SectionLabel>
                <dl className="space-y-2 text-sm">
                  {task.branchName && (
                    <Row label="Branch">
                      <span className="font-mono text-indigo-400 text-[13px]">{task.branchName}</span>
                    </Row>
                  )}
                  <Row label="Board"><span className="text-zinc-300">{board.name}</span></Row>
                  <Row label="Created"><span className="text-zinc-300">{relativeTime(task.createdAt)}</span></Row>
                  <Row label="Updated"><span className="text-zinc-300">{relativeTime(task.updatedAt)}</span></Row>
                  {task.errorMessage && (
                    <Row label="Error"><span className="text-red-400 text-[13px]">{task.errorMessage}</span></Row>
                  )}
                </dl>
              </section>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

// ─── Answer panel ────────────────────────────────────────────────────────────

function AnswerPanel({ question, onSubmit }: { question: string; onSubmit: (answer: string) => void }) {
  const [answer, setAnswer] = useState('');
  const inputRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    setTimeout(() => inputRef.current?.focus(), 100);
  }, []);

  const submit = () => {
    if (answer.trim()) {
      onSubmit(answer.trim());
      setAnswer('');
    }
  };

  return (
    <section className="rounded-lg border border-amber-500/40 bg-amber-500/5 p-4 space-y-3">
      <div className="flex items-center gap-2">
        <span className="text-amber-400"><LightningIcon /></span>
        <SectionLabel>Agent needs your input</SectionLabel>
      </div>
      <p className="text-sm text-zinc-200 leading-relaxed">{question}</p>
      <div className="border-t border-amber-500/20 pt-3 space-y-2">
        <textarea
          ref={inputRef}
          value={answer}
          onChange={e => setAnswer(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) submit(); }}
          placeholder="Type your answer…"
          rows={3}
          className="w-full bg-[#0f0f10] border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/40 resize-none transition-colors"
        />
        <div className="flex items-center justify-between">
          <span className="text-[11px] text-zinc-600">⌘ Enter to submit</span>
          <button
            onClick={submit}
            disabled={!answer.trim()}
            className="px-4 py-1.5 bg-amber-500 text-zinc-950 text-sm font-semibold rounded-lg hover:bg-amber-400 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Submit answer →
          </button>
        </div>
      </div>
    </section>
  );
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div className="text-xs font-medium uppercase tracking-wider text-zinc-500 mb-2">
      {children}
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-4">
      <dt className="text-zinc-500 w-20 flex-none">{label}</dt>
      <dd className="flex-1 min-w-0">{children}</dd>
    </div>
  );
}

function StatusChip({ status }: { status: string }) {
  const cls: Record<string, string> = {
    Waiting: 'bg-zinc-700/50 text-zinc-400',
    Running: 'bg-blue-500/15 text-blue-400',
    PendingApproval: 'bg-zinc-700/50 text-zinc-400',
    Asking: 'bg-amber-500/15 text-amber-400',
    Done: 'bg-green-500/15 text-green-400',
    Error: 'bg-red-500/15 text-red-400',
  };
  const label: Record<string, React.ReactNode> = {
    Waiting: 'Waiting',
    Running: <span className="flex items-center gap-1"><span className="w-1.5 h-1.5 rounded-full bg-blue-400 animate-pulse" />Running</span>,
    PendingApproval: 'Waiting',
    Asking: <span className="flex items-center gap-1"><LightningIcon />Needs input</span>,
    Done: '✓ Done',
    Error: '✕ Error',
  };
  return (
    <span className={`inline-flex items-center text-[11px] px-2 py-0.5 rounded-full font-medium ${cls[status] ?? ''}`}>
      {label[status] ?? status}
    </span>
  );
}
