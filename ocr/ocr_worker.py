"""Private JSON-lines PaddleOCR worker. It never writes input images or recognized text to disk."""
from __future__ import annotations

import base64
import contextlib
import io
import json
import os
import sys
import traceback

import numpy as np
from PIL import Image

# Paddle's native runtime writes diagnostics directly to file descriptor 1, bypassing
# contextlib.redirect_stdout. Keep a duplicate for our JSON protocol, then redirect
# the process-wide stdout descriptor to stderr before Paddle is imported.
_protocol_output = os.fdopen(os.dup(sys.stdout.fileno()), "w", encoding="utf-8", buffering=1)
os.dup2(sys.stderr.fileno(), sys.stdout.fileno())


def emit(payload: dict) -> None:
    print(json.dumps(payload, ensure_ascii=False), file=_protocol_output, flush=True)


def rectangle(points):
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    return min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)


class OcrEngine:
    def __init__(self):
        # PaddleOCR writes model-download progress to stdout, which would corrupt this protocol.
        with contextlib.redirect_stdout(sys.stderr):
            from paddleocr import PaddleOCR
            self.engine = PaddleOCR(lang="ch")

    def recognize(self, image):
        try:
            return self._recognize_v3(image)
        except (AttributeError, TypeError):
            return self._recognize_legacy(image)

    def _recognize_v3(self, image):
        lines = []
        with contextlib.redirect_stdout(sys.stderr):
            results = self.engine.predict(image)
            for result in results:
                payload = result.json if hasattr(result, "json") else result
                if isinstance(payload, str):
                    payload = json.loads(payload)
                # PaddleOCR 3.x wraps the page result in a `res` object; older
                # result shapes expose the fields directly.
                if isinstance(payload, dict):
                    payload = payload.get("res", payload)
                for box, text, score in zip(
                    payload.get("dt_polys", []),
                    payload.get("rec_texts", []),
                    payload.get("rec_scores", []),
                ):
                    x, y, width, height = rectangle(box)
                    lines.append({"text": str(text), "confidence": float(score), "x": x, "y": y, "width": width, "height": height})
        return lines

    def _recognize_legacy(self, image):
        lines = []
        with contextlib.redirect_stdout(sys.stderr):
            results = self.engine.ocr(image, cls=False)
        for page in results or []:
            for item in page or []:
                box, recognition = item
                x, y, width, height = rectangle(box)
                lines.append({"text": str(recognition[0]), "confidence": float(recognition[1]), "x": x, "y": y, "width": width, "height": height})
        return lines


def run() -> int:
    engine = OcrEngine()
    for raw in sys.stdin:
        try:
            request = json.loads(raw)
            # The .NET record serializer uses PascalCase by default; accept both
            # forms so this private JSON-lines protocol stays version-tolerant.
            encoded_image = request.get("imagePngBase64") or request["ImagePngBase64"]
            payload = base64.b64decode(encoded_image)
            image = np.array(Image.open(io.BytesIO(payload)).convert("RGB"))
            emit({"ok": True, "lines": engine.recognize(image), "error": None})
        except Exception as exc:  # keep the protocol usable even after one malformed request
            traceback.print_exc(file=sys.stderr)
            emit({"ok": False, "lines": [], "error": str(exc)})
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
