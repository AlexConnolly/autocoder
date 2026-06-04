import { useEffect, useRef, useState } from 'react';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (title: string, description?: string) => void;
}

export default function CreateTaskModal({ isOpen, onClose, onCreate }: Props) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [showDesc, setShowDesc] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setTitle('');
      setDescription('');
      setShowDesc(false);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  const submit = () => {
    if (title.trim()) {
      onCreate(title.trim(), description.trim() || undefined);
      setTitle('');
      setDescription('');
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center sm:items-center p-4">
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/70" onClick={onClose} />

      {/* Modal */}
      <div className="relative bg-surface-raised border border-border rounded-xl w-full max-w-md shadow-2xl">
        <div className="p-5">
          <h2 className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 mb-4">New task</h2>

          <input
            ref={inputRef}
            value={title}
            onChange={e => setTitle(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && !e.shiftKey && submit()}
            placeholder="What needs to be done?"
            className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2.5 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition-colors"
          />

          {!showDesc ? (
            <button
              onClick={() => setShowDesc(true)}
              className="mt-2 text-xs text-zinc-500 hover:text-brand-400 transition-colors flex items-center gap-1"
            >
              <span>+</span> Add description
            </button>
          ) : (
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Optional description…"
              rows={3}
              className="mt-2 w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 resize-none transition-colors"
            />
          )}
        </div>

        <div className="flex items-center justify-between px-5 py-3 border-t border-border">
          <button
            onClick={onClose}
            className="text-sm text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={submit}
            disabled={!title.trim()}
            className="px-4 py-1.5 bg-brand-500 hover:bg-brand-600 text-white text-sm font-semibold rounded-lg disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Create →
          </button>
        </div>
      </div>
    </div>
  );
}
