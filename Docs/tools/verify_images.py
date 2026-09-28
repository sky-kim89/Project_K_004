"""이미지 제출 전 검사기 — Docs/Image_Production_Brief.md 6장.

사용법 (저장소 루트에서):
    python Docs/tools/verify_images.py                # 전부
    python Docs/tools/verify_images.py Synergies      # 폴더 하나만
    python Docs/tools/verify_images.py Synergies --sheet   # 검수용 시트 PNG 도 만든다

검사 항목
    크기      계열 규격과 같은가
    형식      RGBA PNG 인가
    내용      전부 투명하거나 전부 불투명하지 않은가
    선명도    기준 완성본(node_soul_urn)보다 흐릿하지 않은가 — 경고만
    색수      팔레트가 지나치게 많거나(사진·3D 렌더) 적지 않은가(더미 그대로) — 경고만
    워터마크  오른쪽 아래 구석에 주변과 동떨어진 밝은 얼룩이 있는가 (의심만 알린다)
    잘림      2×2 시트에서 자를 때 이웃 칸 조각이 따라왔는가 — 가장자리 픽셀로 본다 (경고만)

⚠ 이 검사기는 "규격" 만 본다. 무엇이 그려졌는지는 --sheet 로 눈으로 본다.
"""

import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow 가 필요하다:  pip install pillow")

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
TEX = os.path.join(ROOT, "Assets", "_project", "3.Textures")

# 폴더 → (최종 크기, 종류)
#   종류: "plate" 판이 칸을 채운다 · "cut" 투명 배경(잘림 검사 O) · "scene" 전체 화면 그림
#   Brief 4장의 표와 같아야 한다.
RULES = {
    "Icons/SpeciesPassives": (128, "plate"),
    "Icons/Synergies":       (128, "cut"),
    "Icons/RunPerks":        (128, "plate"),
    "Icons/Skills":          (128, "plate"),
    "Icons/Passives":        (128, "plate"),
    "Icons/Difficulty":      (128, "cut"),
    "Icons/Species":         (128, "cut"),
    "Icons/LobbyBtns":       (128, "cut"),
    "Icons/RelicTree":       (256, "cut"),
    "Icons/Items":           (256, "cut"),
    "Icons/Gear":            (256, "cut"),
    "Icons/Gear/Items":      (256, "cut"),
    "Icons/RunNodes":        (256, "plate"),
    "Icons/RunEvents":       ((1920, 1080), "scene"),
    "Scenes/Facilities":     ((1920, 1080), "scene"),
}

# 폴더가 아니라 파일 하나씩 — 브랜드 4장 (ImageSpecs/00_Brand.md)
BRAND = {
    "Assets/Resources/Icon/Icon.png":                                  ((2048, 2048), "scene"),
    "Assets/_project/3.Textures/BG/BG.png":                            ((2560, 1440), "scene"),
    "Assets/_project/3.Textures/UI/Lobby/title_pixel_general.png":     ((760, 600),   "cut"),
    "Assets/_project/3.Textures/FortressWall_Vertical.png":            ((224, 1280),  "scene"),
}

# 이미 완성된 것 — 규격이 달라도 통과시킨다 (Brief 2장 규칙 6)
DONE = {
    "node_origin", "node_soul_urn", "node_time_reins", "node_moment_mastery",
    "node_disarm", "node_trial_baptism", "node_fear_brand", "node_wither_curse",
    "node_doom_prophecy",
}

# 만들지 않는 것 (Brief 7장) — 검사에서 뺀다
SKIP = {
    "item_battlestone", "item_energy", "item_equip_upgrade_stone", "item_equipbox",
    "item_expbook", "item_gem", "item_general_upgrade_stone", "item_honor",
    "item_skillscroll", "item_soldier_shard", "item_stamina",
    "icon_cat_skill", "icon_cat_summoner",
    "sperk_AffinityGrade", "sperk_SwellOnRepeat", "sperk_WarCry", "sperk_WildSprint",
}


