import { useEffect, useRef, useState } from 'react';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (title: string, description?: string) => void;
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

export default function CreateTaskModal({ isOpen, onClose, onCreate }: Props) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [showDesc, setShowDesc] = useState(false);
  const [interimTranscript, setInterimTranscript] = useState('');
  const [voiceError, setVoiceError] = useState('');
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

  useEffect(() => {
    if (isOpen) {
      setTitle('');
      setDescription('');
      setShowDesc(false);
      setInterimTranscript('');
      setVoiceError('');
      setTimeout(() => inputRef.current?.focus(), 50);
    } else {
      stop();
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

  const displayValue = isListening
    ? title + (interimTranscript ? ' ' + interimTranscript : '')
    : title;

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center sm:items-center p-4">
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/70" onClick={onClose} />

      {/* Modal */}
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
                  isListening
                    ? 'text-red-500 animate-pulse'
                    : 'text-zinc-400 hover:text-brand-500'
                }`}
                aria-label={isListening ? 'Stop recording' : 'Start voice input'}
              >
                <MicIcon className="w-4 h-4" />
              </button>
            )}
          </div>

          {voiceError && (
            <p className="mt-1 text-xs text-red-400">{voiceError}</p>
          )}

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
