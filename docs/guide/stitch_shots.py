"""Join the scrolled shots of one window into a single picture, then copy the guide's pictures here.

    py -B docs/guide/stitch_shots.py <folder with the raw shots>

The raw shots come from a run on the fictional demo database only (tools/CAL-QR.DemoData). A screen taller than the capture
display was shot at scroll steps that overlap by 30 percent: <name>.png, <name>_p01.png ... and <name>.pane.txt with the bounds
of its scrollable pane (x y width height steps, relative to the shot).
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent / "images"
STITCH = ["settings_CardSecurity", "settings_CardPaths", "settings_CardPrint", "settings_CardBackup",
          "settings_CardGeneral", "record_new", "record_btn2", "user_new", "owner_new", "type_new", "record_btn3"]
COPY = ["login", "dashboard", "records", "records_search", "record_btn1", "record_btn4", "record_btn5", "verify", "owners", "types", "reports", "users", "audit"]
WIZARD = ["wizard2", "wizard3"]
WIZARD_BOTTOM = 688     # the wizard is taller than the capture display: its buttons lie under the task bar
SCROLLBAR = 24          # width of the pane's scroll bar, at its left edge
TRIM = 3                # rows at the edges of the pane: its frame stays in place while the content scrolls
CARDS_ABOVE_PANE = 135  # settings: the row of cards above the pane is kept, the banner is not


def overlap(top: np.ndarray, next_: np.ndarray) -> tuple[int, float]:
    """The number of rows at the end of `top` that best match the start of `next_`, and the mismatch."""
    best, best_k = 1e9, 0
    for k in range(int(next_.shape[0] * 0.2), min(top.shape[0], next_.shape[0]) + 1):  # blank rows match too well below this
        d = float(np.abs(top[-k:].astype(int) - next_[:k].astype(int)).mean())
        if d < best:
            best, best_k = d, k
    return best_k, best


def stitch(raw: Path, name: str) -> Image.Image:
    x, y, w, h, n = (int(v) for v in (raw / f"{name}.pane.txt").read_text().split())
    frames = [np.array(Image.open(raw / f"{name}.png").convert("RGB"))]
    frames += [np.array(Image.open(raw / f"{name}_p{k:02d}.png").convert("RGB")) for k in range(1, n + 1)]
    x0, x1 = max(0, x), min(frames[0].shape[1], x + w)
    y1 = min(frames[0].shape[0], y + h)
    inner = x0 + SCROLLBAR                 # the scroll bar moves between shots: compare without it
    pane = frames[0][y:y1 - TRIM, inner:x1]
    for f in frames[1:]:
        part = f[y + TRIM:y1 - TRIM, inner:x1]
        k, d = overlap(pane, part)
        print(f"    overlap {k} rows, mismatch {d:.2f}")
        if d > 12.0:
            sys.exit(f"{name}: no clean overlap")
        pane = np.vstack([pane, part[k:]])
    # the pane loses its scroll bar: the picture shows all of it
    pane[:, -6:] = pane[:, -7:-6]          # the pane's frame stays in place: do not repeat it
    rows = pane.shape[0]
    left = np.repeat(frames[0][y:y + 1, :x0], rows, axis=0)
    right = np.repeat(frames[0][y:y + 1, x1:], rows, axis=0)
    pane = np.hstack([left, np.repeat(pane[:, :1], inner - x0, axis=1), pane, right])
    top = y - CARDS_ABOVE_PANE if name.startswith("settings_") else 0
    return Image.fromarray(np.vstack([frames[0][top:y], pane, frames[0][y1:]]))


def main() -> None:
    raw = Path(sys.argv[1])
    OUT.mkdir(exist_ok=True)
    for name in STITCH:
        print(name)
        save(stitch(raw, name), name)
    for name in COPY:
        save(Image.open(raw / f"{name}.png").convert("RGB"), name)
    for name in WIZARD:
        im = Image.open(raw / f"{name}.png").convert("RGB")
        save(im.crop((0, 0, im.width, WIZARD_BOTTOM)), name)
    # the certificate form is too long for one page: cut it at the quietest row near the middle
    im = Image.open(OUT / "record_btn3.png").convert("RGB")
    a = np.array(im.convert("L")).astype(int)
    mid = im.height // 2
    cut = mid - 150 + int(np.argmin(a[mid - 150:mid + 150].std(axis=1)))
    save(im.crop((0, 0, im.width, cut)), "record_btn3_a")
    save(im.crop((0, cut, im.width, im.height)), "record_btn3_b")
    (OUT / "record_btn3.png").unlink()
    im = Image.open(raw / "settings_CardReset.png").convert("RGB")
    save(im.crop((0, 345, im.width, im.height)), "settings_CardReset")


def save(im: Image.Image, name: str) -> None:
    im.quantize(256, dither=Image.Dither.NONE).save(OUT / f"{name}.png", optimize=True)


if __name__ == "__main__":
    main()
