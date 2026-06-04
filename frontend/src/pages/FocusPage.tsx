import type { Board, WorkTask } from '../types';
import TaskCard from '../components/task/TaskCard';

interface Props {
  board: Board;
  tasks: WorkTask[];
  liveOutputs: Record<string, string>;
  onRetry: (id: string) => void;
  onSelectTask: (id: string) => void;
}

export default function FocusPage({ board, tasks, liveOutputs, onRetry, onSelectTask }: Props) {
  const actionable = tasks.filter(t => t.status === 'Asking');

  if (actionable.length === 0) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-8">
        <div className="w-3 h-3 rounded-full bg-green-500 animate-pulse ring-2 ring-green-500/20 ring-offset-2 ring-offset-[var(--color-bg)]" />
        <p className="text-sm text-zinc-500 dark:text-zinc-400">All clear. The agents are working.</p>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-y-auto p-4 space-y-3 pb-20">
      <p className="text-[11px] font-semibold uppercase tracking-widest text-zinc-600 mb-1">
        {actionable.length} {actionable.length === 1 ? 'task needs' : 'tasks need'} your input
      </p>
      {actionable.map(task => {
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
    </div>
  );
}
