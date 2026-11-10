# The release step's white check (Grant, 10 Nov): text, lines and filled shapes drawn in white (or nearly white)
# vanish on paper. Items on layer "0" came out white in Grant's exports and black on James's PC.
# Prints one JSON object per page: {"text": n, "samples": [...], "strokes": n, "fills": n}.
# Usage: python -I pdfwhite.py <file.pdf>   (needs PyMuPDF: pip install pymupdf)
import json
import sys

import pymupdf


def white(c):
    if c is None:
        return False
    if isinstance(c, int):  # text: sRGB as an integer
        c = ((c >> 16) & 255) / 255, ((c >> 8) & 255) / 255, (c & 255) / 255
    if len(c) == 1:  # grey
        return c[0] >= 0.94
    if len(c) == 4:  # CMYK: no ink
        return max(c) <= 0.06
    return min(c) >= 0.94


pages = []
with pymupdf.open(sys.argv[1]) as doc:
    for page in doc:
        r = {"text": 0, "samples": [], "strokes": 0, "fills": 0}
        for block in page.get_text("dict")["blocks"]:
            for line in block.get("lines", []):
                for span in line["spans"]:
                    t = span["text"].strip()
                    if t and white(span["color"]):
                        r["text"] += 1
                        if len(r["samples"]) < 3 and t not in r["samples"]:
                            r["samples"].append(t[:30])
        for d in page.get_drawings():
            if "s" in d["type"] and white(d.get("color")):
                r["strokes"] += 1
            if "f" in d["type"] and white(d.get("fill")):
                r["fills"] += 1
        pages.append(r)
print(json.dumps(pages))
