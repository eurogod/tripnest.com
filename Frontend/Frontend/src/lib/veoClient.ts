// Veo (Google Flow's video model) client via the Gemini API.
//
// Converts a listing photo into a short cinematic walkthrough clip:
// POST models/{model}:predictLongRunning with the photo inline, poll the
// operation, then download the generated MP4. In dev all calls go through
// the Vite '/veo' proxy so the browser never fights CORS; production
// builds talk to the API host directly.

const API_HOST = 'https://generativelanguage.googleapis.com';
const BASE = import.meta.env.DEV ? '/veo' : API_HOST;

const MODEL: string =
  (import.meta.env.VITE_VEO_MODEL as string | undefined) ?? 'veo-3.1-fast-generate-preview';

const POLL_INTERVAL_MS = 10_000;
const TIMEOUT_MS = 6 * 60_000;

export function getVeoApiKey(): string | undefined {
  const key = import.meta.env.VITE_GEMINI_API_KEY as string | undefined;
  return key && key.trim().length > 0 ? key.trim() : undefined;
}

export function isVeoConfigured(): boolean {
  return getVeoApiKey() !== undefined;
}

function headers(key: string): Record<string, string> {
  return { 'x-goog-api-key': key, 'Content-Type': 'application/json' };
}

async function fileToBase64(file: File): Promise<{ data: string; mimeType: string }> {
  const dataUrl = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
  const comma = dataUrl.indexOf(',');
  return { data: dataUrl.slice(comma + 1), mimeType: file.type || 'image/jpeg' };
}

const WALKTHROUGH_PROMPT =
  'Slow, smooth cinematic real-estate walkthrough shot of this room, camera gliding gently ' +
  'forward through the space, photorealistic, natural lighting, no people, no on-screen text.';

interface VeoOperation {
  name: string;
  done?: boolean;
  error?: { message?: string };
  response?: {
    generateVideoResponse?: {
      generatedSamples?: { video?: { uri?: string } }[];
    };
  };
}

/** Generate an 8s 9:16 walkthrough clip from a room photo. Returns the MP4 blob. */
export async function generateClipFromPhoto(photo: File): Promise<Blob> {
  const key = getVeoApiKey();
  if (!key) throw new Error('VITE_GEMINI_API_KEY is not configured');

  const image = await fileToBase64(photo);
  const startRes = await fetch(`${BASE}/v1beta/models/${MODEL}:predictLongRunning`, {
    method: 'POST',
    headers: headers(key),
    body: JSON.stringify({
      instances: [{
        prompt: WALKTHROUGH_PROMPT,
        // Live API contract (docs show inlineData, but the endpoint rejects
        // it): image as bytesBase64Encoded + mimeType, duration as a number.
        image: { bytesBase64Encoded: image.data, mimeType: image.mimeType },
      }],
      parameters: { aspectRatio: '9:16', resolution: '720p', durationSeconds: 8 },
    }),
  });
  if (!startRes.ok) {
    let detail = await startRes.text();
    try {
      detail = (JSON.parse(detail) as { error?: { message?: string } }).error?.message ?? detail;
    } catch { /* non-JSON body — use as-is */ }
    if (startRes.status === 429) {
      throw new Error('Veo quota exceeded — enable billing for your Gemini API key (Veo requires a paid tier).');
    }
    throw new Error(`Veo generation request failed (${startRes.status}): ${detail}`);
  }
  const { name } = (await startRes.json()) as VeoOperation;

  const deadline = Date.now() + TIMEOUT_MS;
  let uri: string | undefined;
  while (Date.now() < deadline) {
    await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
    const pollRes = await fetch(`${BASE}/v1beta/${name}`, { headers: headers(key) });
    if (!pollRes.ok) continue; // transient poll failure — keep waiting
    const op = (await pollRes.json()) as VeoOperation;
    if (op.error) throw new Error(`Veo generation failed: ${op.error.message ?? 'unknown error'}`);
    if (op.done) {
      uri = op.response?.generateVideoResponse?.generatedSamples?.[0]?.video?.uri;
      break;
    }
  }
  if (!uri) throw new Error('Veo generation timed out or returned no video');

  // Generated files live on the API host; route through the dev proxy too.
  const videoRes = await fetch(uri.replace(API_HOST, BASE), {
    headers: { 'x-goog-api-key': key },
  });
  if (!videoRes.ok) throw new Error(`Downloading the generated video failed (${videoRes.status})`);
  return videoRes.blob();
}
