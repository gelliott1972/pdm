"""PDF mark-up helper for the Desk's in-browser commenting tool (Grant, 28 Nov). The Desk (and any portal using the
same component) runs it; it never talks to the browser itself. Needs PyMuPDF (pip install pymupdf), as desk.py does.

  python -I pdfmark.py info <file>                     -> JSON {"pages": [{"w": .., "h": ..}], "image": bool}
  python -I pdfmark.py render <file> <page> <zoom> <out.png>
  python -I pdfmark.py save <file> <annots.json> <out.pdf> <author>

Coordinates are in page points as displayed (top-left origin, y down, the page's rotation applied), the same space the
browser draws in. annots.json: [{"type": "box"|"arrow"|"highlight"|"text", "page": 0-based,
 "rect": [x0, y0, x1, y1] (box, cloud, highlight, text, callout), "point": [x, y] (callout: where its arrow points),
 "from": [x, y], "to": [x, y] (arrow), "text": "..." (text, callout; a note on others)}]
save writes standard annotations (Square, a Square with a cloudy border for a cloud, Line with an open arrow head,
Highlight, FreeText, and FreeText with the Callout intent and an arrowed leader line for a callout), red, by <author>. An image
file is first made into a one-page PDF the size of the image. <out.pdf> may be <file> itself: then it's saved in place.
Exit code 0 and JSON {"ok": true, "count": n} on success; otherwise 1 and a plain-English line on stdout.
"""
import json, os, sys

RED = (0.85, 0.1, 0.1)


def open_doc(path):
    import pymupdf
    if os.path.splitext(path)[1].lower() == ".pdf":
        return pymupdf.open(path), False
    img = pymupdf.open(path)  # an image: one page the size of the picture, in points (1 px = 1 pt)
    pdf = pymupdf.open("pdf", img.convert_to_pdf())
    img.close()
    return pdf, True


def info(path):
    doc, image = open_doc(path)
    pages = [{"w": round(p.rect.width, 2), "h": round(p.rect.height, 2)} for p in doc]
    print(json.dumps({"pages": pages, "image": image}))


def render(path, page, zoom, out):
    import pymupdf
    doc, _ = open_doc(path)
    pix = doc[int(page)].get_pixmap(matrix=pymupdf.Matrix(float(zoom), float(zoom)), alpha=False, annots=True)
    tmp = out + ".tmp"
    pix.save(tmp, output="png")
    os.replace(tmp, out)


def save(path, annots_file, out, author):
    import pymupdf
    with open(annots_file, encoding="utf-8") as f:
        annots = json.load(f)
    doc, image = open_doc(path)
    n = 0
    for a in annots:
        page = doc[int(a["page"])]
        m = page.derotation_matrix  # displayed -> unrotated page space, where annotations live
        t = a.get("type")
        note = (a.get("text") or "").strip()
        if t in ("box", "cloud", "highlight", "text", "callout"):
            x0, y0, x1, y1 = a["rect"]
            r = (pymupdf.Rect(min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)) * m).normalize()
            if r.width < 2 or r.height < 2:
                continue
            if t in ("box", "cloud"):
                an = page.add_rect_annot(r)
                an.set_colors(stroke=RED)
                # A revision cloud (Bluebeam's, Disney's reviewers): a Square with the cloudy border effect (BE /S /C).
                an.set_border(width=1.5, clouds=2 if t == "cloud" else -1)
            elif t == "callout":
                # A callout: FreeText with the Callout intent and a leader line ending in an arrow at "point".
                tip = pymupdf.Point(*a["point"]) * m
                knee = pymupdf.Point(r.x0 if tip.x < r.x0 else r.x1 if tip.x > r.x1 else (r.x0 + r.x1) / 2,
                                     r.y0 if tip.y < r.y0 else r.y1 if tip.y > r.y1 else (r.y0 + r.y1) / 2)
                an = page.add_freetext_annot(r, note or " ", fontsize=11, fontname="helv", text_color=RED, callout=(tip, knee),
                                             line_end=pymupdf.PDF_ANNOT_LE_OPEN_ARROW, rotate=page.rotation)
                an.set_border(width=1)
            elif t == "highlight":
                an = page.add_highlight_annot(r)
                an.set_colors(stroke=(1, 0.85, 0.2))
            else:
                an = page.add_freetext_annot(r, note or " ", fontsize=11, fontname="helv", text_color=RED, rotate=page.rotation)
                an.set_border(width=1)
        elif t == "arrow":
            p1 = pymupdf.Point(*a["from"]) * m
            p2 = pymupdf.Point(*a["to"]) * m
            if abs(p1 - p2) < 3:
                continue
            an = page.add_line_annot(p1, p2)
            an.set_colors(stroke=RED)
            an.set_border(width=1.5)
            an.set_line_ends(pymupdf.PDF_ANNOT_LE_NONE, pymupdf.PDF_ANNOT_LE_OPEN_ARROW)
        else:
            continue
        an.set_info(title=author, content=note if t != "text" else note)
        an.update()
        n += 1
    same = os.path.abspath(out) == os.path.abspath(path)
    if same and not image:
        doc.save(path, incremental=True, encryption=pymupdf.PDF_ENCRYPT_KEEP)
    else:
        tmp = out + ".tmp"
        doc.save(tmp, garbage=1, deflate=True)
        doc.close()
        os.replace(tmp, out)
    print(json.dumps({"ok": True, "count": n}))


def main():
    try:
        cmd = sys.argv[1]
        if cmd == "info":
            info(sys.argv[2])
        elif cmd == "render":
            render(*sys.argv[2:6])
        elif cmd == "save":
            save(*sys.argv[2:6])
        else:
            print("unknown command"); return 1
        return 0
    except ImportError:
        print("PyMuPDF isn't installed on the Desk's PC (pip install pymupdf)."); return 1
    except PermissionError:
        print("The file is open in another program (close it in PDF-XChange first)."); return 1
    except Exception as ex:  # noqa: BLE001 - one plain line for the page
        if "Permission denied" in str(ex):
            print("The file is open in another program (close it in PDF-XChange first), then save again."); return 1
        print(f"Couldn't do it: {type(ex).__name__}: {ex}"[:300]); return 1


sys.exit(main())
