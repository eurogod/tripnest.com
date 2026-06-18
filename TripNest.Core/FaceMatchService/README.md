# TripNest Face Match Sidecar

A small Python service that wraps **DeepFace** to compare two face photos
(e.g. the NIA Ghana Card photo vs a freshly uploaded selfie) and return a
similarity score. Called by TripNest.Core's `FaceMatchClient` over HTTP.

This exists as a separate Python service because the best open-source face
recognition models (FaceNet, ArcFace, etc., bundled inside DeepFace) are
Python-only — there isn't a mature equivalent in the .NET ecosystem.

---

## Setup

```bash
cd FaceMatchService
python -m venv venv
source venv/bin/activate          # Windows: venv\Scripts\activate
pip install -r requirements.txt
```

First install will take a while — `tensorflow` and DeepFace's model weights
are large downloads.

## Run

```bash
uvicorn main:app --host 0.0.0.0 --port 5001
```

Visit `http://localhost:5001/docs` for interactive Swagger-style docs.

First request will be slow (downloading the Facenet512 model weights to
`~/.deepface/weights/`). Subsequent requests are fast.

---

## Endpoint

### POST /compare-faces

```json
{
  "photo1_url": "http://localhost:5050/photos/GHA-000001234-5.jpg",
  "photo2_base64": "<base64-encoded selfie bytes>"
}
```

You can mix a URL for one photo and base64 for the other — TripNest.Core
will typically use:
- `photo1_url` → the NIA photo (TripNest.Id serves it over HTTP already)
- `photo2_base64` → the user's freshly uploaded selfie (read straight off
  disk on the .NET side and base64-encoded, no need to expose it via URL)

**Response:**
```json
{
  "verified": true,
  "similarity_score": 91.4,
  "distance": 0.21,
  "threshold": 0.4,
  "model": "Facenet512",
  "facial_areas_detected": true
}
```

- `similarity_score` is 0-100, scaled from DeepFace's raw distance metric —
  this is the number TripNest.Core's `IFaceMatchClient` reads and compares
  against the configurable face match threshold (default 80).
- If no face is detected in either photo, returns `422` with a clear message.
- If a photo URL can't be fetched, returns `424 Failed Dependency`.

---

## Switching to AWS Rekognition later

Nothing in TripNest.Core needs to change except swapping which class
implements `IFaceMatchClient`:

```csharp
// Today:
builder.Services.AddHttpClient<IFaceMatchClient, LocalFaceMatchClient>();

// Later:
builder.Services.AddScoped<IFaceMatchClient, AwsRekognitionFaceMatchClient>();
```

`LocalFaceMatchClient` (calls this sidecar) and a future
`AwsRekognitionFaceMatchClient` both just implement:

```csharp
Task<double> CompareFacesAsync(string photoUrl1, string photoPath2);
```

So this sidecar can run during development/demo, and be swapped for AWS
Rekognition later with zero changes to Verification, Users, or any other
module — they only ever talk to the `IFaceMatchClient` interface.
