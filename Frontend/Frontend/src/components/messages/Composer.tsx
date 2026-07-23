import { useEffect, useRef, useState } from 'react';
import { aiErrorMessage } from '../../api/assistant';
import { ArrowUpIcon, MicIcon, PaperclipIcon, SparkleIcon, TrashIcon } from '../tenant/icons';
import { clock } from './time';

interface ComposerProps {
  /** Caller owns the optimistic append + API call; resolve false to restore the draft for retry. */
  onSendText: (text: string) => Promise<boolean>;
  onSendVoice: (audioUrl: string, duration: number) => void;
  onAttach: (file: File) => void;
  /** Surface transient problems (unsupported recording, mic denied…). */
  onNotice: (message: string) => void;
  /** When provided, an AI button fills the draft with a suggested reply to edit. */
  suggest?: () => Promise<string>;
  /** Fired as the user types, for live typing indicators. */
  onTyping?: () => void;
}

/** Message composer: text draft, file attach, and voice-note recording. */
export default function Composer({ onSendText, onSendVoice, onAttach, onNotice, suggest, onTyping }: ComposerProps) {
  const [draft, setDraft] = useState('');
  const [recording, setRecording] = useState(false);
  const [suggesting, setSuggesting] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<BlobPart[]>([]);
  const timerRef = useRef<number | null>(null);
  const startedAtRef = useRef(0);
  const discardRef = useRef(false);

  // Stop any in-flight recording on unmount / thread switch.
  useEffect(() => {
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
      recorderRef.current?.stream.getTracks().forEach((t) => t.stop());
    };
  }, []);

  const send = async () => {
    const text = draft.trim();
    if (!text) return;
    setDraft('');
    const ok = await onSendText(text);
    // Give the user their message back to retry.
    if (!ok) setDraft((d) => d || text);
  };

  const attach = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) onAttach(file);
    e.target.value = '';
  };

  // Fill the draft with an AI suggestion for the user to edit — never auto-sent.
  const runSuggest = async () => {
    if (!suggest || suggesting) return;
    setSuggesting(true);
    try {
      setDraft(await suggest());
      inputRef.current?.focus();
    } catch (err) {
      onNotice(aiErrorMessage(err));
    } finally {
      setSuggesting(false);
    }
  };

  // Stop the recorder; `discard` decides whether the clip is sent or dropped.
  const finishRecording = (discard: boolean) => {
    discardRef.current = discard;
    recorderRef.current?.stop();
  };

  const startRecording = async () => {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      onNotice('Recording is not supported on this device.');
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];
      discardRef.current = false;
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunksRef.current.push(e.data);
      };
      recorder.onstop = () => {
        const seconds = (Date.now() - startedAtRef.current) / 1000;
        if (!discardRef.current && chunksRef.current.length > 0) {
          const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' });
          onSendVoice(URL.createObjectURL(blob), seconds);
        }
        stream.getTracks().forEach((t) => t.stop());
        if (timerRef.current) clearInterval(timerRef.current);
        setRecording(false);
        setElapsed(0);
      };
      recorderRef.current = recorder;
      startedAtRef.current = Date.now();
      recorder.start();
      setRecording(true);
      setElapsed(0);
      timerRef.current = window.setInterval(
        () => setElapsed((Date.now() - startedAtRef.current) / 1000),
        200,
      );
    } catch {
      onNotice('Microphone permission denied.');
    }
  };

  return (
    <div className="border-t border-gray-100 p-3">
      <div className="mx-auto max-w-2xl rounded-2xl border border-gray-200 bg-white px-3 py-2 transition-colors focus-within:border-brand">
        {recording ? (
          <div className="flex items-center gap-3 px-1 py-1.5">
            <button
              type="button"
              onClick={() => finishRecording(true)}
              aria-label="Discard recording"
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-muted hover:bg-gray-100 hover:text-rose-600"
            >
              <TrashIcon size={17} />
            </button>
            <span className="h-2.5 w-2.5 shrink-0 animate-pulse rounded-full bg-rose-500" />
            <span className="text-sm font-medium tabular-nums text-ink">{clock(elapsed)}</span>
            <span className="text-sm text-muted">Recording…</span>
            <div className="flex-1" />
            <button
              type="button"
              onClick={() => finishRecording(false)}
              aria-label="Send voice message"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand text-white hover:bg-brand/90"
            >
              <ArrowUpIcon size={18} />
            </button>
          </div>
        ) : (
          <>
            <input
              ref={inputRef}
              value={draft}
              onChange={(e) => { setDraft(e.target.value); if (e.target.value) onTyping?.(); }}
              onKeyDown={(e) => { if (e.key === 'Enter') void send(); }}
              placeholder="Type a message…"
              className="w-full bg-transparent px-1 py-1.5 text-sm text-ink outline-none placeholder:text-muted"
            />
            <div className="mt-1 flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <button
                  type="button"
                  onClick={() => fileRef.current?.click()}
                  aria-label="Attach a file"
                  className="flex h-8 w-8 items-center justify-center rounded-full text-muted hover:bg-gray-100 hover:text-ink"
                >
                  <PaperclipIcon size={17} />
                </button>
                {suggest && (
                  <button
                    type="button"
                    onClick={() => void runSuggest()}
                    disabled={suggesting || Boolean(draft.trim())}
                    aria-label="Suggest a reply"
                    title={draft.trim() ? 'Clear your draft to get a suggestion' : 'Suggest a reply'}
                    className="flex h-8 w-8 items-center justify-center rounded-full text-muted hover:bg-gray-100 hover:text-brand disabled:pointer-events-none disabled:opacity-40"
                  >
                    <SparkleIcon size={17} className={suggesting ? 'animate-pulse text-brand' : undefined} />
                  </button>
                )}
              </div>
              <input ref={fileRef} type="file" className="hidden" onChange={attach} />

              <div className="flex items-center gap-1.5">
                {draft.trim() ? (
                  <button
                    type="button"
                    onClick={() => void send()}
                    aria-label="Send message"
                    className="flex h-9 w-9 items-center justify-center rounded-full bg-brand text-white transition-opacity hover:bg-brand/90"
                  >
                    <ArrowUpIcon size={18} />
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => void startRecording()}
                    aria-label="Record a voice message"
                    className="flex h-9 w-9 items-center justify-center rounded-full text-muted transition-colors hover:bg-gray-100 hover:text-ink"
                  >
                    <MicIcon size={18} />
                  </button>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
