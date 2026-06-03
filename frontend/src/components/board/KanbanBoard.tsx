import type { Board, WorkTask } from '../../types';
import KanbanColumn from './KanbanColumn';

interface Props {
  board: Board;
  tasks: WorkTask[];
  liveOutputs: Record<string, string>;
  onRetry: (id: string) => void;
  onSelectTask: (id: string) => void;
}

export default function KanbanBoard({ board, tasks, liveOutputs, onRetry, onSelectTask }: Props) {
  const sorted = [...board.columns].sort((a, b) => a.position - b.position);

  const byColumn = tasks.reduce<Record<string, WorkTask[]>>((acc, t) => {
    if (!acc[t.currentColumnId]) acc[t.currentColumnId] = [];
    acc[t.currentColumnId].push(t);
    return acc;
  }, {});

  return (
    <div className="flex h-full overflow-x-auto scrollbar-none">
      {sorted.map(col => (
        <KanbanColumn
          key={col.id}
          column={col}
          tasks={byColumn[col.id] ?? []}
          liveOutputs={liveOutputs}
          onRetry={onRetry}
          onSelectTask={onSelectTask}
        />
      ))}
    </div>
  );
}
