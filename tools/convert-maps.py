"""Downscale the FH6 seasonal map images (8192x8192 AVIF game-file exports) into
2048x2048 JPGs that WPF can load. Run from the repo root with the source *.avif files present:

    python tools/convert-maps.py

Requires Pillow >= 11.3 (AVIF support built in):  python -m pip install -U pillow
The source .avif files are git-ignored (very large); the converted JPGs live in
src/Fh6.Telemetry.Overlay/assets/maps/ and are copied next to the exe at build time.
"""
import os
from PIL import Image

SEASONS = ["spring", "summer", "autumn", "winter"]
OUT_DIR = os.path.join("src", "Fh6.Telemetry.Overlay", "assets", "maps")
SIZE = 2048
QUALITY = 90


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    for season in SEASONS:
        src = f"{season}.avif"
        if not os.path.exists(src):
            print(f"skip (missing): {src}")
            continue
        image = Image.open(src).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
        out = os.path.join(OUT_DIR, f"{season}.jpg")
        image.save(out, "JPEG", quality=QUALITY)
        print(f"{season}: {out}  {round(os.path.getsize(out) / 1024)} KB")


if __name__ == "__main__":
    main()
