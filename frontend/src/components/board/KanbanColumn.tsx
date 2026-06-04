import type { Column, WorkTask } from '../../types';
import { cn } from '../../utils/cn';
import TaskCard from '../task/TaskCard';

const STATUS_ORDER: Record<string, number> = {
  Asking: 0, Error: 1, Running: 2, Waiting: 3, PendingApproval: 3, Done: 4,
};

function sortedTasks(tasks: WorkTask[]): WorkTask[] {
  return [...tasks].sort((a, b) => (STATUS_ORDER[a.status] ?? 9) - (STATUS_ORDER[b.status] ?? 9));
}

interface Props {
  column: Column;
  tasks: WorkTask[];
  liveOutputs: Record<string, string>;
  onRetry: (id: string) => void;
  onSelectTask: (id: string) => void;
}

export default function KanbanColumn({ column, tasks, liveOutputs, onRetry, onSelectTask }: Props) {
  const isRunning = tasks.some(t => t.status === 'Running');
  const sorted = sortedTasks(tasks);

  return (
    <div className="flex-none w-72 flex flex-col h-full border-r border-border last:border-r-0">
      <div className="sticky top-0 z-10 px-3 py-2.5 border-b border-border bg-[var(--color-bg)] flex items-center gap-2">
        <span className="text-[11px] font-semibold tracking-widest uppercase text-zinc-500 dark:text-zinc-400 flex-1 truncate">
          {column.name}
        </span>
        <span className={cn(
          'text-[10px] px-1.5 rounded-full bg-zinc-200 dark:bg-zinc-800 text-zinc-500',
          tasks.length === 0 && 'opacity-0'
        )}>
          {tasks.length}
        </span>
        {isRunning && (
          <span className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" title="Agent running" />
        )}
      </div>

      <div className="flex-1 overflow-y-auto p-2 space-y-2">
        {sorted.map(task => (
          <TaskCard
            key={task.id}
            task={task}
            column={column}
            liveOutput={liveOutputs[task.id]}
            onRetry={onRetry}
            onClick={onSelectTask}
          />
        ))}
        {tasks.length === 0 && (
          <div className="h-24 flex items-center justify-center">
            <span className="text-[12px] text-zinc-400 dark:text-zinc-700">No tasks</span>
          </div>
        )}
      </div>
    </div>
  );
}
