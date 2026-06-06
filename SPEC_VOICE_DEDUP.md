# Spec: Fix Voice-to-Text Word Duplication

## Problem

The speech-to-text feature in the "New task" modal duplicates words. A user dictating "hello world how are you" may end up with "hello world hello world how are you" in the title field, or similar repeated fragments.

---

## Root Cause Analysis

### File: `frontend/src/hooks/useSpeechRecognition.ts`

The `onresult` handler loops from `event.resultIndex` to `event.results.length`:

```typescript
recognition.onresult = (event: SpeechRecognitionEvent) => {
  for (let i = event.resultIndex; i < event.results.length; i++) {
    const result = event.results[i];
    onTranscript(result[0].transcript, result.isFinal);
  }
};
```

### File: `frontend/src/modals/CreateTaskModal.tsx`

`handleTranscript` appends every final result to the title:

```typescript
function handleTranscript(text: string, isFinal: boolean) {
  if (isFinal) {
    setTitle(prev => (prev ? prev + ' ' + text : text).trim());
    setInterimTranscript('');
  } else {
    setInterimTranscript(text);
  }
}
```

### Bug 1 — Chrome re-delivers already-final results (primary cause)

Chrome's Web Speech API sometimes fires `onresult` multiple times with the same `event.resultIndex` pointing to a result already marked `isFinal: true`. The loop re-processes it, calling `onTranscript(text, true)` a second time. `handleTranscript` then appends the same text to `title` again.

Concrete example:

1. User says "hello world". Result index 0 becomes final.
   - `handleTranscript("hello world", true)` → `title = "hello world"`
2. Chrome fires a second `onresult` event with `resultIndex = 0` (same result, still final).
   - `handleTranscript("hello world", true)` fires again → `title = "hello world hello world"` ← **duplication**

This is a well-known Chrome/Chromium bug with the continuous Speech Recognition mode.

### Bug 2 — Interim transcript may overlap with committed title text

When `isFinal: false`, the hook calls `onTranscript(result[i][0].transcript, false)`, and the modal assembles the display value as:

```typescript
const displayValue = isListening
  ? title + (interimTranscript ? ' ' + interimTranscript : '')
  : title;
```

