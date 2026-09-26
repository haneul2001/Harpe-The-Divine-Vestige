# Tiled 지형(wang) 세트 — 모서리(corner) 방식 오토타일.
#
# 팩의 벽·구덩이·바닥 전환은 전부 "corner" 지형이다. 셀 마스크(이 칸은 벽이다)를 주면
# 각 칸의 네 모서리 값을 구해 wangid가 맞는 타일을 고른다.
# 모서리 점은 그 점에 닿는 네 칸 중 하나라도 마스크에 있으면 지형으로 본다 —
# 즉 마스크 바깥으로 반 칸 두께의 테두리 타일이 더 깔린다 (Tiled 지형 붓과 같은 결과).
import re, random

class WangSet:
    def __init__(self, name, tiles):
        self.name = name
        self.by_id = {}          # (TR, BR, BL, TL) → [tileid...]
        for tid, wid in tiles:
            v = [int(x) for x in wid.split(',')]
            key = (v[1], v[3], v[5], v[7])
            self.by_id.setdefault(key, []).append(tid)

def load_wangsets(tsx_path):
    s = open(tsx_path, encoding='utf-8', errors='ignore').read()
    out = {}
    for ws in re.finditer(r'<wangset name="([^"]*)" type="(\w+)" tile="(-?\d+)">(.*?)</wangset>', s, re.S):
        if ws.group(2) != 'corner': continue
        tiles = [(int(a), b) for a, b in re.findall(r'<wangtile tileid="(\d+)" wangid="([^"]+)"', ws.group(4))]
        out[ws.group(1)] = WangSet(ws.group(1), tiles)
    return out

def paint_corner(mask, wangset, rng=None, color=1, seed=0):
    """mask: set of (x,y). 반환 {(x,y): tileid} — 모서리가 하나라도 지형인 칸 전부.
    변형 타일은 칸 좌표로 정해진 난수로 고른다 — 이웃 칸이 바뀌어도 이 칸 그림은 그대로여야
    문을 막은 그림(plug)을 차이로 뽑을 수 있다."""
    pts = set()
    for (x, y) in mask:
        pts |= {(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)}
    cells = set()
    for (px, py) in pts:
        cells |= {(px - 1, py - 1), (px, py - 1), (px - 1, py), (px, py)}
    out = {}
    for (x, y) in cells:
        key = (color if (x + 1, y) in pts else 0,      # TR
               color if (x + 1, y + 1) in pts else 0,  # BR
               color if (x, y + 1) in pts else 0,      # BL
               color if (x, y) in pts else 0)          # TL
        if key == (0, 0, 0, 0): continue
        cands = wangset.by_id.get(key)
        if not cands: continue
        out[(x, y)] = cands[cell_rng(seed, x, y).randrange(len(cands))]
    return out

def cell_rng(seed, x, y, salt=0):
    return random.Random((seed * 1000003 + salt * 7919 + (y + 1000) * 4099 + (x + 1000)) & 0xFFFFFFFF)
