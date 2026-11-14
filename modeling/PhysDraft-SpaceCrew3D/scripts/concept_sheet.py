"""Concept sheet: chibi crew in front A-pose, 3 head-to-body ratios x 2 outfits.
Writes concepts/proportions.svg (A-pose front view = good input for image-to-3D + rigging)."""
import math, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "concepts"); os.makedirs(OUT, exist_ok=True)

INK = "#1d2333"
C = dict(white="#eef1f6", grey="#596173", orange="#f2661c", visor="#14254a", gold="#d9a441",
         skin="#f2b48f", hair="#3a2a24", navy="#2f4f9c", shoe="#2b2d33", cheek="#ff8a8a")

def rr(x, y, w, h, r, fill, sw=4):
    return f'<rect x="{x-w/2:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{r:.1f}" fill="{fill}" stroke="{INK}" stroke-width="{sw}"/>'

def ell(cx, cy, rx, ry, fill, sw=4, extra=""):
    return f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="{rx:.1f}" ry="{ry:.1f}" fill="{fill}" stroke="{INK}" stroke-width="{sw}" {extra}/>'

def limb(x1, y1, x2, y2, w, fill):
    # capsule along a line: thick stroke outline + fill
    return (f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="{w+8:.1f}" stroke-linecap="round"/>'
            f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{fill}" stroke-width="{w:.1f}" stroke-linecap="round"/>')

def figure(cx, ground, H, heads, suit):
    h = H / heads                      # head height
    s = []
    body_h = H - h                      # everything below the head
    torso_h = body_h * 0.52
    leg_h = body_h - torso_h
    head_cy = ground - H + h / 2
    neck = ground - body_h
    torso_w = h * (0.80 if suit else 0.66)
    limb_w = h * (0.30 if suit else 0.22)
    # legs
    for d in (-1, 1):
        lx = cx + d * torso_w * 0.24
        s.append(limb(lx, neck + torso_h * 0.8, lx, ground - limb_w * 0.5, limb_w, C["white"] if suit else C["navy"]))
        s.append(ell(lx + d * 3, ground - limb_w * 0.35, limb_w * 0.75, limb_w * 0.42, C["grey"] if suit else C["shoe"]))
    # arms in A-pose (~35deg below horizontal)
    arm_len = torso_h * 0.95
    for d in (-1, 1):
        sx, sy = cx + d * torso_w * 0.42, neck + torso_h * 0.18
        ex, ey = sx + d * arm_len * math.cos(math.radians(55)), sy + arm_len * math.sin(math.radians(55))
        s.append(limb(sx, sy, ex, ey, limb_w, C["white"] if suit else C["navy"]))
        s.append(ell(ex, ey, limb_w * 0.62, limb_w * 0.62, C["orange"] if suit else C["skin"]))
    # torso
    s.append(rr(cx, neck, torso_w, torso_h, torso_w * 0.42, C["white"] if suit else C["navy"]))
    if suit:
        s.append(rr(cx, neck + torso_h * 0.30, torso_w * 0.45, torso_h * 0.22, 6, C["grey"], 3))
        s.append(ell(cx - torso_w * 0.09, neck + torso_h * 0.41, 4.5, 4.5, "#3be38a", 0))
        s.append(ell(cx + torso_w * 0.09, neck + torso_h * 0.41, 4.5, 4.5, "#ff4a3a", 0))
        s.append(rr(cx, neck + torso_h * 0.80, torso_w * 1.0, torso_h * 0.10, 4, C["orange"], 3))
    else:
        s.append(rr(cx + torso_w * 0.2, neck + torso_h * 0.25, torso_w * 0.2, torso_h * 0.1, 3, C["orange"], 3))
        s.append(rr(cx, neck + torso_h * 0.80, torso_w * 1.0, torso_h * 0.08, 4, C["shoe"], 3))
    # head
    hr = h * 0.5
    if suit:
        s.append(f'<line x1="{cx+hr*0.6:.1f}" y1="{head_cy-hr*0.7:.1f}" x2="{cx+hr*0.6:.1f}" y2="{head_cy-hr*1.35:.1f}" stroke="{INK}" stroke-width="4"/>')
        s.append(ell(cx + hr * 0.6, head_cy - hr * 1.38, 7, 7, C["orange"], 3))
        s.append(ell(cx, neck - 2, torso_w * 0.42, h * 0.07, C["grey"]))
        s.append(ell(cx, head_cy, hr * 1.06, hr, C["white"]))
        s.append(ell(cx, head_cy + hr * 0.05, hr * 0.74, hr * 0.58, C["visor"], 5, f'style="stroke:{C["gold"]}"'))
        s.append(ell(cx - hr * 0.30, head_cy - hr * 0.20, hr * 0.14, hr * 0.08, "#ffffff", 0, 'opacity="0.85"'))
    else:
        s.append(ell(cx, neck - 2, torso_w * 0.30, h * 0.05, C["orange"], 3))
        for d in (-1, 1):
            s.append(ell(cx + d * hr * 0.98, head_cy + hr * 0.05, hr * 0.14, hr * 0.2, C["skin"], 3))
        s.append(ell(cx, head_cy, hr * 0.98, hr * 0.95, C["skin"]))
        # hair cap
        s.append(f'<path d="M{cx-hr*1.02:.1f},{head_cy+hr*0.05:.1f} Q{cx-hr*1.05:.1f},{head_cy-hr*1.15:.1f} {cx:.1f},{head_cy-hr*1.05:.1f} '
                 f'Q{cx+hr*1.05:.1f},{head_cy-hr*1.15:.1f} {cx+hr*1.02:.1f},{head_cy+hr*0.05:.1f} '
                 f'Q{cx+hr*0.6:.1f},{head_cy-hr*0.55:.1f} {cx-hr*0.1:.1f},{head_cy-hr*0.45:.1f} '
                 f'Q{cx-hr*0.7:.1f},{head_cy-hr*0.4:.1f} {cx-hr*1.02:.1f},{head_cy+hr*0.05:.1f}Z" fill="{C["hair"]}" stroke="{INK}" stroke-width="4"/>')
        for d in (-1, 1):
            ex = cx + d * hr * 0.36
            s.append(ell(ex, head_cy + hr * 0.12, hr * 0.11, hr * 0.17, INK, 0))
            s.append(ell(ex - hr * 0.04, head_cy + hr * 0.04, hr * 0.04, hr * 0.05, "#fff", 0))
            s.append(ell(cx + d * hr * 0.60, head_cy + hr * 0.38, hr * 0.13, hr * 0.07, C["cheek"], 0, 'opacity="0.8"'))
        s.append(f'<path d="M{cx-hr*0.1:.1f},{head_cy+hr*0.42:.1f} Q{cx:.1f},{head_cy+hr*0.52:.1f} {cx+hr*0.1:.1f},{head_cy+hr*0.42:.1f}" fill="none" stroke="{INK}" stroke-width="4" stroke-linecap="round"/>')
    return "\n".join(s)

W, Hc = 2000, 900
ground, H = 790, 440
parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{Hc}" viewBox="0 0 {W} {Hc}">',
         f'<rect width="{W}" height="{Hc}" fill="#f4f1ea"/>',
         '<style>text{font-family:"Hiragino Sans","Hiragino Kaku Gothic ProN",sans-serif;fill:#1d2333}</style>',
         f'<text x="60" y="70" font-size="34" font-weight="700">PhysDraft クルー 設定画：頭身比較（A ポーズ・正面）</text>',
         f'<text x="60" y="110" font-size="20">同じ身長 {H}px で頭身だけを変えています。後でボーンを入れる前提で、腕は約35°下げた A ポーズ。</text>']
labels = [(2.0, "A｜2頭身（ころころ）"), (2.5, "B｜2.5頭身（今の3D）"), (3.0, "C｜3頭身（動かしやすい）")]
for i, (n, lab) in enumerate(labels):
    base = 360 + i * 640
    parts.append(f'<line x1="{base-300}" y1="{ground}" x2="{base+300}" y2="{ground}" stroke="#c9c3b6" stroke-width="3"/>')
    for k in range(int(math.ceil(n)) + 1):
        y = ground - min(k * H / n, H)
        parts.append(f'<line x1="{base-300}" y1="{y:.1f}" x2="{base+300}" y2="{y:.1f}" stroke="#ddd6c8" stroke-width="2" stroke-dasharray="6 8"/>')
    parts.append(figure(base - 145, ground, H, n, True))
    parts.append(figure(base + 145, ground, H, n, False))
    parts.append(f'<text x="{base}" y="{ground+60}" font-size="28" font-weight="700" text-anchor="middle">{lab}</text>')
parts.append("</svg>")
open(os.path.join(OUT, "proportions.svg"), "w", encoding="utf-8").write("\n".join(parts))
print("wrote", os.path.join(OUT, "proportions.svg"))
