import { useEffect, useRef, useState } from 'react';
import * as api from '../api/client';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import type { BoardRepository, TaskRepositoryConfig } from '../types';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (title: string, description: string, repositories?: TaskRepositoryConfig[]) => void;
  repositories?: BoardRepository[];
}

function MicIcon({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
      <rect x="9" y="2" width="6" height="12" rx="3" />
      <path d="M5 10a7 7 0 0 0 14 0" />
      <line x1="12" y1="19" x2="12" y2="22" />
      <line x1="9" y1="22" x2="15" y2="22" />
    </svg>
  );
}

function slugify(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 50)
    .replace(/-$/, '');
}

interface RepoState {
  branchName: string;
  isEnabled: boolean;
  suggestions: string[];
  showSuggestions: boolean;
}

export default function CreateTaskModal({ isOpen, onClose, onCreate, repositories = [] }: Props) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [interimTranscript, setInterimTranscript] = useState('');
  const [voiceError, setVoiceError] = useState('');
  const [repoStates, setRepoStates] = useState<Record<string, RepoState>>({});
  const inputRef = useRef<HTMLInputElement>(null);

  function handleTranscript(text: string, isFinal: boolean) {
    if (isFinal) {
      setTitle(prev => (prev ? prev + ' ' + text : text).trim());
      setInterimTranscript('');
    } else {
      setInterimTranscript(text);
    }
  }

  function handleSpeechError(error: string) {
    setInterimTranscript('');
    setVoiceError(error);
    setTimeout(() => setVoiceError(''), 3000);
  }

  const { isSupported, isListening, start, stop } = useSpeechRecognition({
    onTranscript: handleTranscript,
    onError: handleSpeechError,
  });

  // Initialise repo states and fetch branch suggestions when modal opens
  useEffect(() => {
    if (!isOpen) { stop(); return; }

    setTitle('');
    setDescription('');
    setInterimTranscript('');
    setVoiceError('');
    setTimeout(() => inputRef.current?.focus(), 50);

    const initial: Record<string, RepoState> = {};
    repositories.forEach(r => {
      initial[r.id] = { branchName: '', isEnabled: true, suggestions: [], showSuggestions: false };
    });
    setRepoStates(initial);

    repositories.forEach(r => {
      api.fetchRepoBranches(r.id)
        .then(branches => setRepoStates(prev => ({
          ...prev,
          [r.id]: { ...prev[r.id], suggestions: branches },
        })))
        .catch(() => {});
    });
  }, [isOpen]);

  // Update branch names when title changes
  useEffect(() => {
    const slug = slugify(title);
    setRepoStates(prev => {
      const next = { ...prev };
      repositories.forEach(r => {
        const state = prev[r.id];
        if (!state) return;
        // Only auto-update if it still matches the auto-generated pattern or is empty
        const wasAuto = state.branchName === '' || state.branchName.startsWith('auto/');
        if (wasAuto) {
          next[r.id] = { ...state, branchName: slug ? `auto/${slug}` : '' };
        }
      });
      return next;
    });
  }, [title]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  const submit = () => {
    if (!title.trim() || !description.trim()) return;

    const repos: TaskRepositoryConfig[] = repositories.map(r => {
      const state = repoStates[r.id];
      return {
        repositoryId: r.id,
        branchName: state?.branchName || `auto/${slugify(title.trim())}`,
        isEnabled: state?.isEnabled ?? true,
      };
    });

    onCreate(title.trim(), description.trim(), repos.length > 0 ? repos : undefined);
    setTitle('');
    setDescription('');
  };

  const displayValue = isListening
    ? title + (interimTranscript ? ' ' + interimTranscript : '')
    : title;

  const updateRepo = (id: string, patch: Partial<RepoState>) =>
    setRepoStates(prev => ({ ...prev, [id]: { ...prev[id], ...patch } }));

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center sm:items-center p-4">
      <div className="fixed inset-0 bg-black/70" onClick={onClose} />

      <div className="relative bg-surface-raised border border-border rounded-xl w-full max-w-md shadow-2xl">
        <div className="p-5">
          <h2 className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 mb-4">New task</h2>

          <div className="relative">
            <input
              ref={inputRef}
              value={displayValue}
              onChange={e => !isListening && setTitle(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && !e.shiftKey && submit()}
              placeholder={isListening ? 'Listening…' : 'What needs to be done?'}
              readOnly={isListening}
              className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2.5 pr-9 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition-colors"
            />
            {isSupported && (
              <button
                type="button"
                onMouseDown={e => e.preventDefault()}
                onClick={isListening ? stop : start}
                className={`absolute right-2.5 top-1/2 -translate-y-1/2 p-0.5 rounded transition-colors ${
                  isListening ? 'text-red-500 animate-pulse' : 'text-zinc-400 hover:text-brand-500'
                }`}
                aria-label={isListening ? 'Stop recording' : 'Start voice input'}
              >
                <MicIcon className="w-4 h-4" />
              </button>
            )}
          </div>

          {voiceError && <p className="mt-1 text-xs text-red-400">{voiceError}</p>}

          <textarea
            value={description}
            onChange={e => setDescription(e.target.value)}
            placeholder="Description…"
            rows={3}
            className="mt-2 w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 resize-none transition-colors"
          />

          {repositories.length > 0 && (
            <div className="mt-4 space-y-3">
              <p className="text-[11px] font-semibold uppercase tracking-wider text-zinc-500">Branches</p>
              {repositories.map(repo => {
                const state = repoStates[repo.id];
                if (!state) return null;
                const filtered = state.suggestions.filter(s =>
                  s.toLowerCase().includes(state.branchName.toLowerCase())
                );
                return (
                  <div key={repo.id} className="flex items-center gap-2">
                    {/* Enable toggle */}
                    <button
                      type="button"
                      onClick={() => updateRepo(repo.id, { isEnabled: !state.isEnabled })}
                      className={`w-8 h-4 rounded-full flex-none transition-colors relative ${
                        state.isEnabled ? 'bg-brand-500' : 'bg-zinc-300 dark:bg-zinc-700'
                      }`}
                      title={state.isEnabled ? 'Disable repo' : 'Enable repo'}
                    >
                      <span className={`absolute top-0.5 w-3 h-3 bg-white rounded-full shadow transition-all ${
                        state.isEnabled ? 'left-4.5' : 'left-0.5'
                      }`} />
                    </button>

                    {/* Repo name */}
                    <span className="text-xs text-zinc-500 w-24 flex-none truncate" title={repo.name}>
                      {repo.name}
                    </span>

                    {/* Branch name input with autocomplete */}
                    <div className="relative flex-1">
                      <input
                        type="text"
                        value={state.branchName}
                        onChange={e => updateRepo(repo.id, { branchName: e.target.value, showSuggestions: true })}
                        onFocus={() => updateRepo(repo.id, { showSuggestions: true })}
                        onBlur={() => setTimeout(() => updateRepo(repo.id, { showSuggestions: false }), 150)}
                        disabled={!state.isEnabled}
                        placeholder={`auto/${slugify(title) || 'branch-name'}`}
                        className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded px-2 py-1 text-xs font-mono text-zinc-800 dark:text-zinc-200 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-brand-500 disabled:opacity-40 transition-colors"
                      />
                      {state.showSuggestions && filtered.length > 0 && (
                        <ul className="absolute z-50 top-full mt-1 left-0 right-0 bg-surface-raised border border-border rounded shadow-lg max-h-32 overflow-y-auto">
                          {filtered.slice(0, 8).map(s => (
                            <li
                              key={s}
                              onMouseDown={() => updateRepo(repo.id, { branchName: s, showSuggestions: false })}
                              className="px-2 py-1 text-xs font-mono text-zinc-700 dark:text-zinc-300 hover:bg-brand-500/10 cursor-pointer"
                            >
                              {s}
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
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
            disabled={!title.trim() || !description.trim()}
            className="px-4 py-1.5 bg-brand-500 hover:bg-brand-600 text-white text-sm font-semibold rounded-lg disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Create →
          </button>
        </div>
      </div>
    </div>
  );
}
