import { useEffect, useRef, useState } from 'react';
import { getSignatureInfo, uploadSignature, type SignatureInfo } from '../../api/profile';
import { useT } from '../../lib/i18n';
import Card from '../ui/Card';
import Button from '../ui/Button';
import { PencilIcon, TrashIcon } from '../tenant/icons';

/** Bitmap resolution of the pad. The element is displayed responsively, so pointer
 *  coordinates are scaled from client pixels into this space when drawing. */
const CANVAS_W = 600;
const CANVAS_H = 200;

/**
 * The user's signature — what gets stamped onto agreements when they sign. The user draws it
 * here and can preview it before saving; the drawing is exported as a transparent PNG and sent
 * to POST /api/profile/signature (multipart), the same endpoint an uploaded image used.
 *
 * The first save is free; replacing it needs the account password (+ Ghana Card number once
 * verified) and sits behind a 30-day cooldown, all enforced server-side.
 */
export default function SignatureCard() {
  const [info, setInfo] = useState<SignatureInfo | null>(null);
  const [password, setPassword] = useState('');
  const [ghanaCard, setGhanaCard] = useState('');
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [hasDrawn, setHasDrawn] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  // Render-stable "now" for the cooldown comparison (impure calls can't run in render).
  const [mountedAt] = useState(() => Date.now());
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const t = useT();

  useEffect(() => {
    getSignatureInfo().then(setInfo).catch(() => setInfo(null));
  }, []);

  // Stroke style is set once — 2d context state persists until the bitmap is resized.
  useEffect(() => {
    const ctx = canvasRef.current?.getContext('2d');
    if (!ctx) return;
    ctx.lineWidth = 2.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#08060d';
  }, []);

  const replacing = Boolean(info?.hasSignature);
  const cooldownActive =
    replacing && info?.editableFrom != null && new Date(info.editableFrom).getTime() > mountedAt;

  /** Pointer position in bitmap coordinates (the canvas is scaled by CSS). */
  const pointAt = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = e.currentTarget;
    const rect = canvas.getBoundingClientRect();
    return {
      x: (e.clientX - rect.left) * (canvas.width / rect.width),
      y: (e.clientY - rect.top) * (canvas.height / rect.height),
    };
  };

  const startStroke = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const ctx = e.currentTarget.getContext('2d');
    if (!ctx) return;
    // Capture so a stroke keeps tracking even if the pointer leaves the pad mid-draw.
    e.currentTarget.setPointerCapture(e.pointerId);
    const { x, y } = pointAt(e);
    ctx.beginPath();
    ctx.moveTo(x, y);
    drawing.current = true;
  };

  const extendStroke = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return;
    const ctx = e.currentTarget.getContext('2d');
    if (!ctx) return;
    const { x, y } = pointAt(e);
    ctx.lineTo(x, y);
    ctx.stroke();
    if (!hasDrawn) setHasDrawn(true);
    // A fresh stroke invalidates any preview taken earlier.
    if (previewUrl) setPreviewUrl(null);
  };

  const endStroke = () => {
    drawing.current = false;
  };

  const clearPad = () => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (canvas && ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
    setHasDrawn(false);
    setPreviewUrl(null);
  };

  /** Render exactly what will be uploaded, so the user can check it before saving. */
  const showPreview = () => {
    const canvas = canvasRef.current;
    if (canvas) setPreviewUrl(canvas.toDataURL('image/png'));
  };

  const toPngFile = () =>
    new Promise<File | null>((resolve) => {
      const canvas = canvasRef.current;
      if (!canvas) return resolve(null);
      canvas.toBlob(
        (blob) => resolve(blob ? new File([blob], 'signature.png', { type: 'image/png' }) : null),
        'image/png',
      );
    });

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!hasDrawn || busy) return;
    setBusy(true);
    setNote(null);
    try {
      const file = await toPngFile();
      if (!file) throw new Error('Could not read the drawing.');
      await uploadSignature(file, password || undefined, ghanaCard || undefined);
      setNote(replacing ? 'Signature replaced.' : 'Signature saved — you can now sign agreements.');
      setPassword('');
      setGhanaCard('');
      clearPad();
      setInfo(await getSignatureInfo().catch(() => info));
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not save the signature.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="p-6">
      <h2 className="text-lg font-bold text-ink">{t('Signature')}</h2>
      <p className="mt-1 text-sm text-muted">
        {replacing
          ? `On file since ${info?.updatedAt ? new Date(info.updatedAt).toLocaleDateString() : '—'}. Used when you sign agreements.`
          : t('Draw your signature below — it’s applied to agreements when you sign.')}
      </p>

      {cooldownActive ? (
        <p className="mt-3 text-sm text-muted">
          For security, your signature can be changed again from{' '}
          {new Date(info!.editableFrom!).toLocaleDateString()}.
        </p>
      ) : (
        <form onSubmit={submit} className="mt-3 space-y-3">
          {/* Draw area — click/tap and drag to sign. */}
          <div className="relative">
            <canvas
              ref={canvasRef}
              width={CANVAS_W}
              height={CANVAS_H}
              onPointerDown={startStroke}
              onPointerMove={extendStroke}
              onPointerUp={endStroke}
              onPointerCancel={endStroke}
              className="tn-glow h-40 w-full touch-none cursor-crosshair rounded-xl border-2 border-dashed border-brand/40 bg-white hover:border-brand"
              aria-label={t('Signature drawing area')}
            />
            {!hasDrawn && (
              <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-1">
                <PencilIcon size={28} className="text-brand" />
                <span className="text-sm font-semibold text-ink">
                  {t('Click and draw your signature')}
                </span>
                <span className="text-xs text-muted">
                  {t('Use your mouse, trackpad or finger')}
                </span>
              </div>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={clearPad}
              disabled={!hasDrawn || busy}
            >
              <TrashIcon size={16} className="mr-1.5" />
              {t('Clear')}
            </Button>
            <Button
              type="button"
              variant="dark"
              size="sm"
              onClick={showPreview}
              disabled={!hasDrawn || busy}
            >
              {t('Preview')}
            </Button>
            <Button size="sm" disabled={!hasDrawn || busy}>
              {busy ? 'Saving…' : replacing ? 'Replace signature' : 'Save signature'}
            </Button>
          </div>

          {/* Preview area — the exact PNG that will be uploaded. */}
          {previewUrl && (
            <div className="rounded-xl border border-gray-200 bg-gray-50 p-3">
              <p className="mb-2 text-xs font-semibold text-muted">
                {t('Preview — this is exactly what will be saved')}
              </p>
              <img
                src={previewUrl}
                alt={t('Signature preview')}
                className="h-24 w-full object-contain"
              />
            </div>
          )}

          {replacing && (
            <>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Account password (required to replace)"
                required
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
              />
              <input
                value={ghanaCard}
                onChange={(e) => setGhanaCard(e.target.value)}
                placeholder="Ghana Card number (if identity-verified)"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
              />
            </>
          )}
        </form>
      )}

      {note && <p className="mt-2 text-sm text-muted">{note}</p>}
    </Card>
  );
}
