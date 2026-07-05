"""
Unit tests for the face-match sidecar (main.py).

DeepFace is mocked so these run in milliseconds with no model weights or
TensorFlow — we're testing the HTTP contract, the score scaling, and the
error paths that TripNest.Core's FaceMatchClient depends on, not DeepFace.
"""
import base64
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

# Make main.py importable when pytest is run from the tests/ dir or elsewhere.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import main  # noqa: E402


def b64(data: bytes = b"not-a-real-image") -> str:
    return base64.b64encode(data).decode()


class FakeDeepFace:
    """Stand-in for the DeepFace module; records the last verify() kwargs."""

    def __init__(self, result=None, raises=None, faces=None, extract_raises=None):
        self._result = result or {"distance": 0.0, "threshold": 0.3, "verified": True}
        self._raises = raises
        # Default: one live face filling the frame (antispoof_score 0.9 -> liveness 90).
        self._faces = faces if faces is not None else [
            {"facial_area": {"w": 100, "h": 100}, "is_real": True, "antispoof_score": 0.9}
        ]
        self._extract_raises = extract_raises
        self.last_kwargs = None

    def extract_faces(self, **kwargs):
        if self._extract_raises is not None:
            raise self._extract_raises
        return self._faces

    def verify(self, **kwargs):
        self.last_kwargs = kwargs
        if self._raises is not None:
            raise self._raises
        return self._result


@pytest.fixture
def client():
    # Plain TestClient (no `with`) so the real lifespan/DeepFace import is skipped.
    return TestClient(main.app)


@pytest.fixture(autouse=True)
def loaded_model(monkeypatch):
    """Default: a model is loaded and reports a perfect match."""
    monkeypatch.setattr(main, "_deepface", FakeDeepFace())


def test_health_reports_model_loaded(client):
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "ok", "model_loaded": True}


def test_compare_returns_similarity_score(client, monkeypatch):
    monkeypatch.setattr(main, "_deepface", FakeDeepFace({"distance": 0.0, "threshold": 0.3, "verified": True}))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    assert r.status_code == 200
    body = r.json()
    assert body["verified"] is True
    assert body["similarity_score"] == 100.0  # distance 0 -> perfect
    assert body["liveness_score"] == 90.0     # antispoof_score 0.9 -> 90
    assert body["liveness_passed"] is True
    assert body["model"] == "Facenet512"


def test_liveness_score_reflects_spoof(client, monkeypatch):
    # A low antispoof_score (likely a printed/replayed photo) -> low liveness, not real.
    monkeypatch.setattr(main, "_deepface", FakeDeepFace(
        faces=[{"facial_area": {"w": 100, "h": 100}, "is_real": False, "antispoof_score": 0.15}]))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    body = r.json()
    assert body["liveness_score"] == 15.0
    assert body["liveness_passed"] is False


def test_liveness_judged_on_largest_face(client, monkeypatch):
    # The subject's face fills the frame; an incidental small background face should be ignored.
    monkeypatch.setattr(main, "_deepface", FakeDeepFace(faces=[
        {"facial_area": {"w": 10, "h": 10}, "is_real": False, "antispoof_score": 0.1},
        {"facial_area": {"w": 200, "h": 200}, "is_real": True, "antispoof_score": 0.95},
    ]))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    body = r.json()
    assert body["liveness_score"] == 95.0
    assert body["liveness_passed"] is True


def test_score_is_50_at_threshold_distance(client, monkeypatch):
    # distance == threshold -> halfway -> 50
    monkeypatch.setattr(main, "_deepface", FakeDeepFace({"distance": 0.3, "threshold": 0.3, "verified": True}))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    assert r.json()["similarity_score"] == 50.0


def test_score_clamped_to_zero_for_far_distance(client, monkeypatch):
    # distance >= 2*threshold -> clamped to 0
    monkeypatch.setattr(main, "_deepface", FakeDeepFace({"distance": 5.0, "threshold": 0.3, "verified": False}))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    body = r.json()
    assert body["verified"] is False
    assert body["similarity_score"] == 0.0


def test_no_face_detected_returns_422(client, monkeypatch):
    monkeypatch.setattr(main, "_deepface", FakeDeepFace(raises=ValueError("Face could not be detected")))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    assert r.status_code == 422


def test_unexpected_deepface_error_returns_500(client, monkeypatch):
    monkeypatch.setattr(main, "_deepface", FakeDeepFace(raises=RuntimeError("boom")))
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    assert r.status_code == 500


def test_model_not_loaded_returns_503(client, monkeypatch):
    monkeypatch.setattr(main, "_deepface", None)
    r = client.post("/compare-faces", json={"photo1_base64": b64(), "photo2_base64": b64()})
    assert r.status_code == 503


def test_missing_image_source_returns_400(client):
    # photo1 has neither url nor base64
    r = client.post("/compare-faces", json={"photo2_base64": b64()})
    assert r.status_code == 400


def test_url_fetch_failure_returns_424(client, monkeypatch):
    import requests

    def boom(*args, **kwargs):
        raise requests.exceptions.ConnectionError("refused")

    monkeypatch.setattr(requests, "get", boom)
    r = client.post("/compare-faces", json={"photo1_url": "http://localhost:5135/photos/x.jpg", "photo2_base64": b64()})
    assert r.status_code == 424


def test_invalid_base64_returns_400(client):
    r = client.post("/compare-faces", json={"photo1_base64": "!!!not base64!!!", "photo2_base64": b64()})
    assert r.status_code == 400
