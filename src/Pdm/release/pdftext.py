# The release step's PDF read-back: prints the text of each page of one PDF as a JSON list of strings.
# Usage: python -I pdftext.py <file.pdf>   (needs PyMuPDF: pip install pymupdf)
import json
import sys

import pymupdf

with pymupdf.open(sys.argv[1]) as doc:
    print(json.dumps([page.get_text() for page in doc]))
