import { useEffect, useState } from 'react';
import * as api from '../../api/client';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const BOARD_ID = '10000000-0000-0000-0000-000000000001';

export default function BoardTab() {
  const [form, setForm] = useState({ name: '', globalInstructions: '', maxInProgress: '' });
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (USE_MOCK) {
      setForm({ name: 'Main Project', globalInstructions: "Always follow existing code conventions. Write TypeScript. Use the project's existing test framework.", maxInProgress: '' });
      return;
    }
    api.fetchBoard(BOARD_ID)
      .then(b => setForm({
        name: b.name,
        globalInstructions: b.globalInstructions ?? '',
        maxInProgress: b.maxInProgress != null ? String(b.maxInProgress) : '',
      }))
      .catch(console.error);
  }, []);

  const save = async () => {
    setSaving(true);
    try {
      const maxInProgress = form.maxInProgress.trim() === '' ? null : parseInt(form.maxInProgress, 10);
      if (!USE_MOCK) await api.updateBoard(BOARD_ID, form.name, form.globalInstructions, maxInProgress);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (err) {
      console.error(err);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-lg">
      <div>
        <label className="block text-[11px] text-zinc-500 uppercase tracking-widest mb-2">Board name</label>
        <input
          value={form.name}
          onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
          className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:border-zinc-500 transition-colors"
        />
      </div>

      <div>
        <label className="block text-[11px] text-zinc-500 uppercase tracking-widest mb-2">Global instructions</label>
        <p className="text-[11px] text-zinc-600 mb-2">Prepended to every agent prompt on this board.</p>
        <textarea
          value={form.globalInstructions}
          onChange={e => setForm(p => ({ ...p, globalInstructions: e.target.value }))}
          rows={4}
          className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-zinc-500 resize-none transition-colors"
        />
      </div>

      <div>
        <label className="block text-[11px] text-zinc-500 uppercase tracking-widest mb-2">Max in progress</label>
        <p className="text-[11px] text-zinc-600 mb-2">
          Maximum number of tasks allowed in the pipeline at once (all columns except the first and last). Leave blank for no limit.
        </p>
        <input
          type="number"
          min="1"
          value={form.maxInProgress}
          onChange={e => setForm(p => ({ ...p, maxInProgress: e.target.value }))}
          placeholder="No limit"
          className="w-32 bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-zinc-500 transition-colors"
        />
      </div>

      <div className="pt-2">
        <button
          onClick={save}
          disabled={saving}
          className="px-4 py-2 bg-zinc-800 text-zinc-100 dark:bg-zinc-100 dark:text-zinc-900 text-sm font-semibold rounded-lg hover:bg-zinc-700 dark:hover:bg-white transition-colors disabled:opacity-50"
        >
          {saved ? '✓ Saved' : saving ? 'Saving…' : 'Save changes'}
        </button>
      </div>
    </div>
  );
}
