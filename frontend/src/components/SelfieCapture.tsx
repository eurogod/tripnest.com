import { useCallback, useEffect, useRef, useState } from 'react';
import { Button } from './ui';
import { Camera, Check } from './icons';

/**
 * Live camera selfie capture. Produces a JPEG File via the canvas, which the caller
 * uploads to obtain a stored path for Ghana Card face matching.
 */
export function SelfieCapture({ onCapture }: { onCapture: (file: File, dataUrl: string) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);

  const stop = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
  }, []);

  const start = useCallback(async () => {
    setError(null);
    setPreview(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: 640, height: 640 },
        audio: false,
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      setReady(true);
    } catch {
      setError('Camera access was blocked. Allow camera permission, or upload a photo instead.');
    }
  }, []);

  useEffect(() => {
    start();
    return stop;
  }, [start, stop]);

  const snap = () => {
    const video = videoRef.current;
    if (!video) return;
    const size = Math.min(video.videoWidth, video.videoHeight);
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    // Center-crop square + mirror back to natural orientation.
    const sx = (video.videoWidth - size) / 2;
    const sy = (video.videoHeight - size) / 2;
    ctx.drawImage(video, sx, sy, size, size, 0, 0, size, size);
    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        const dataUrl = canvas.toDataURL('image/jpeg', 0.9);
        const file = new File([blob], 'selfie.jpg', { type: 'image/jpeg' });
        setPreview(dataUrl);
        stop();
        setReady(false);
        onCapture(file, dataUrl);
      },
      'image/jpeg',
      0.9,
    );
  };

  const onUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      setPreview(dataUrl);
      stop();
      onCapture(file, dataUrl);
    };
    reader.readAsDataURL(file);
  };

  return (
    <div className="flex flex-col items-center">
      <div className="relative aspect-square w-64 overflow-hidden rounded-2xl border-2 border-dashed border-brand-600/40 bg-ink/5">
        {preview ? (
          <img src={preview} alt="Your selfie" className="h-full w-full object-cover" />
        ) : (
          <>
            <video ref={videoRef} playsInline muted className="h-full w-full -scale-x-100 object-cover" />
            <div className="pointer-events-none absolute inset-6 rounded-full border-2 border-white/70" />
          </>
        )}
        {!ready && !preview && !error && (
          <div className="absolute inset-0 grid place-items-center text-sm text-muted">Starting camera…</div>
        )}
      </div>

      {error && <p className="mt-3 max-w-xs text-center text-sm text-danger">{error}</p>}

      <div className="mt-4 flex gap-2">
        {preview ? (
          <Button variant="outline" onClick={start}>
            <Camera className="h-4 w-4" /> Retake
          </Button>
        ) : ready ? (
          <Button onClick={snap}>
            <Check className="h-4 w-4" /> Capture
          </Button>
        ) : null}
        <label className="btn-outline cursor-pointer">
          Upload instead
          <input type="file" accept="image/*" capture="user" className="hidden" onChange={onUpload} />
        </label>
      </div>
    </div>
  );
}
