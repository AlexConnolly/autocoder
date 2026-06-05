import { useEffect, useState } from 'react';
import * as api from '../../api/client';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const BOARD_ID = '10000000-0000-0000-0000-000000000001';

export default function BoardTab() {
  const [form, setForm] = useState({ name: '', globalInstructions: '', maxInProgress: '', cavemanMode: false });
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (USE_MOCK) {
      setForm({ name: 'Main Project', globalInstructions: "Always follow existing code conventions. Write TypeScript. Use the project's existing test framework.", maxInProgress: '', cavemanMode: false });
      return;
    }
    api.fetchBoard(BOARD_ID)
      .then(b => setForm({
        name: b.name,
        globalInstructions: b.globalInstructions ?? '',
        maxInProgress: b.maxInProgress != null ? String(b.maxInProgress) : '',
        cavemanMode: b.cavemanMode,
      }))
      .catch(console.error);
  }, []);

  const save = async () => {
    setSaving(true);
    try {
      const maxInProgress = form.maxInProgress.trim() === '' ? null : parseInt(form.maxInProgress, 10);
      if (!USE_MOCK) await api.updateBoard(BOARD_ID, form.name, form.globalInstructions, maxInProgress, form.cavemanMode);
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

      <div>
        <label className="block text-[11px] text-zinc-500 uppercase tracking-widest mb-2">Caveman mode</label>
        <p className="text-[11px] text-zinc-600 mb-2">
          Appends a terse-output system prompt to every agent run, reducing context usage.
        </p>
        <button
          type="button"
          role="switch"
          aria-checked={form.cavemanMode}
          onClick={() => setForm(p => ({ ...p, cavemanMode: !p.cavemanMode }))}
          className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none ${form.cavemanMode ? 'bg-zinc-800 dark:bg-zinc-100' : 'bg-zinc-300 dark:bg-zinc-600'}`}
        >
          <span
            className={`inline-block h-4 w-4 transform rounded-full bg-white dark:bg-zinc-900 transition-transform ${form.cavemanMode ? 'translate-x-6' : 'translate-x-1'}`}
          />
        </button>
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
