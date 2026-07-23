// Free, in-browser walkthrough clip renderer: a cinematic Ken Burns pan/zoom
// over a listing photo, drawn on a canvas and recorded to WebM with
// MediaRecorder. No API, no quota — the default provider when Veo isn't
// available. Recording is realtime, so a clip takes its own duration (~8s).

export const KENBURNS_DURATION_SEC = 8;

const WIDTH = 720;
const HEIGHT = 1280; // 9:16, matching the tour's Stories frame
const FPS = 30;

// A few motion paths, varied per room so consecutive clips don't feel cloned.
// pan values are -1..1 fractions of the available slack in each axis.
const PATHS = [
  { zoomFrom: 1.06, zoomTo: 1.28, panX: 0.7, panY: -0.3 },
  { zoomFrom: 1.25, zoomTo: 1.05, panX: -0.6, panY: 0.4 },
  { zoomFrom: 1.08, zoomTo: 1.3, panX: -0.5, panY: -0.5 },
  { zoomFrom: 1.28, zoomTo: 1.08, panX: 0.5, panY: 0.5 },
  { zoomFrom: 1.05, zoomTo: 1.22, panX: 0.0, panY: 0.7 },
];

function easeInOut(p: number): number {
  return p < 0.5 ? 2 * p * p : 1 - (-2 * p + 2) ** 2 / 2;
}

function pickMimeType(): string | undefined {
  return ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm']
    .find((t) => MediaRecorder.isTypeSupported(t));
}

/** Render a 9:16 Ken Burns clip from a photo. `seed` varies the motion path. */
export async function renderKenBurnsClip(photo: Blob, seed = 0): Promise<Blob> {
  if (typeof MediaRecorder === 'undefined') {
    throw new Error('This browser cannot record video (MediaRecorder unavailable)');
  }
  const bitmap = await createImageBitmap(photo);
  const canvas = document.createElement('canvas');
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const ctx = canvas.getContext('2d', { alpha: false });
  if (!ctx) throw new Error('Canvas 2D context unavailable');

  const mimeType = pickMimeType();
  const stream = canvas.captureStream(FPS);
  const recorder = new MediaRecorder(stream, {
    ...(mimeType ? { mimeType } : {}),
    videoBitsPerSecond: 4_000_000,
  });
  const chunks: BlobPart[] = [];
  recorder.ondataavailable = (e) => {
    if (e.data.size > 0) chunks.push(e.data);
  };
  const recorded = new Promise<Blob>((resolve, reject) => {
    recorder.onstop = () => resolve(new Blob(chunks, { type: mimeType ?? 'video/webm' }));
    recorder.onerror = () => reject(new Error('Clip recording failed'));
  });

  const path = PATHS[Math.abs(seed) % PATHS.length];
  const cover = Math.max(WIDTH / bitmap.width, HEIGHT / bitmap.height);

  recorder.start();
  const startedAt = performance.now();
  await new Promise<void>((resolve) => {
    const draw = () => {
      const elapsed = (performance.now() - startedAt) / 1000;
      const p = easeInOut(Math.min(elapsed / KENBURNS_DURATION_SEC, 1));
      const zoom = path.zoomFrom + (path.zoomTo - path.zoomFrom) * p;
      const dw = bitmap.width * cover * zoom;
      const dh = bitmap.height * cover * zoom;
      // Slack available for panning while the image still covers the frame.
      const slackX = Math.max(0, (dw - WIDTH) / 2);
      const slackY = Math.max(0, (dh - HEIGHT) / 2);
      const ox = path.panX * slackX * (2 * p - 1) * 0.5;
      const oy = path.panY * slackY * (2 * p - 1) * 0.5;
      ctx.drawImage(bitmap, (WIDTH - dw) / 2 + ox, (HEIGHT - dh) / 2 + oy, dw, dh);
      if (elapsed < KENBURNS_DURATION_SEC) {
        requestAnimationFrame(draw);
      } else {
        recorder.stop();
        resolve();
      }
    };
    requestAnimationFrame(draw);
  });

  bitmap.close();
  return recorded;
}
