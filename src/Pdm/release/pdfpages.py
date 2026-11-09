# The release package's sheet sizes: prints each page's size of one PDF as a JSON list of [width, height] in mm.
# Usage: python -I pdfpages.py <file.pdf>   (needs PyMuPDF: pip install pymupdf)
import json
import sys

import pymupdf

with pymupdf.open(sys.argv[1]) as doc:
    print(json.dumps([[round(p.rect.width * 25.4 / 72, 1), round(p.rect.height * 25.4 / 72, 1)] for p in doc]))