Some browser speech engines (particularly WebKit/Chrome's cloud-backend integration) occasionally send interim results whose text begins with or includes words from the previous utterance. For example:

- `title = "hello world"` (first utterance committed as final)
- Chrome sends interim for the second utterance: transcript = `"world how are"` (carries over a word)
- `displayValue = "hello world" + " world how are"` = `"hello world world how are"` ← **visual duplication**

When that interim result then becomes final, the bad text is committed permanently to `title`.

### Bug 3 — Loop iterates over stale earlier-final results in the same batch (edge case)

With `continuous: true`, Chrome can fire an `onresult` event containing multiple entries where earlier indices are already-final results and only the last index is new. Since `event.resultIndex` may equal an already-processed index, all already-processed results in the range `[event.resultIndex, results.length)` get re-fired as final. This is a less common but real variant of Bug 1.

---

## Affected Files

| File | Change |
|------|--------|
| `frontend/src/hooks/useSpeechRecognition.ts` | Add `committedIndexRef` guard; strip already-committed prefix from interim text |
| `frontend/src/modals/CreateTaskModal.tsx` | No change required once the hook is fixed |

---

## Proposed Fix

### Change 1 — `useSpeechRecognition.ts`: Guard with `committedIndexRef`

Add a ref that tracks the highest result index that has been committed as final. Reset it each time a new recognition session starts. Before calling `onTranscript` for a final result, check whether its index has already been committed and skip it if so.

```typescript
const committedIndexRef = useRef<number>(-1);
```

In `start()`, reset the ref before calling `recognition.start()`:

```typescript
committedIndexRef.current = -1;
```

Replace the `onresult` handler:

```typescript
recognition.onresult = (event: SpeechRecognitionEvent) => {
  for (let i = event.resultIndex; i < event.results.length; i++) {
    const result = event.results[i];
    if (result.isFinal) {
      if (i > committedIndexRef.current) {
        committedIndexRef.current = i;
        onTranscript(result[0].transcript, true);
      }
      // Already committed — skip. Prevents Chrome's duplicate-delivery bug.
    } else {
      onTranscript(result[0].transcript, false);
    }
  }
};
```

This is the minimal targeted fix for the primary cause. It is idempotent: no matter how many times Chrome re-delivers the same final result, only the first delivery is forwarded to the consumer.

### Change 2 — `useSpeechRecognition.ts`: Strip committed prefix from interim text

To defend against Bug 2 (interim text carrying over words from the committed portion), the hook should accumulate its own committed transcript string and remove any prefix overlap before forwarding interim text to the consumer.

Add a second ref:

```typescript
const committedTextRef = useRef<string>('');
```

Reset it in `start()`:

```typescript
committedTextRef.current = '';
```

When a final result is committed, append to it:

```typescript
if (i > committedIndexRef.current) {
  committedIndexRef.current = i;
  const text = result[0].transcript;
  committedTextRef.current += (committedTextRef.current ? ' ' : '') + text.trim();
  onTranscript(text, true);
}
```

When forwarding an interim result, strip any prefix that the committed text already covers:

```typescript
} else {
  const raw = result[0].transcript;
  const committed = committedTextRef.current;
  // If the interim starts with the committed text (overlap), trim it off.
  const trimmed = committed && raw.trimStart().startsWith(committed)
    ? raw.trimStart().slice(committed.length).trimStart()
    : raw;
  onTranscript(trimmed, false);
}
```

This keeps the interim preview consistent with what the modal displays (i.e. the interim only contains the NEW, not-yet-committed words).

**Note:** This prefix-stripping is a heuristic. It handles the common case where Chrome echoes the committed text as a prefix in the next interim result. It will not mangle results from browsers that send clean, non-overlapping interim transcripts.

---

## What Does NOT Need to Change

- `CreateTaskModal.tsx` — the `handleTranscript` function and `displayValue` logic are correct as long as the hook emits non-duplicate transcripts. No changes needed there.
- The hook's public API (`UseSpeechRecognitionReturn`, `UseSpeechRecognitionOptions`) stays unchanged — this is an internal implementation fix.
- No backend changes.
- No type changes in `types/index.ts`.

---

## Risks and Edge Cases

| Risk | Mitigation |
|------|------------|
| `committedIndexRef` reset on `start()` not `stop()` | Correct: the ref is session-scoped. Resetting on `start()` ensures a fresh session after the user stops and restarts recording. |
| Prefix-strip is too aggressive (false positive) | Only strips if `raw.trimStart()` starts with the entire `committedTextRef` string — it will NOT strip partial word matches. Edge case: if the user says the same sentence twice intentionally ("buy milk buy milk"), the second "buy milk" would appear in interim as `"buy milk buy milk"`, the prefix `"buy milk"` would be stripped, leaving `"buy milk"` for interim preview — which is actually correct behaviour. |
| Prefix-strip misses when capitalisation or punctuation differs | Chrome may capitalise or punctuate the interim differently from the final. The simple `startsWith` check could fail. Mitigation: lower-case both sides for the comparison but use the original `raw` after trimming by length. If the check fails, fall back to returning `raw` unmodified — worst case is a rare visual duplication on interim preview only, not committed text duplication. |
| `committedIndexRef` is not shared across multiple hook instances | Each hook call creates its own ref, which is correct — there is only one `useSpeechRecognition` instance per modal. |
| Chrome 60-second session timeout restarts recognition | When Chrome fires `onend`, `isListening` is set to false and `recognitionRef.current` is the old instance. On the next `start()` call, a fresh instance is created and `committedIndexRef`/`committedTextRef` are reset. This is correct. |

---

## Out of Scope

- Re-enabling mid-dictation keyboard editing (the `readOnly` approach is intentional).
- Language selector UI.
- Dictating into the description field.
- Replacing the Web Speech API with a server-side Whisper transcription fallback.
