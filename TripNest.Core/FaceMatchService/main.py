"""
TripNest Face Match Sidecar
----------------------------
A small FastAPI service that wraps DeepFace to compare two face photos
and return a similarity score (0-100) plus a verified boolean.

Called by TripNest.Core's FaceMatchClient (C#) over HTTP.

Run with:
    uvicorn main:app --host 0.0.0.0 --port 5001
"""

import base64
import io
import logging
import os
import tempfile
import threading
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("face-match-sidecar")

# Each DeepFace inference loads a lot into memory; running several at once can
# OOM the host. Sync endpoints run in FastAPI's threadpool, so without a cap
# concurrent requests would all infer in parallel. Bound the heavy work to
# MAX_CONCURRENT_INFERENCES (default 1) — extra requests queue instead of crashing.
_inference_semaphore = threading.Semaphore(int(os.environ.get("MAX_CONCURRENT_INFERENCES", "1")))

# DeepFace is imported lazily inside the lifespan handler so the API can
# start fast and report a clear error if the model fails to load, instead
# of crashing silently at import time.
_deepface = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _deepface
    logger.info("Loading DeepFace model (first run will download weights)...")
    from deepface import DeepFace  # noqa: WPS433 (intentional lazy import)
    _deepface = DeepFace
    logger.info("DeepFace model ready.")
    yield
    logger.info("Shutting down face match sidecar.")


app = FastAPI(
    title="TripNest Face Match Sidecar",
    description="Compares two face photos using DeepFace and returns a similarity score.",
    version="1.0.0",
    lifespan=lifespan,
)


class CompareFacesRequest(BaseModel):
    # Accept either a remote URL or a base64-encoded image for each photo.
    # TripNest.Core will typically send:
    #   photo1_url  -> the NIA photo URL (fetched from TripNest.Id)
    #   photo2_base64 -> the freshly uploaded selfie (read from disk, base64-encoded)
    photo1_url: str | None = Field(default=None, description="URL of the first photo (e.g. NIA photo)")
    photo1_base64: str | None = Field(default=None, description="Base64-encoded first photo")
    photo2_url: str | None = Field(default=None, description="URL of the second photo (e.g. selfie)")
    photo2_base64: str | None = Field(default=None, description="Base64-encoded second photo")
    model_name: str = Field(default="Facenet512", description="DeepFace model to use")
    detector_backend: str = Field(default="retinaface", description="Face detector backend")


class CompareFacesResponse(BaseModel):
    verified: bool
    similarity_score: float  # 0-100, higher = more similar
    liveness_score: float    # 0-100, higher = more likely a live capture (anti-spoofing)
    liveness_passed: bool    # the anti-spoofing model's own real/spoof verdict on the selfie
    distance: float          # raw DeepFace distance metric (lower = more similar)
    threshold: float
    model: str
    facial_areas_detected: bool


def _resolve_image(url: str | None, b64: str | None, label: str) -> str:
    """
    Resolves an image source into a local file path that DeepFace can read.
    Accepts either a URL (downloaded to a temp file) or a base64 string
    (decoded to a temp file). Exactly one of url/b64 must be provided.
    """
    if not url and not b64:
        raise HTTPException(status_code=400, detail=f"{label}: provide either a url or base64 image")

    suffix = ".jpg"
    tmp = tempfile.NamedTemporaryFile(delete=False, suffix=suffix)

    if b64:
        try:
            image_bytes = base64.b64decode(b64)
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"{label}: invalid base64 data") from exc
        tmp.write(image_bytes)
        tmp.flush()
        return tmp.name

    # URL case
    import requests  # local import keeps cold-start fast when unused
    try:
        resp = requests.get(url, timeout=10)
        resp.raise_for_status()
    except Exception as exc:
        logger.warning("%s: failed to fetch image from URL %s -> %r", label, url, exc)
        raise HTTPException(status_code=424, detail=f"{label}: failed to fetch image from URL") from exc
    tmp.write(resp.content)
    tmp.flush()
    return tmp.name


@app.get("/health")
async def health():
    return {"status": "ok", "model_loaded": _deepface is not None}


@app.post("/compare-faces", response_model=CompareFacesResponse)
def compare_faces(request: CompareFacesRequest):
    """
    Compares two face photos and returns a similarity score.

    Declared as a *sync* endpoint on purpose: the body does blocking work
    (HTTP image fetch + a multi-second DeepFace inference). FastAPI runs sync
    endpoints in a threadpool, so the event loop stays free and concurrent
    requests don't starve each other — an async def here would block the loop
    and make the 10s image fetch time out under load (seen as spurious 424s).

    similarity_score is derived from DeepFace's distance metric, scaled to
    0-100 so the .NET side can apply a simple ">= threshold" rule
    (consistent with how the original stub worked).
    """
    if _deepface is None:
        raise HTTPException(status_code=503, detail="Face match model is not loaded yet")

    path1 = _resolve_image(request.photo1_url, request.photo1_base64, "photo1")
    path2 = _resolve_image(request.photo2_url, request.photo2_base64, "photo2")

    try:
        # Serialize the memory-heavy inference so concurrent requests queue
        # rather than running in parallel and OOM-killing the process.
        with _inference_semaphore:
            # Anti-spoofing / liveness on the SELFIE only (photo2). The NIA reference
            # photo (photo1) is trusted by definition, so spoof-checking it is pointless
            # and printed government photos would fail it anyway. extract_faces with
            # anti_spoofing=True runs DeepFace's MiniFASNet model and tags each detected
            # face with is_real + antispoof_score (0-1, higher = more likely live).
            faces = _deepface.extract_faces(
                img_path=path2,
                detector_backend=request.detector_backend,
                enforce_detection=True,
                anti_spoofing=True,
            )
            # A selfie may contain more than one face; judge liveness on the largest
            # (the subject), not an incidental background face.
            primary = max(faces, key=lambda f: f["facial_area"]["w"] * f["facial_area"]["h"])
            liveness_score = round(float(primary.get("antispoof_score", 0.0)) * 100, 2)
            liveness_passed = bool(primary.get("is_real", False))

            result = _deepface.verify(
                img1_path=path1,
                img2_path=path2,
                model_name=request.model_name,
                detector_backend=request.detector_backend,
                enforce_detection=True,
            )
    except ValueError as exc:
        # DeepFace raises ValueError when no face is detected in one of the images
        raise HTTPException(status_code=422, detail=f"Face detection failed: {exc}") from exc
    except Exception as exc:
        logger.exception("DeepFace comparison failed")
        raise HTTPException(status_code=500, detail="Internal error during face comparison") from exc

    distance = float(result["distance"])
    threshold = float(result["threshold"])
    verified = bool(result["verified"])

    # Convert distance (lower = more similar) into a 0-100 similarity score.
    # Distances above 2x the threshold are clamped to 0 similarity.
    max_distance = threshold * 2
    similarity = max(0.0, 100.0 * (1 - min(distance, max_distance) / max_distance))

    return CompareFacesResponse(
        verified=verified,
        similarity_score=round(similarity, 2),
        liveness_score=liveness_score,
        liveness_passed=liveness_passed,
        distance=round(distance, 4),
        threshold=round(threshold, 4),
        model=request.model_name,
        facial_areas_detected=True,
    )
