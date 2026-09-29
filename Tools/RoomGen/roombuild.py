# 방 하나를 "설계 → 문 전부 뚫린 판 → 막힌 문용 벽 그림(plug) → JSON"까지 만든다.
#
# 문은 그 크기의 방이 가질 수 있는 자리 전부에 뚫어 둔다. 생성기가 이웃이 없는 문을 벽으로 막을 때는
# 여기서 그려 둔 plug 스프라이트를 덮는다 — 문 조합마다 프리팹을 따로 만들 필요가 없다.
import os
from PIL import Image
from roomgen import Room

CELL_W, CELL_H = 20, 11
PROJECT = r'C:/GitHub/Harpe The Divine Vestige'
PLUG_DIR = 'Assets/Haneul_Branch/Art/RoomPlugs'

def all_doors(span):
    sx, sy = span
    d = []
    for s in range(sx): d += [('up', s), ('down', s)]
    for s in range(sy): d += [('left', s), ('right', s)]
    return d

def door_cells(span, d, sub):
    """문 구멍이 차지하는 (col, row) — 격자 좌표(위가 0행).
    구멍은 6칸. 모서리 지형이라 양 끝 칸은 벽이 반 칸씩 흘러들어 실제로 보이는 트임은 5칸 —
    Depths 보스 게이트(가로 4.9칸, 세로 바닥 자국 4.6칸)와 맞는다. 옆문은 3~8행: 위쪽 벽 앞면 바로 아래부터."""
    w, h = CELL_W * span[0], CELL_H * span[1]
    if sub == -1:   # 변 한가운데 (위·아래 문만)
        cols = range(w // 2 - 3, w // 2 + 3)
        return [(c, 0 if d == 'up' else h - 1) for c in cols]
    if d == 'up':    return [(sub * CELL_W + c, 0) for c in range(7, 13)]
    if d == 'down':  return [(sub * CELL_W + c, h - 1) for c in range(7, 13)]
    row = h - 1 - (sub * CELL_H + 5)          # 칸의 5행 → 3~8행
    rows = range(row - 2, row + 4)
    if d == 'left':  return [(0, r) for r in rows]
    return [(w - 1, r) for r in rows]

def frame(span, interior, doors):
    """바깥 한 줄 벽 + 문 구멍 + 안쪽 설계. interior: 줄 목록(가로 w-2, 세로 h-2)."""
    w, h = CELL_W * span[0], CELL_H * span[1]
    rows = [['#'] * w for _ in range(h)]
    for y in range(1, h - 1):
        line = interior[y - 1] if y - 1 < len(interior) else ''
        for x in range(1, w - 1):
            c = line[x - 1] if x - 1 < len(line) else '.'
            rows[y][x] = c
    for d, sub in doors:
        for (x, y) in door_cells(span, d, sub): rows[y][x] = 'D'
        # 옆문: 구멍 바로 위 두 칸의 벽 기둥을 끊는다. 기둥 끝(끝막이+앞면 두 줄)이 구멍 위로 3칸 튀어나와
        # 통로가 좁고 내려간 창살 자국이 벽을 뚫고 지나가 보였다. 앞면 타일은 규칙이 그대로 달고 벽 타일맵에 남아 막는다
        if d in ('left', 'right') and sub >= 0:
            x = 0 if d == 'left' else CELL_W * span[0] - 1
            top = min(y for (_, y) in door_cells(span, d, sub))
            for y in (top - 2, top - 1):
                if 0 < y and rows[y][x] == '#': rows[y][x] = 'd'
    return '\n'.join(''.join(r) for r in rows)

def door_local(span, d, sub):
    """Door 오브젝트의 방 기준 좌표 (임포터와 같은 식)."""
    hw, hh = CELL_W * span[0] * 0.5, CELL_H * span[1] * 0.5
    if sub == -1:    return (0.0, hh - 0.5 if d == 'up' else -hh + 0.5)
    if d == 'up':    return (-hw + 10 + sub * 20, hh - 0.5)
    if d == 'down':  return (-hw + 10 + sub * 20, -hh + 0.5)
    # 옆문 구멍(3~8행)의 가운데는 칸 중심보다 반 칸 아래다 (위쪽 벽이 3줄이라 바닥이 아래로 치우침)
    if d == 'left':  return (-hw + 0.5, -hh + 5.0 + sub * 11)
    return (hw - 0.5, -hh + 5.0 + sub * 11)

def build(name, span, interior, seed=0, spawns=None, kind='Normal', out_dir='out', props=None, doors=None, **room_kw):
    """props: [(스프라이트 png 경로(프로젝트 상대), x, y)] — 방 좌표, 발 기준 가운데. doors: 문 목록(기본은 전부)."""
    doors = doors if doors is not None else all_doors(span)
    open_room = Room(name, frame(span, interior, doors), seed=seed, **room_kw)
    open_room.props = list(props or [])
    base = open_room.render()
    open_cells, (ox, oy) = open_room.unity_cells()
    open_walls = {(c[0], c[1]) for c in open_cells['Wall']}
    open_map = {tm: {(c[0], c[1]): (c[2], c[3]) for c in cells} for tm, cells in open_cells.items()}
    patches = {}
    h = open_room.h
    plugs = {}
    os.makedirs(f'{PROJECT}/{PLUG_DIR}', exist_ok=True)
    for d, sub in doors:
        closed_room = Room(name, frame(span, interior, [x for x in doors if x != (d, sub)]), seed=seed, **room_kw)
        closed_room.props = open_room.props
        closed = closed_room.render()
        # 두 그림이 다른 픽셀 범위 = 이 문을 막았을 때 바뀌는 자리
        diff = Image.eval(Image.new('L', base.size, 0), lambda v: v)
        a, b = base.convert('RGB'), closed.convert('RGB')
        import numpy as np
        m = (np.array(a) != np.array(b)).any(axis=2)
        ys, xs = np.where(m)
        if len(xs) == 0:
            # 그림 차이가 없어도(불투명한 끝막이가 바닥을 덮는 팩) 구멍은 막아야 한다 — 문 칸 + 위쪽 문이면 앞면 두 줄
            cs = door_cells(span, d, sub)
            xs = np.array([c[0] * 32 for c in cs] + [c[0] * 32 + 31 for c in cs])
            ys = np.array([c[1] * 32 for c in cs] + [c[1] * 32 + 31 + (64 if d == 'up' else 0) for c in cs])
        x0, y0, x1, y1 = xs.min() // 32 * 32, ys.min() // 32 * 32, (xs.max() // 32 + 1) * 32, (ys.max() // 32 + 1) * 32
        crop = closed.crop((x0, y0, x1, y1))
        fn = f'{name}_{d}{sub}.png'
        crop.save(f'{PROJECT}/{PLUG_DIR}/{fn}')
        # 스프라이트 중심의 방 좌표. 타일 칸 (cx, cy)는 세계 y [cy+0.5, cy+1.5]에 걸친다 (Grid가 0.5 올라가 있다)
        cx = ox + (x0 + x1) / 2 / 32
        cy = oy + 0.5 + h - (y0 + y1) / 2 / 32
        dx, dy = door_local(span, d, sub)
        # 충돌: 문을 막았을 때 새로 생긴 벽 타일 칸들을 덮는 사각형. 칸 (cx, cy)는 세계 x [cx, cx+1], y [cy+0.5, cy+1.5]
        closed_cells = closed_room.unity_cells()[0]
        new_walls = [c for c in {(c[0], c[1]) for c in closed_cells['Wall']} if c not in open_walls]
        # 막힌 문은 그림 대신 이 타일들로 바꿔 끼운다 — 주변 벽과 같은 타일이라 이음새가 안 보인다 ('' = 지움)
        pl = []
        for tm in open_map:
            closed_map = {(c[0], c[1]): (c[2], c[3]) for c in closed_cells.get(tm, [])}
            for key in set(open_map[tm]) | set(closed_map):
                a, b = open_map[tm].get(key), closed_map.get(key)
                if a == b: continue
                pl.append({'m': tm, 'x': key[0], 'y': key[1], 'a': b[0] if b else '', 'f': b[1] if b else 0})
        patches[(d, sub)] = pl
        if new_walls:
            wx0 = min(c[0] for c in new_walls); wx1 = max(c[0] for c in new_walls) + 1
            wy0 = min(c[1] for c in new_walls) + 0.5; wy1 = max(c[1] for c in new_walls) + 1.5
            col = ((wx0 + wx1) / 2 - dx, (wy0 + wy1) / 2 - dy, wx1 - wx0, wy1 - wy0)
        else:
            col = (0.0, 0.0, 0.0, 0.0)
        plugs[(d, sub)] = (f'{PLUG_DIR}/{fn}', round(cx - dx, 3), round(cy - dy, 3)) + tuple(round(v, 3) for v in col)
    os.makedirs(out_dir, exist_ok=True)
    open_room.to_json(f'{out_dir}/{name}.json', doors, spawns, kind, span, plugs, patches)
    return open_room, plugs
