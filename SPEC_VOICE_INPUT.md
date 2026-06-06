# Spec: Voice Input for Task Creation

## Overview

Add a microphone button to the "New task" modal that lets the user dictate a task description using their device microphone. Speech is transcribed to text in real time and placed into the title (and optionally the description) field. No backend changes are required; the feature uses the browser's built-in `SpeechRecognition` API.

The task title says "text to speech" but in this context the intent is clearly **speech-to-text** (the user speaks to describe the feature they want — the transcript fills the input).

---

## Affected Files

| File | Change |
|------|--------|
| `frontend/src/modals/CreateTaskModal.tsx` | Primary change — add mic button, recording state, and SpeechRecognition integration |
| `frontend/src/hooks/useSpeechRecognition.ts` | **New file** — encapsulate SpeechRecognition lifecycle |

No changes are required in `api/client.ts`, `types/index.ts`, `useBoard.ts`, `BoardPage.tsx`, or the backend.

---

## New Hook: `useSpeechRecognition.ts`

Path: `frontend/src/hooks/useSpeechRecognition.ts`

### Purpose

Isolate the `SpeechRecognition` browser API from the modal component. Keeps the component clean and makes the hook independently testable.

### Interface

```ts
interface UseSpeechRecognitionOptions {
  onTranscript: (text: string, isFinal: boolean) => void;
  onError?: (error: string) => void;
  lang?: string; // defaults to navigator.language or 'en-US'
}

interface UseSpeechRecognitionReturn {
  isListening: boolean;
  isSupported: boolean;
  start: () => void;
  stop: () => void;
}
```

### Behaviour

- `isSupported`: `true` if `window.SpeechRecognition || window.webkitSpeechRecognition` exists. When `false`, the mic button is hidden — no error is thrown.
- `start()`: Creates a new `SpeechRecognition` instance with `continuous: true` and `interimResults: true`, then calls `.start()`. Sets `isListening = true`.
- `stop()`: Calls `.stop()` on the instance. The final `onresult` event fires before `onend`; `isListening` is set to `false` in the `onend` handler.
- `onTranscript(text, isFinal)` is called on every `onresult` event. `isFinal` mirrors `results[i].isFinal`. Interim (non-final) results let the component show a live preview while the user is still speaking.
- Handles `onerror` by calling `onError` with a human-readable message and resetting `isListening`.
- The hook cleans up in a `useEffect` return: calls `.abort()` on any live instance to avoid leaked listeners.
- `continuous: true` keeps the session alive across natural pauses; the user explicitly stops by clicking the mic button again.

### Browser Compatibility Note

`SpeechRecognition` is supported in Chrome, Edge, and Safari 14.1+. Firefox does not support it without a flag as of the knowledge cutoff. The spec handles this gracefully by hiding the button when `isSupported` is false.

---

## Changes to `CreateTaskModal.tsx`

### New State

```ts
const [isListening, setIsListening] = useState(false);
const [interimTranscript, setInterimTranscript] = useState('');
```

`interimTranscript` stores the in-progress partial result returned by `isFinal: false` events. It is displayed as placeholder-style greyed text overlaid on (or appended after) the current title, so the user can see what is being captured before it is committed.

### `useSpeechRecognition` Integration

Call the hook at the top of the component:

```ts
const { isSupported, isListening, start, stop } = useSpeechRecognition({
  onTranscript: handleTranscript,
  onError: handleSpeechError,
});
```

#### `handleTranscript`

```ts
function handleTranscript(text: string, isFinal: boolean) {
  if (isFinal) {
    setTitle(prev => (prev ? prev + ' ' + text : text).trim());
    setInterimTranscript('');
  } else {
    setInterimTranscript(text);
  }
}
```

- Final results are appended to any text already in the title field (space-separated). This allows multiple utterances to accumulate naturally.
- Interim results only update `interimTranscript` — they do not mutate `title` yet.

#### `handleSpeechError`

```ts
function handleSpeechError(error: string) {
  setInterimTranscript('');
  // Show a small inline error for ~3 seconds, then clear
  setVoiceError(error);
  setTimeout(() => setVoiceError(''), 3000);
}
```

Add `const [voiceError, setVoiceError] = useState('')` to support this.

### Mic Button Placement

The microphone button sits at the **right end of the title input**. The title `<input>` needs `pr-9` (right padding) so the button does not overlap the text. The button is absolutely positioned inside a relative wrapper:

