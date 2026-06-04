import type { Column } from '../../types';
import { cn } from '../../utils/cn';

interface Props {
  columns: Column[];
  activeId: string | null;
  onSelect: (id: string | null) => void;
}

export default function MobileColumnFilter({ columns, activeId, onSelect }: Props) {
  const sorted = [...columns].sort((a, b) => a.position - b.position);
  return (
    <div className="flex gap-2 overflow-x-auto scrollbar-none px-4 py-2 border-b border-border">
      <button
        onClick={() => onSelect(null)}
        className={cn(
          'flex-none text-xs px-3 py-1.5 rounded-full border transition-colors',
          activeId === null
            ? 'border-zinc-600 text-zinc-800 dark:text-zinc-200 bg-zinc-200 dark:bg-zinc-800'
            : 'border-zinc-300 dark:border-zinc-700 text-zinc-500 hover:border-zinc-400 dark:hover:border-zinc-500'
        )}
      >
        All
      </button>
      {sorted.map(col => (
        <button
          key={col.id}
          onClick={() => onSelect(col.id)}
          className={cn(
            'flex-none text-xs px-3 py-1.5 rounded-full border transition-colors whitespace-nowrap',
            activeId === col.id
              ? 'border-zinc-600 text-zinc-800 dark:text-zinc-200 bg-zinc-200 dark:bg-zinc-800'
              : 'border-zinc-300 dark:border-zinc-700 text-zinc-500 hover:border-zinc-400 dark:hover:border-zinc-500'
          )}
        >
          {col.name}
        </button>
      ))}
    </div>
  );
}
