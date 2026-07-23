import { useEffect, useRef, useState } from 'react';

type Phase = 'idle' | 'starting' | 'live' | 'captured' | 'error';

const BTN_OUTLINE =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink transition-colors hover:bg-gray-50 disabled:opacity-40';

/**
 * Live selfie capture for identity verification. The Ghana Card check runs a
 * server-side liveness test, so the selfie must come from the camera in the
 * moment — there is deliberately no file-upload fallback.
 */
export default function SelfieCapture({ onCapture }: {
  /** Receives the captured frame as a JPEG file; called again on retake. */
  onCapture: (file: File | null) => void;
}) {
  const [phase, setPhase] = useState<Phase>('idle');
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const stopStream = () => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
  };

  // Release the camera when the component unmounts mid-stream.
  useEffect(() => stopStream, []);

  const start = async () => {
    setError(null);
    setPhase('starting');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
        audio: false,
      });
      streamRef.current = stream;
      setPhase('live');
      // The video element renders once phase is 'live'; attach on next frame.
      requestAnimationFrame(() => {
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          void videoRef.current.play().catch(() => {});
        }
      });
    } catch {
      setPhase('error');
      setError(
        typeof navigator.mediaDevices?.getUserMedia === 'function'
          ? 'Camera access was blocked. Allow camera access in your browser and try again.'
          : 'This device has no camera available. Verification needs a live selfie.',
      );
    }
  };

  const capture = () => {
    const video = videoRef.current;
    if (!video || video.videoWidth === 0) return;
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d')?.drawImage(video, 0, 0);
    canvas.toBlob((blob) => {
      if (!blob) return;
      setPreview(canvas.toDataURL('image/jpeg', 0.9));
      onCapture(new File([blob], 'selfie.jpg', { type: 'image/jpeg' }));
      stopStream();
      setPhase('captured');
    }, 'image/jpeg', 0.9);
  };

  const retake = () => {
    setPreview(null);
    onCapture(null);
    void start();
  };

  return (
    <div className="space-y-3">
      {phase === 'live' || phase === 'starting' ? (
        <div className="w-full max-w-sm overflow-hidden rounded-2xl border border-gray-200 bg-black">
          {/* Mirrored preview feels natural; the captured frame stays unmirrored. */}
          <video
            ref={videoRef}
            playsInline
            muted
            className="aspect-[4/3] w-full -scale-x-100 object-cover"
          />
        </div>
      ) : preview ? (
        <img
          src={preview}
          alt="Your captured selfie"
          className="aspect-[4/3] w-full max-w-sm rounded-2xl border border-gray-200 object-cover"
        />
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        {(phase === 'idle' || phase === 'error') && (
          <button type="button" onClick={() => void start()} className={BTN_OUTLINE}>
            Open camera
          </button>
        )}
        {(phase === 'live' || phase === 'starting') && (
          <button type="button" onClick={capture} disabled={phase !== 'live'} className={BTN_OUTLINE}>
            Capture selfie
          </button>
        )}
        {phase === 'captured' && (
          <button type="button" onClick={retake} className={BTN_OUTLINE}>
            Retake
          </button>
        )}
        <span className="text-sm text-muted">
          {phase === 'captured'
            ? 'Looks good — this photo will be matched against your card.'
            : 'A live selfie, taken now, to match your card.'}
        </span>
      </div>
      {error && <p className="text-sm text-rose-600" role="alert">{error}</p>}
    </div>
  );
}