```tsx
<div className="relative">
  <input
    ref={inputRef}
    value={isListening ? title + (interimTranscript ? ' ' + interimTranscript : '') : title}
    onChange={e => !isListening && setTitle(e.target.value)}
    onKeyDown={e => e.key === 'Enter' && !e.shiftKey && submit()}
    placeholder={isListening ? 'Listening…' : 'What needs to be done?'}
    className="w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded-lg px-3 py-2.5 pr-9 text-sm ..."
    readOnly={isListening}
  />
  {isSupported && (
    <button
      type="button"
      onMouseDown={e => e.preventDefault()} // prevent focus steal
      onClick={isListening ? stop : start}
      className={`absolute right-2.5 top-1/2 -translate-y-1/2 p-0.5 rounded transition-colors
        ${isListening
          ? 'text-red-500 animate-pulse'
          : 'text-zinc-400 hover:text-brand-500'
        }`}
      aria-label={isListening ? 'Stop recording' : 'Start voice input'}
    >
      <MicIcon className="w-4 h-4" />
    </button>
  )}
</div>
```

**While recording (`isListening = true`)**:
- The input's displayed value shows `title + ' ' + interimTranscript` so the user sees live feedback.
- The input is `readOnly` to prevent keyboard editing mid-dictation (avoids cursor conflicts with live text insertion).
- The mic icon is red and pulses (`animate-pulse`).
- The placeholder changes to "Listening…".

**When recording stops**:
- `interimTranscript` is cleared.
- The final transcript has already been appended to `title` via `handleTranscript`.
- The input becomes editable again; the user can refine the text.

### Mic Icon

Use an inline SVG or import from an icon library already used in the project. Check whether `lucide-react` is already a dependency (likely given the component style); if so, use `import { Mic } from 'lucide-react'`. If not, inline a minimal mic SVG to avoid adding a dependency.

### Reset on Modal Close

In the `useEffect` for `isOpen`:

```ts
useEffect(() => {
  if (isOpen) {
    setTitle('');
    setDescription('');
    setShowDesc(false);
    setInterimTranscript('');
    setVoiceError('');
    setTimeout(() => inputRef.current?.focus(), 50);
  } else {
    stop(); // abort any in-progress recording when modal closes
  }
}, [isOpen]);
```

### Error Display

Below the title input (above the "+ Add description" button), render the error inline:

```tsx
{voiceError && (
  <p className="mt-1 text-xs text-red-400">{voiceError}</p>
)}
```

---

## UX / Interaction Design

| Scenario | Behaviour |
|----------|-----------|
| User clicks mic; speaks one sentence | Transcript appends to title on final result |
| User clicks mic; speaks multiple sentences | Each final result is space-appended; interim shows live |
| User clicks mic again to stop | Recording stops; final partial result is committed |
| User presses Escape while recording | Modal closes, `stop()` is called in the `useEffect` cleanup |
| Mic permission denied by browser | `onerror` fires with `'not-allowed'`; inline error shown |
| Browser doesn't support Speech API | Mic button is not rendered; no degraded experience |
| Title already has text when mic activated | New speech is appended with a leading space |

---

## Risks and Edge Cases

1. **Chrome speech session timeout (~60 s)**: `SpeechRecognition` with `continuous: true` may disconnect after ~60 seconds of continuous audio in Chrome. The `onend` event fires; the hook sets `isListening = false`. The user can tap the mic to start again. No special handling needed — the behaviour is acceptable.

2. **Mobile Safari**: `webkitSpeechRecognition` is available but requires explicit user gesture (the button click qualifies). Ensure `start()` is called directly in the click handler, not in a `setTimeout` or async callback, so it is treated as user-initiated.

3. **Multiple rapid clicks**: Use a `startingRef` boolean inside the hook to guard against calling `.start()` on an already-started instance, which throws a `DOMException`.

4. **Input `readOnly` during dictation**: Because the input is read-only, the user cannot type during recording. This is intentional to avoid cursor conflicts. The tradeoff is that they cannot correct errors mid-dictation — they stop recording first.

5. **Language**: Defaults to `navigator.language`. No UI language picker is in scope for this feature.

6. **Long titles**: SpeechRecognition returns continuous speech as multiple final results; all are appended to title. There is no hard limit enforced by this feature — the backend already accepts long strings.

7. **iOS Firefox / desktop Firefox**: `SpeechRecognition` is not supported. The button is hidden. No action needed.

---

## Out of Scope

- Backend transcription fallback (e.g., Whisper API) — browser API is sufficient.
- Populating the description field via voice — title field only in this spec; description voice input can be a follow-up.
- A separate "dictate" mode for the description textarea.
- Any changes to the task list, board, or backend APIs.
