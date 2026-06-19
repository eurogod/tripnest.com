#!/usr/bin/env bash
# Launches the TripNest face-match sidecar on :5001.
# Creates/installs the venv on first run. Uses Python 3.11 because
# tensorflow==2.16.1 has no wheels for very new Python (e.g. 3.14).
set -euo pipefail
cd "$(dirname "$0")"

PYTHON="${PYTHON:-python3.11}"
PORT="${PORT:-5001}"

if ! command -v "$PYTHON" >/dev/null 2>&1; then
  echo "ERROR: $PYTHON not found. Install Python 3.11 (tensorflow 2.16 needs <=3.12)." >&2
  exit 1
fi

if [ ! -x "venv/bin/uvicorn" ]; then
  echo "First run: creating venv and installing deps (TensorFlow + DeepFace, ~1GB)..."
  "$PYTHON" -m venv venv
  venv/bin/pip install --upgrade pip
  venv/bin/pip install -r requirements.txt
fi

echo "Starting face-match sidecar on http://0.0.0.0:${PORT} (first request downloads model weights)..."
exec venv/bin/uvicorn main:app --host 0.0.0.0 --port "$PORT"
