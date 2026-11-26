"""Rebuild the two A4 sheets from the exact textures imported into the Android APK."""
from pathlib import Path
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.pdfgen.canvas import Canvas

root = Path(__file__).resolve().parents[2]
pdf = Canvas(str(root / "docs/M2-terciky.pdf"), pagesize=A4, invariant=1)
pdf.setTitle("Spell | M2 | A + B")
for i, label in enumerate(("A", "B")):
    pdf.setFont("Helvetica-Bold", 27)
    pdf.drawString(25*mm, 267*mm, "Spell  /  " + label)
    pdf.setFont("Helvetica", 12)
    pdf.drawRightString(185*mm, 269*mm, "M2   |   A4   |   100 %")
    marker = root / f"client-unity/Assets/Spell/Markers/board-{i}.png"
    pdf.drawImage(str(marker), 25*mm, 88*mm, 160*mm, 160*mm)
    pdf.drawString(25*mm, 75*mm, "160 x 160 mm")
    pdf.setLineWidth(.8)
    pdf.line(25*mm, 49*mm, 125*mm, 49*mm)
    for x in (25, 125):
        pdf.line(x*mm, 47*mm, x*mm, 51*mm)
    pdf.drawString(25*mm, 40*mm, "100 mm")
    pdf.showPage()
pdf.save()
