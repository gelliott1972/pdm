r"""LS-08A-07 doubled tube (28 Nov 2026, for Grant via the Desk): one PDF from SwPic's pictures plus two drawn views
(plan section at the 180 tube's mid-height and a front elevation), the 180 tube in red and SFP-2's front leg in blue.
Numbers are from rebuilt SOLIDWORKS (SwPic interference check and a fresh STEP of 08A07-SFA-1X, configuration SAE_END).
Usage: python doubled_pdf.py <SwPic out folder> <out.pdf>
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

src, out = sys.argv[1], sys.argv[2]
W, H = 1654, 1170   # A4 landscape at 140 dpi


def font(sz, bold=False):
    for f in (('arialbd.ttf' if bold else 'arial.ttf'), 'DejaVuSans.ttf'):
        try:
            return ImageFont.truetype(os.path.join(r'C:\Windows\Fonts', f), sz)
        except OSError:
            pass
    return ImageFont.load_default()


def page(title, lines=(), img=None):
    p = Image.new('RGB', (W, H), 'white')
    d = ImageDraw.Draw(p)
    d.text((60, 40), title, fill='black', font=font(34, True))
    y = 100
    for l in lines:
        d.text((60, y), l, fill='black', font=font(22))
        y += 32
    if img is not None:
        im = img.copy()
        im.thumbnail((W - 120, H - y - 60))
        p.paste(im, ((W - im.width) // 2, y + 20))
    return p


def shs(d, cx, cy, s, wall, col, dash=False, width=3):
    # outer and inner squares of a 40x40x4 tube, centred, scale s px/mm
    for half in (20, 20 - wall):
        x0, y0, x1, y1 = cx - half * s, cy - half * s, cx + half * s, cy + half * s
        if dash:
            n = 16
            for i in range(n):
                if i % 2:
                    continue
                t0, t1 = i / n, (i + 1) / n
                for (ax, ay, bx, by) in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)):
                    d.line((ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1), fill=col, width=width)
        else:
            d.rectangle((x0, y0, x1, y1), outline=col, width=width)


RED, BLUE, GREY = (200, 30, 30), (30, 80, 220), (150, 150, 150)

# plan section at Y -800 (looking down), X across, Z down the page
sec = Image.new('RGB', (900, 700), 'white')
d = ImageDraw.Draw(sec)
s = 9.0
cx, cy = 450, 330
# FP-200x200 plate outline below (X -450..-250, Z 1090..1290), dashed grey; tube centre X -350, Z 1190
shs(d, cx, cy, s, 4, BLUE, width=6)
shs(d, cx, cy, s, 4, RED, dash=True, width=3)
d.text((40, 600), 'Blue: 08X07-SFP-2 front leg, SHS 40x40x4.', fill='black', font=font(20))
d.text((40, 630), 'Red dashed: the "180" tube, SHS 40x40x4. Both centred on X -350, Z 1190:', fill='black', font=font(20))
d.text((40, 660), 'the outlines lie exactly on top of each other.', fill='black', font=font(20))
d.text((40, 20), 'Plan section at Y -800 (the 180 tube\'s mid-height), looking down', fill='black', font=font(24, True))

# front elevation (X across, Y up): leg Y -890..-500, tube Y -890..-710, plate Y -900..-890
el = Image.new('RGB', (900, 700), 'white')
d = ImageDraw.Draw(el)
s2 = 1.2
ox, oy = 200, 600          # X -350 at ox; Y -900 at oy
def P(x, y):
    return (ox + (x + 350) * s2 * 3, oy - (y + 900) * s2)
d.rectangle(P(-450, -890) + P(-250, -900), outline=GREY, width=2)
x0, y0 = P(-370, -500); x1, y1 = P(-330, -890)
d.rectangle((x0, y0, x1, y1), outline=BLUE, width=6)
x0, y0 = P(-370, -710); x1, y1 = P(-330, -890)
d.rectangle((x0 + 10, y0, x1 - 10, y1), outline=RED, width=4)
d.text((P(-330, -500)[0] + 20, P(-330, -500)[1] - 10), 'SFP-2 front leg: Y -890 to -500 (390 long)', fill=BLUE, font=font(20))
d.text((P(-330, -710)[0] + 20, P(-330, -710)[1] - 10), '"180" tube: Y -890 to -710 (180 long)', fill=RED, font=font(20))
d.text((P(-250, -890)[0] + 20, P(-250, -895)[1] - 10), 'plate FP-200x200 (GNRIC-PLP-10)', fill=GREY, font=font(20))
d.text((40, 20), 'Front elevation of the leg (X widened 3x for clarity)', fill='black', font=font(24, True))

facts = [
    'Where: LS-08A-07, subframe 08A07-SFA-1X, configuration SAE_END (the one 08A07-STA-01 uses), latest vault version (James 27 Nov 07:01Z).',
    'Front leg of 08X07-SFP-2 (configuration Floor Mount-500): SHS 40x40x4, X -370..-330, Z 1170..1210, from the plate (Y -890) up to Y -500.',
    'Tube 08X07-BRP-SHS-6 (part 08X07-BRP-SHS, configuration "180"): SHS 40x40x4, same X and Z, Y -890..-710.',
    'SOLIDWORKS interference check: these two are the only clash in the subframe; overlap 95,635 mm3 = the whole 180 tube.',
    'So the 180 tube sits entirely inside the SFP-2 leg: the same steel twice (180 mm of SHS 40x40x4, about 0.8 kg, would be ordered twice).',
    'In the other configuration (V2, used by the STA\'s V2) SFP-2 is a "Cantilever-500b" with no floor leg; there the 180 tubes ARE the legs',
    '(no clash). Its twin 08X07-BRP-SHS-5 is already suppressed in SAE_END; SHS-6 is not.',
    'Nothing has been changed. Pictures: SOLIDWORKS (shading only; colour overrides did not show in its saved images) and two drawn views.',
]
pages = [page('LS-08A-07: is the 180 tube inside SFP-2\'s front leg a duplicate?', facts)]
pages.append(page('Plan section and front elevation (drawn from the SOLIDWORKS numbers)', [], None))
comb = Image.new('RGB', (1800, 700), 'white'); comb.paste(sec, (0, 0)); comb.paste(el, (900, 0))
pages[-1] = page('Plan section and front elevation (drawn from the SOLIDWORKS numbers)', [], comb)
for f, t in (('iso.png', 'Isometric: the front leg area with both parts shown (parts away from the spot hidden)'),
             ('iso-only-08X07-SFP-2.png', 'Same view with the 180 tube hidden: SFP-2\'s own front leg still reaches the plate'),
             ('iso-only-08X07-BRP-SHS-6.png', 'Same view with SFP-2 hidden: the 180 tube on its own, on the same plate'),
             ('front.png', 'Front view (both shown)')):
    pth = os.path.join(src, f)
    if os.path.exists(pth):
        pages.append(page(t, [], Image.open(pth).convert('RGB')))
pages[0].save(out, save_all=True, append_images=pages[1:], resolution=140)
print('PDF', out, len(pages), 'pages')
