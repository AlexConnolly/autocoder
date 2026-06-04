import { useEffect, useRef, useState } from 'react';

interface SpeechRecognitionResult {
  isFinal: boolean;
  [index: number]: { transcript: string };
}

interface SpeechRecognitionResultList {
  length: number;
  resultIndex: number;
  [index: number]: SpeechRecognitionResult;
}

interface SpeechRecognitionEvent {
  resultIndex: number;
  results: SpeechRecognitionResultList;
}

interface SpeechRecognitionErrorEvent {
  error: string;
}

interface SpeechRecognition {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  onresult: ((event: SpeechRecognitionEvent) => void) | null;
  onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
  onend: (() => void) | null;
  start: () => void;
  stop: () => void;
  abort: () => void;
}

interface SpeechRecognitionConstructor {
  new(): SpeechRecognition;
}

declare global {
  interface Window {
    SpeechRecognition?: SpeechRecognitionConstructor;
    webkitSpeechRecognition?: SpeechRecognitionConstructor;
  }
}

interface UseSpeechRecognitionOptions {
  onTranscript: (text: string, isFinal: boolean) => void;
  onError?: (error: string) => void;
  lang?: string;
}

interface UseSpeechRecognitionReturn {
  isListening: boolean;
  isSupported: boolean;
  start: () => void;
  stop: () => void;
}

const ERROR_MESSAGES: Record<string, string> = {
  'not-allowed': 'Microphone access denied.',
  'no-speech': 'No speech detected.',
  'network': 'Network error during speech recognition.',
  'audio-capture': 'No microphone found.',
  'aborted': '',
};

export function useSpeechRecognition({
  onTranscript,
  onError,
  lang,
}: UseSpeechRecognitionOptions): UseSpeechRecognitionReturn {
  const isSupported =
    typeof window !== 'undefined' &&
    !!(window.SpeechRecognition || window.webkitSpeechRecognition);

  const [isListening, setIsListening] = useState(false);
  const recognitionRef = useRef<SpeechRecognition | null>(null);
  const startingRef = useRef(false);
  const committedIndexRef = useRef(-1);
  const committedTextRef = useRef('');

  useEffect(() => {
    return () => {
      recognitionRef.current?.abort();
    };
  }, []);

  function start() {
    if (!isSupported || startingRef.current || isListening) return;

    const SR = window.SpeechRecognition ?? window.webkitSpeechRecognition;
    if (!SR) return;

    committedIndexRef.current = -1;
    committedTextRef.current = '';

    const recognition = new SR();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = lang ?? navigator.language ?? 'en-US';

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const result = event.results[i];
        if (result.isFinal) {
          // Chrome sometimes re-fires onresult with the same resultIndex for an
          // already-committed final result — skip it to prevent word duplication.
          if (i <= committedIndexRef.current) continue;
          committedIndexRef.current = i;
          committedTextRef.current += (committedTextRef.current ? ' ' : '') + result[0].transcript;
          onTranscript(result[0].transcript, true);
        } else {
          // Some engines include already-committed words in the interim transcript.
          // Strip that prefix so the displayed interim text doesn't duplicate the title.
          let interim = result[0].transcript;
          const committed = committedTextRef.current;
          if (committed && interim.startsWith(committed)) {
            interim = interim.slice(committed.length).trimStart();
          }
          onTranscript(interim, false);
        }
      }
    };

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      const msg = ERROR_MESSAGES[event.error] ?? `Speech error: ${event.error}`;
      startingRef.current = false;
      setIsListening(false);
      if (msg && onError) onError(msg);
    };

    recognition.onend = () => {
      startingRef.current = false;
      setIsListening(false);
    };

    recognitionRef.current = recognition;
    startingRef.current = true;
    recognition.start();
    setIsListening(true);
  }

  function stop() {
    recognitionRef.current?.stop();
  }

  return { isListening, isSupported, start, stop };
}