def check(path, name, size, kind):
    scene = kind == "scene"
    """한 장을 검사해 (에러 목록, 경고 목록) 을 돌려준다."""
    errors, warns = [], []
    im = Image.open(path)

    want = size if isinstance(size, tuple) else (size, size)
    if im.size != want:
        errors.append(f"크기 {im.size[0]}x{im.size[1]} — {want[0]}x{want[1]} 이어야 한다")
        return errors, warns

    if im.mode != "RGBA":
        # 투명이 필요 없는 그림(전체 화면 장면·앱 아이콘)은 RGB 로 둬도 된다
        (warns if scene else errors).append(f"형식 {im.mode} — 투명이 필요하면 RGBA 로 저장할 것")
        im = im.convert("RGBA")

    px = im.load()
    w, h = im.size

    # ── 알파: 0/255 외 픽셀 비율 ──
    soft = opaque = 0
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            a = px[x, y][3]
            if a == 255:
                opaque += 1
            elif a != 0:
                soft += 1
    sampled = ((h + 1) // 2) * ((w + 1) // 2)

    # 기준 완성본이 38% 남짓이다 — 그보다 훨씬 흐리면 가장자리가 번진 것이다
    if not scene and soft / sampled > 0.50:
        warns.append(f"반투명 픽셀 {soft / sampled:.0%} — 가장자리가 번졌다 "
                     f"(기준 완성본 node_soul_urn 은 39%)")

    if opaque == 0:
        errors.append("전부 투명하다")
    elif not scene and opaque == sampled:
        warns.append("전부 불투명하다 — 투명 배경이 맞는지 확인")

    # ── 색 수: 픽셀아트는 팔레트가 좁다 (기준 완성본 256px 이 약 15,000색) ──
    colors = len({px[x, y] for y in range(0, h, 2) for x in range(0, w, 2)})
    if not scene and colors > 25000:
        warns.append(f"색 {colors:,}개 — 사진·3D 렌더처럼 넓다 (기준 완성본은 15,000 남짓)")
    if colors < 24:
        warns.append(f"색 {colors}개뿐 — 자리표시(더미)가 그대로인지 확인")

    # ── 시트 자르기 잔재: 네 변 맨 바깥 줄에 그림이 닿았는가 ──
    #    2×2 시트를 어긋나게 자르면 이웃 칸 조각이 가장자리에 띠로 남는다.
    if kind == "cut":
        edge = 0
        for x in range(w):
            edge += px[x, 0][3] > 0
            edge += px[x, h - 1][3] > 0
        for y in range(h):
            edge += px[0, y][3] > 0
            edge += px[w - 1, y][3] > 0
        if edge / (2 * (w + h)) > 0.25:
            warns.append("가장자리에 그림이 닿았다 — 시트에서 어긋나게 잘렸는지, "
                         "주제가 여백을 넘었는지 확인")

    # ── 워터마크 의심: 오른쪽 아래 12% 구석 ──
    cx, cy = int(w * 0.88), int(h * 0.88)
    corner = [px[x, y] for y in range(cy, h) for x in range(cx, w) if px[x, y][3] > 0]
    if corner and len(corner) > 16:
        bright = sum(1 for c in corner if c[0] > 200 and c[1] > 200 and c[2] > 200)
        if bright / len(corner) > 0.5:
            warns.append("오른쪽 아래 구석이 밝다 — 생성 도구 워터마크인지 확인")

    return errors, warns


def sheet(folder, files, size):
    """검수용 시트 — 표시 크기(48)와 원본 축소를 두 줄로 붙인다."""
    from PIL import ImageDraw
    small, cols, label = 48, 12, 12
    cell = max(small, 96) + 8
    rows = (len(files) + cols - 1) // cols
    out = Image.new("RGBA", (cols * cell, rows * (cell + small + label + 8)), (40, 40, 48, 255))
    d = ImageDraw.Draw(out)
    for i, (name, path) in enumerate(files):
        im = Image.open(path).convert("RGBA")
        x, y = (i % cols) * cell, (i // cols) * (cell + small + label + 8)
        out.alpha_composite(im.resize((cell - 8, cell - 8), Image.NEAREST), (x + 4, y + 4))
        out.alpha_composite(im.resize((small, small), Image.NEAREST), (x + 4, y + cell))
        d.text((x + 4, y + cell + small + 2), name[-16:], fill=(230, 230, 230, 255))
    path = os.path.join(ROOT, "Docs", "art_review", f"sheet_{folder.replace('/', '_')}.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path)
    return path


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    make_sheet = "--sheet" in sys.argv
    only = args[0].replace("\\", "/") if args else None

    total_err = total_warn = total_ok = 0

    if not only or only.lower() in ("brand", "브랜드"):
        print("[브랜드 4장]")
        for rel, (size, kind) in BRAND.items():
            path = os.path.join(ROOT, *rel.split("/"))
            if not os.path.isfile(path):
                print(f"  ERROR  {rel}: 파일 없음")
                total_err += 1
                continue
            errors, warns = check(path, rel, size, kind)
            for e in errors:
                print(f"  ERROR  {os.path.basename(rel)}: {e}")
            for w in warns:
                print(f"  warn   {os.path.basename(rel)}: {w}")
            total_err += len(errors)
            total_warn += len(warns)
            if not errors:
                total_ok += 1

    for folder, (size, kind) in RULES.items():
        if only and not folder.endswith(only) and only not in folder:
            continue
        d = os.path.join(TEX, *folder.split("/"))
        if not os.path.isdir(d):
            print(f"[{folder}] 폴더 없음 — 아직 안 만들었으면 정상")
            continue

        files = []
        print(f"\n[{folder}]")
        for f in sorted(os.listdir(d)):
            if not f.endswith(".png"):
                continue
            name = f[:-4]
            if name in SKIP or name in DONE:
                continue
            path = os.path.join(d, f)
            files.append((name, path))
            errors, warns = check(path, name, size, kind)
            for e in errors:
                print(f"  ERROR  {name}: {e}")
            for w in warns:
                print(f"  warn   {name}: {w}")
            total_err += len(errors)
            total_warn += len(warns)
            if not errors:
                total_ok += 1

        if make_sheet and files:
            print(f"  시트: {sheet(folder, files, size)}")

    print(f"\n통과 {total_ok} · 에러 {total_err} · 경고 {total_warn}")
    sys.exit(1 if total_err else 0)


if __name__ == "__main__":
    main()
