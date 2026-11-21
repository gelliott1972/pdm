"""Excel sheet for Grant's answers to Desk SW9 (21 Nov 2026): one row per drawing, sheet and view with the centre lines and
centre marks that were white and now show; Grant picks Keep / Delete all / Delete some per row.
Usage: python sw/white/sw9-sheet.py <work folder> <out.xlsx>
"""
import collections, ntpath, sys
from openpyxl import Workbook
from openpyxl.styles import Font, Alignment, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

W, out = sys.argv[1], sys.argv[2]
BS = chr(92)
rows = collections.OrderedDict()
for l in open(W + '/fix.txt', encoding='utf-8', errors='replace'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'ITEM' and c[4] in ('CenterLine', 'CenterMarkSym'):
        k = (c[1], c[2], c[3])
        rows.setdefault(k, [0, 0])
        rows[k][0 if c[4] == 'CenterLine' else 1] += 1
rows[(BS.join(['P:', 'APEX', 'GNRIC', 'GNRIC-BRP-10.SLDDRW']), 'Sheet4', '')] = [2, 0]   # done before item logging
keys = sorted(rows, key=lambda k: (ntpath.basename(k[0]).upper(), k[1], k[2]))

wb = Workbook()
ws = wb.active
ws.title = 'SW9'
F = Font(name='Arial', size=10)
B = Font(name='Arial', size=10, bold=True)
I = Font(name='Arial', size=10, italic=True, color='808080')
ws['A1'] = 'Centre lines and centre marks that were white and now show (Desk SW9). Fill in column G for each row; add a note in H if needed.'
ws['A2'] = 'G: Keep = leave them showing. Delete all = SW deletes every one listed on that row. Delete some = say which in H (e.g. "the short one at the right").'
for a in ('A1', 'A2'): ws[a].font = F
head = ['Drawing', 'Folder', 'Sheet', 'View', 'Centre lines', 'Centre marks', 'Keep / Delete all / Delete some', 'Note']
for i, h in enumerate(head, 1):
    ws.cell(row=4, column=i, value=h).font = B
ex = ['Example: GNRIC-BRP-11', 'APEX/GNRIC', 'Sheet4', 'Drawing View57', 2, 0, 'Delete all', 'Short stubs, already deleted on 21 Nov']
for i, v in enumerate(ex, 1):
    ws.cell(row=5, column=i, value=v).font = I
r = 6
for k in keys:
    f, sheet, view = k
    vals = [ntpath.basename(f)[:-7], ntpath.dirname(f)[3:].replace(BS, '/'), sheet, view or '(not logged)', rows[k][0], rows[k][1], None, None]
    for i, v in enumerate(vals, 1):
        ws.cell(row=r, column=i, value=v).font = F
    ws.cell(row=r, column=7).fill = PatternFill('solid', start_color='FFFF00')
    r += 1
dv = DataValidation(type='list', formula1='"Keep,Delete all,Delete some"', allow_blank=True)
ws.add_data_validation(dv)
dv.add('G6:G%d' % (r - 1))
for col, w in zip('ABCDEFGH', (22, 30, 9, 20, 12, 12, 30, 45)):
    ws.column_dimensions[col].width = w
for row in ws.iter_rows(min_row=4, max_row=r - 1):
    for cell in row: cell.alignment = Alignment(vertical='top')
ws.freeze_panes = 'A5'
wb.save(out)
print(r - 6, 'rows written to', out)
