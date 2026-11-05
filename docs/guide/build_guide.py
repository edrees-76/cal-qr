"""Build the Arabic user guide PDF: deploy/output/guide/UserGuide.ar.pdf.

    py -B docs/guide/build_guide.py

Source: docs/guide/guide_ar.html (body with {{VERSION}}, {{DATE}}, {{TOC}}), guide.css, images/ (shot on the fictional demo
database). Printed by headless Chrome or Edge, which shape Arabic correctly. Two passes: invisible markers find the page of each
heading, then the contents get their page numbers. Bookmarks and title are added with PyMuPDF. deploy/output is not committed.
"""
from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import date
from pathlib import Path

import pymupdf

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = ROOT / "deploy" / "output" / "guide" / "UserGuide.ar.pdf"
TITLE = "CAL-QR — دليل المستخدم"
CHROME = [os.environ.get("GUIDE_CHROME", ""),
          os.path.join(os.environ.get("LOCALAPPDATA", ""), r"Google\Chrome\Application\chrome.exe"),
          r"C:\Program Files\Google\Chrome\Application\chrome.exe",
          r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
          r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
          r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"]
HEADING = re.compile(r'<h([23]) id="([^"]+)">(.*?)</h\1>', re.S)


def version() -> str:
    text = (ROOT / "CAL-QR" / "CAL-QR.csproj").read_text(encoding="utf-8")
    return re.search(r"<Version>([^<]+)</Version>", text).group(1)


def chrome() -> str:
    for c in CHROME:
        if c and Path(c).exists():
            return c
    sys.exit("Chrome or Edge not found: set GUIDE_CHROME")


def toc(body: str, pages: dict[str, int] | None) -> str:
    rows = []
    for level, hid, text in HEADING.findall(body):
        page = pages.get(hid, "") if pages else "00"
        rows.append(f'<li class="l{level}"><a href="#{hid}"><span>{text}</span><span class="pg">{page}</span></a></li>')
    return '<ul class="toc-list">' + "".join(rows) + "</ul>"


def document(body: str, pages: dict[str, int] | None, markers: bool) -> str:
    today = date.today().strftime("%Y/%m/%d")
    main = body.replace("{{VERSION}}", version()).replace("{{DATE}}", today).replace("{{TOC}}", toc(body, pages))
    if markers:
        main = HEADING.sub(lambda m: f'<h{m[1]} id="{m[2]}">{m[3]}<span class="mk">@@{m[2]}@@</span></h{m[1]}>', main)
    css = (HERE / "guide.css").read_text(encoding="utf-8")
    return (f'<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>{TITLE}</title>'
            f"<style>{css}</style></head><body>{main}</body></html>")


def print_pdf(html: Path, pdf: Path) -> None:
    with tempfile.TemporaryDirectory() as profile:
        subprocess.run([chrome(), "--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--no-first-run",
                        f"--user-data-dir={profile}", f"--print-to-pdf={pdf}", html.resolve().as_uri()],
                       check=True, capture_output=True, timeout=180)


def marker_pages(pdf: Path) -> dict[str, int]:
    found: dict[str, int] = {}
    with pymupdf.open(pdf) as doc:
        for i, page in enumerate(doc):
            for m in re.findall(r"@@([\w-]+)@@", page.get_text()):
                found.setdefault(m, i + 1)
    return found


def finish(pdf: Path, body: str, pages: dict[str, int]) -> int:
    with pymupdf.open(pdf) as doc:
        marks = [[int(lv) - 1, re.sub(r"<[^>]+>", "", text), pages[hid]]
                 for lv, hid, text in HEADING.findall(body) if hid in pages]
        doc.set_toc(marks)
        doc.set_metadata({"title": f"{TITLE} {version()}", "author": "Edrees F. Elhery"})
        n = doc.page_count
        doc.save(OUT, garbage=4, deflate=True)
    return n


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    body = (HERE / "guide_ar.html").read_text(encoding="utf-8")
    html = HERE / "_build.html"           # beside the source: image paths are relative to it
    work = Path(tempfile.mkdtemp())
    try:
        html.write_text(document(body, None, True), encoding="utf-8")
        print_pdf(html, work / "a.pdf")
        pages = marker_pages(work / "a.pdf")
        missing = [h for _l, h, _t in HEADING.findall(body) if h not in pages]
        if missing:
            sys.exit(f"headings without a page: {missing}")
        html.write_text(document(body, pages, False), encoding="utf-8")
        print_pdf(html, work / "b.pdf")
        n = finish(work / "b.pdf", body, pages)
    finally:
        html.unlink(missing_ok=True)
        shutil.rmtree(work, ignore_errors=True)
    print(f"{OUT}: {n} pages, {OUT.stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
