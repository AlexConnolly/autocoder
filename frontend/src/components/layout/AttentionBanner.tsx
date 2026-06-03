import type { Column, WorkTask } from '../../types';

interface Props {
  tasks: WorkTask[];
  columns: Column[];
  onSelectTask: (id: string) => void;
}

function LightningIcon() {
  return (
    <svg className="w-4 h-4 flex-none" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M9.5 1.5L4 9h5l-1.5 5.5 6-8H8.5l1-5z" stroke="currentColor" strokeWidth="1.25" strokeLinejoin="round" fill="none" />
    </svg>
  );
}

export default function AttentionBanner({ tasks, columns, onSelectTask }: Props) {
  const actionable = tasks.filter(t => t.status === 'Asking');
  if (actionable.length === 0) return null;

  const colName = (colId: string) => columns.find(c => c.id === colId)?.name ?? '';

  return (
    <div className="flex items-center gap-3 px-4 py-2 bg-amber-500/8 border-b border-amber-500/20 text-sm flex-wrap">
      <span className="text-amber-400 font-medium flex-none flex items-center gap-1.5">
        <LightningIcon />
        {actionable.length} {actionable.length === 1 ? 'task needs' : 'tasks need'} your input
      </span>
      <span className="text-amber-800/80">·</span>
      <div className="flex flex-wrap gap-2">
        {actionable.map(t => (
          <button
            key={t.id}
            onClick={() => onSelectTask(t.id)}
            className="text-amber-300 hover:text-amber-100 bg-amber-500/10 hover:bg-amber-500/20 rounded px-2 py-0.5 text-sm transition-colors"
          >
            {t.title}
            <span className="text-amber-600 text-xs ml-1">({colName(t.currentColumnId)})</span>
          </button>
        ))}
      </div>
    </div>
  );
}
