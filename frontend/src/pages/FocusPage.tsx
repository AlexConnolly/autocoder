import { useState } from 'react';
import type { Board, WorkTask } from '../types';
import TaskCard from '../components/task/TaskCard';

interface Props {
  board: Board;
  tasks: WorkTask[];
  liveOutputs: Record<string, string>;
  onRetry: (id: string) => void;
  onSelectTask: (id: string) => void;
  onAnswer: (taskId: string, answer: string) => void;
}

function LightningIcon() {
  return (
    <svg className="w-3.5 h-3.5 flex-none" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8.5 1.5L3.5 8H7l-1 4.5 5-6.5H8L8.5 1.5z" stroke="currentColor" strokeWidth="1.25" strokeLinejoin="round" fill="none" />
    </svg>
  );
}

function InlineAnswerCard({ task, colName, onAnswer, onViewDetails }: {
  task: WorkTask;
  colName: string;
  onAnswer: (taskId: string, answer: string) => void;
  onViewDetails: (id: string) => void;
}) {
  const [answer, setAnswer] = useState('');

  const submit = () => {
    if (answer.trim()) {
      onAnswer(task.id, answer.trim());
      setAnswer('');
    }
  };

  return (
    <div className="rounded-lg border-l-2 border-amber-500 border-t border-r border-b border-amber-500/30 bg-amber-50/60 dark:bg-amber-950/30 ring-1 ring-amber-500/20 shadow-card">
      <div className="p-3 pb-2">
        <div className="flex items-start gap-2 mb-2">
          <span className="text-amber-400 mt-0.5"><LightningIcon /></span>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 leading-snug">{task.title}</p>
            <span className="text-[11px] text-amber-500/80">{colName} — needs your input</span>
          </div>
          <button
            onClick={() => onViewDetails(task.id)}
            className="text-[11px] text-zinc-500 hover:text-zinc-300 flex-none mt-0.5"
          >
            Details
          </button>
        </div>
        {task.pendingQuestion && (
          <p className="text-sm text-zinc-700 dark:text-zinc-300 leading-relaxed border-t border-amber-500/20 pt-2 mb-3">
            {task.pendingQuestion}
          </p>
        )}
        <textarea
          value={answer}
          onChange={e => setAnswer(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) submit(); }}
          placeholder="Type your answer…"
          rows={3}
          className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-amber-500 focus:ring-2 focus:ring-amber-500/40 resize-none transition-colors"
        />
        <div className="flex items-center justify-between mt-2">
          <span className="text-[11px] text-zinc-600">⌘ Enter to submit</span>
          <button
            onClick={submit}
            disabled={!answer.trim()}
            className="px-4 py-1.5 bg-amber-500 text-zinc-950 text-sm font-semibold rounded-lg hover:bg-amber-400 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Submit →
          </button>
        </div>
      </div>
    </div>
  );
}

export default function FocusPage({ board, tasks, liveOutputs, onRetry, onSelectTask, onAnswer }: Props) {
  const asking = tasks.filter(t => t.status === 'Asking');
  const other = tasks.filter(t => t.status !== 'Asking' && t.status !== 'Done');

  if (asking.length === 0 && other.length === 0) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-8">
        <div className="w-3 h-3 rounded-full bg-green-500 animate-pulse ring-2 ring-green-500/20 ring-offset-2 ring-offset-[var(--color-bg)]" />
        <p className="text-sm text-zinc-500 dark:text-zinc-400">All clear. The agents are working.</p>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-3 pb-20">
      {asking.length > 0 && (
        <>
          <p className="text-[11px] font-semibold uppercase tracking-widest text-amber-600 dark:text-amber-500">
            {asking.length} {asking.length === 1 ? 'task needs' : 'tasks need'} your input
          </p>
          {asking.map(task => {
            const column = board.columns.find(c => c.id === task.currentColumnId);
            if (!column) return null;
            return (
              <InlineAnswerCard
                key={task.id}
                task={task}
                colName={column.name}
                onAnswer={onAnswer}
                onViewDetails={onSelectTask}
              />
            );
          })}
        </>
      )}

      {other.length > 0 && (
        <>
          {asking.length > 0 && <div className="border-t border-border pt-1" />}
          <p className="text-[11px] font-semibold uppercase tracking-widest text-zinc-600">
            In progress
          </p>
          {other.map(task => {
            const column = board.columns.find(c => c.id === task.currentColumnId);
            if (!column) return null;
            return (
              <TaskCard
                key={task.id}
                task={task}
                column={column}
                liveOutput={liveOutputs[task.id]}
                onRetry={onRetry}
                onClick={onSelectTask}
              />
            );
          })}
        </>
      )}
    </div>
  );
}
