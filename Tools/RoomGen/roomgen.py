# 방 설계도(글자 격자) → 완성 타일 레이어 → Unity용 JSON + 미리보기 PNG.
#
# 격자 한 글자가 한 칸(1 유닛)이다. Unity 방 좌표계는 y가 위, 여기 격자는 위 줄이 y 큰 쪽이다.
#   '#' 벽 윗면(밴드)   '.' 바닥   'D' 문 자리(바닥, 벽 밴드에 뚫린 구멍)   'P' 구덩이
#   'r' 난간(B.1 연필 — 벽·구덩이 가장자리에 그린다)   ' ' 밖(벽으로 채움)
#
# 벽은 팩 방식대로 두 단계다: 지형 붓(wang) → 연필 타일, 규칙 맵(automap) → 앞면이 딸린 완성 타일.
# 규칙이 방 밖을 들여다보므로 여백을 두고 돌린 뒤 방 크기로 자른다.
import os, re, json, random
from tiled import Tileset, TiledMap
from wang import load_wangsets, paint_corner, cell_rng
from automap import load_rule_list, run

PROJECT = r'C:/GitHub/Harpe The Divine Vestige'
ROOT = PROJECT + '/Assets/ThirdParty/RafaelMatos'
PAD = 4

class Pack:
    """팩 하나 — 타일셋·지형 세트·규칙·Unity 타일 에셋 경로 해석."""
    def __init__(self, folder, rules_txt='rules.txt'):
        self.folder = folder
        self.root = f'{ROOT}/{folder}'
        self.ted = f'{self.root}/TiledMap Editor'
        tsx_dir = self.ted + '/Tilesets'
        self.tilesets = {}
        for f in os.listdir(tsx_dir):
            if f.endswith('.tsx'):
                ts = Tileset(os.path.join(tsx_dir, f))
                if ts.columns > 0: self.tilesets[f] = ts
        self.wang = {}
        for f, ts in self.tilesets.items():
            self.wang.update(load_wangsets(ts.path))
        self.rules = load_rule_list(f'{self.ted}/{rules_txt}')
        self._idx = {}   # tsx 이름 → {(col,row): 스프라이트 번호}
        self._prob = {}
    def sprite_index(self, tsx_name):
        if tsx_name in self._idx: return self._idx[tsx_name]
        ts = self.tilesets[tsx_name]
        meta = open(ts.image_path + '.meta', encoding='utf-8', errors='ignore').read()
        h = ts.image.height
        table = {}
        # 스프라이트 이름 ↔ rect 를 짝지어 (col,row) → 이름. 이름 규칙은 팩마다 달라서(base_N, st2u_x..y..) 이름 자체를 쓴다
        pat = r'      name: ([^\n]+)\n      rect:\n        serializedVersion: 2\n        x: (\d+)\n        y: (\d+)'
        # 한 그림에 두 벌(base_N, st2u_…)이 같이 있으면 먼저 나오는 base_N을 쓴다 — Crypt Tile 에셋 이름이 그쪽이다
        for n, x, y in re.findall(pat, meta):
            table.setdefault((int(x) // ts.tw, (h - ts.th - int(y)) // ts.th), n.strip())
        self._idx[tsx_name] = table
        return table
    def asset_path(self, cell):
        """(tsx, 로컬id, 플래그) → Unity Tile 에셋 경로 (프로젝트 상대). 없으면 None."""
        tsx, lid, _ = cell
        ts = self.tilesets.get(tsx)
        if ts is None: return None
        base = os.path.splitext(os.path.basename(ts.image_path))[0]
        rel = os.path.relpath(ts.image_path, self.root).replace('\\', '/')
        col, row = lid % ts.columns, lid // ts.columns
        name = self.sprite_index(tsx).get((col, row))
        if name is None: return None
        # Crypt 지형 타일만 팩이 만들어 둔 Tile 에셋(tile palette/<png>/<png>_N.asset)을 쓴다.
        # 나머지(소품 아틀라스, 다른 팩)는 "그림#스프라이트" 꼴로 넘기고 임포터가 Tile 에셋을 만들어 쓴다 —
        # 팩마다 Tile 폴더 규칙이 제각각이라 그걸 다 맞추는 것보다 낫다.
        if self.folder == 'ERW-Crypt' and 'atlas' not in base.lower():
            idx = int(name.rsplit('_', 1)[1]) if '_' in name and name.rsplit('_', 1)[1].isdigit() else None
            if idx is not None:
                return f'Assets/ThirdParty/RafaelMatos/{self.folder}/{os.path.dirname(rel)}/tile palette/{base.lower()}/{base}_{idx}.asset'
        return f'Assets/ThirdParty/RafaelMatos/{self.folder}/{rel}#{name}'
        return f'Assets/ThirdParty/RafaelMatos/{self.folder}/{os.path.dirname(rel)}/tile palette/{base.lower()}/{base}_{idx}.asset'
    def full_tiles(self, wangset_name):
        """지형 세트의 '꽉 찬' 타일들과 확률 (tsx의 probability)."""
        ws = self.wang[wangset_name]
        ids = ws.by_id.get((1, 1, 1, 1), [])
        tsx = next(f for f, ts in self.tilesets.items() if any(True for _ in [0]) and self._owns(ts, wangset_name))
        if tsx not in self._prob:
            s = open(self.tilesets[tsx].path, encoding='utf-8', errors='ignore').read()
            self._prob[tsx] = {int(a): float(b) for a, b in re.findall(r'<tile id="(\d+)" probability="([\d.]+)"', s)}
        pr = self._prob[tsx]
        return tsx, [(t, pr.get(t, 1.0)) for t in ids]
    def _owns(self, ts, wangset_name):
        return wangset_name in load_wangsets(ts.path)

# 팩마다 벽·바닥이 어느 타일셋/지형 세트/규칙 레이어에 있는지. 벽 연필(blob) 배치는 세 팩이 같다.
THEMES = {
    'crypt':     dict(folder='ERW-Crypt', wall_layer='wall-1-2-3', wall_tsx='Tileset - wall 1.tsx', wall='wall-111',
                      floor='ground2 to ground1', patch='ground1 to ground2', walk=('wall-2', 'Tileset - wall 2.tsx'),
                      pit='balluster3', pit_layer='balluster-3-4', rail=('balluster-1', ('Tileset-Terrain.tsx', 1800))),
    'oldprison': dict(folder='ERW - Old Prison', wall_layer='wall-1', wall_tsx='Tileset - wall 1.tsx', wall='wall-1',
                      floor='whole', patch='grass/dirt', walk=None, pit=None, pit_layer=None, rail=None),
    'depths':    dict(folder='ERW - The Depths', wall_layer='plats', wall_tsx='The depths-Tileset-Platform1.tsx', wall='plat-1',
                      floor='ground-cave-tilesets', patch='tileset3-ground detail', walk=None, pit=None, pit_layer=None, rail=None),
    'cemetery':  dict(folder='ERW - Cemetery', wall_layer='wall-1', wall_tsx='wall 1.tsx', wall='wall-1',
                      floor='grass1 to grass2', patch='grass2 to grass1(no shade)', walk=None,
                      pit='hole-1', pit_layer='holes-1-2-3', rail=None),
    'sewers':    dict(folder='ERW - Sewers', wall_layer='walls', wall_tsx='Sewers-Tileset-wall-1.tsx', wall='wall-1',
                      floor='ground path', patch='ground path-transparency', walk=None, pit=None, pit_layer=None, rail=None),
}
_packs = {}
def pack_of(theme):
    t = THEMES[theme]
    if theme not in _packs: _packs[theme] = Pack(t['folder'])
    return _packs[theme]
def crypt(): return pack_of('crypt')

def weighted(rng, items):
    tot = sum(p for _, p in items)
    r = rng.random() * tot
    for t, p in items:
        r -= p
        if r <= 0: return t
    return items[-1][0]

class Room:
    def __init__(self, name, grid, theme='crypt', seed=0, blocks=None, **_ignored):
        self.name = name
        self.theme = THEMES[theme]
        self.pack = pack_of(theme)
        self.rows = [r for r in grid.strip('\n').split('\n')]
        self.h = len(self.rows); self.w = max(len(r) for r in self.rows)
        self.rows = [r.ljust(self.w) for r in self.rows]
        self.rng = random.Random(seed)
        self.seed = seed
        self.layers = None
        self.props = []      # (sprite_png_path, x, y) — 방 좌표(유닛), 스프라이트 바닥 중앙 기준
        # 아틀라스 소품 블록: (격자x, 격자y, 왼쪽위 타일id, 가로칸, 세로칸, 막는지)
        self.blocks = list(blocks or [])
        self.build()

    def at(self, x, y):
        return self.rows[y][x] if 0 <= x < self.w and 0 <= y < self.h else ' '

    def build(self):
        P = PAD; W, H = self.w + 2 * P, self.h + 2 * P
        pack, T = self.pack, self.theme
        floor_tsx, floors = pack.full_tiles(T['floor'])
        WL, PL = T['wall_layer'], T['pit_layer']
        layers = {'terrain0': {}, WL: {}}
        if PL: layers[PL] = {}
        if T['rail']: layers[T['rail'][0]] = {}
        wall_mask, pit_mask, rail, walk_mask = set(), set(), set(), set()
        for y in range(H):
            for x in range(W):
                c = self.at(x - P, y - P)
                if c == '#': wall_mask.add((x, y))          # ' '(방 밖)은 아무것도 아니다 — 벽으로 채우면 문 구멍이 안 뚫린다
                elif c == 'P' and PL: pit_mask.add((x, y))
                elif c == 'W' and T['walk']: walk_mask.add((x, y))
                else:
                    layers['terrain0'][(x, y)] = (floor_tsx, weighted(cell_rng(self.seed, x, y, 1), floors), 0)
                    if c == 'r' and T['rail']: rail.add((x, y))
        # 벽: 지형 붓 → 연필
        for p, t in paint_corner(wall_mask, pack.wang[T['wall']], seed=self.seed).items():
            layers[WL][p] = (T['wall_tsx'], t, 0)
        # 통로(벽2 윗면): 걸을 수 있는 띠. 규칙이 아래로 앞면을 달아 준다 — 구덩이 위 다리가 된다
        self.walk_mask = {(x - P, y - P) for (x, y) in walk_mask}
        if walk_mask:
            for p, t in paint_corner(walk_mask, pack.wang[T['walk'][0]], seed=self.seed + 7).items():
                layers[WL][p] = (T['walk'][1], t, 0)
        # 구덩이: 가장자리 지형 — 안쪽은 투명 타일, 둘레는 테두리
        if pit_mask:
            pit_tsx = next(f for f, ts in pack.tilesets.items() if pack._owns(ts, T['pit']))
            for p, t in paint_corner(pit_mask, pack.wang[T['pit']], seed=self.seed + 13).items():
                layers[PL][p] = (pit_tsx, t, 0)
            for p in pit_mask: layers['terrain0'].pop(p, None)
        # 난간 연필
        if rail:
            rl, pencil = T['rail']
            for p in rail: layers[rl][p] = (pencil[0], pencil[1], 0)
        # 바닥 변화: ',' ':' = 무늬 지형. 기본 바닥 위에 지형 붓으로 얹어 가장자리가 섞인다
        layers['terrain1'] = {}
        if T['patch']:
            patch_tsx = next(f for f, ts in pack.tilesets.items() if pack._owns(ts, T['patch']))
            for ch, salt in ((',', 21), (':', 22)):
                mask = {(x, y) for y in range(H) for x in range(W) if self.at(x - P, y - P) == ch}
                if not mask: continue
                for p, t in paint_corner(mask, pack.wang[T['patch']], seed=self.seed + salt).items():
                    if p in layers['terrain0'] or p in mask: layers['terrain1'][p] = (patch_tsx, t, 0)
        run(pack.rules, layers, W, H, seed=self.seed)
        # 문 구멍에 남은 벽 조각(끝막이·옆면)은 그림만 남기고 벽 타일맵에서 뺀다 —
        # 벽 타일맵은 콜라이더가 붙어서 이 조각들이 통로를 반쯤 막는다. 잠그는 건 Door의 Blocker가 한다.
        layers['wall-open'] = {}
        for y in range(self.h):
            for x in range(self.w):
                if self.at(x, y) != 'D': continue
                cells = [(x + P, y + P)]
                if y == 0: cells += [(x + P, y + P + 1), (x + P, y + P + 2)]     # 위쪽 문: 앞면 두 줄까지
                for p in cells:
                    if p in layers[WL]: layers['wall-open'][p] = layers[WL].pop(p)
        # 소품(아틀라스 블록): 격자 좌표 기준. 막는 것은 벽 타일맵으로, 장식은 오브젝트 타일맵으로
        layers['props-block'] = {}; layers['props-deco'] = {}
        atlas_name = next((f for f in pack.tilesets if 'atlas' in f.lower() and 'sprites' not in f.lower()), None)
        atlas = pack.tilesets.get(atlas_name)
        for (gx, gy, tid, bw, bh, blocking) in self.blocks:
            for by in range(bh):
                for bx in range(bw):
                    p = (gx + bx + P, gy + by + P)
                    layers['props-block' if blocking else 'props-deco'][p] = (atlas_name, tid + by * atlas.columns + bx, 0)
        # 방 크기로 자른다
        self.layers = {}
        for name, g in layers.items():
            self.layers[name] = {(x - P, y - P): c for (x, y), c in g.items() if P <= x < W - P and P <= y < H - P}

    # ── 출력 ──
    def unity_cells(self):
        """Unity 타일맵별 [(cx, cy, 에셋경로, flags)]. 원점: 가로 중앙, 세로 -6(20x11 방 규격과 같음)."""
        ox, oy = -self.w // 2, -6 if self.h == 11 else -(self.h // 2) - 1
        out = {'Ground': [], 'Wall': [], 'Object': [], 'Props': []}
        T = self.theme
        target = {'terrain0': 'Ground', 'terrain1': 'Ground', T['wall_layer']: 'Wall', 'wall-open': 'Object', 'props-block': 'Props', 'props-deco': 'Object'}
        if T['pit_layer']: target[T['pit_layer']] = 'Ground'
        if T['rail']: target[T['rail'][0]] = 'Wall'
        for name, g in self.layers.items():
            if name not in target: continue
            for (x, y), c in g.items():
                path = self.pack.asset_path(c)
                if path is None: continue
                tm = target[name]
                # 통로 윗면은 밟는 곳이라 바닥 타일맵으로 (앞면·난간은 벽 타일맵에 남아 막는다)
                if name == T['wall_layer'] and (x, y) in getattr(self, 'walk_mask', ()): tm = 'Ground'
                out[tm].append((x + ox, (self.h - 1 - y) + oy, path, c[2]))
        return out, (ox, oy)

    def to_json(self, path, doors, spawns=None, kind='Normal', span=(1, 1), plugs=None):
        """doors: [('up', 0), ('right', 0), ...]  plugs: {(dir, sub): (sprite_asset_path, x, y)}"""
        cells, origin = self.unity_cells()
        plugs = plugs or {}
        door_specs = []
        for d, sub in doors:
            pl = plugs.get((d, sub))
            door_specs.append({'dir': d, 'sub': sub, 'plug': pl[0] if pl else '', 'plugX': pl[1] if pl else 0, 'plugY': pl[2] if pl else 0})
        # Unity JsonUtility가 읽을 수 있게 전부 객체 배열로 (딕셔너리·혼합 배열 불가)
        data = {'name': self.name, 'kind': kind, 'width': self.w, 'height': self.h,
                'originX': origin[0], 'originY': origin[1], 'spanX': span[0], 'spanY': span[1],
                'doors': door_specs,
                'spawns': [{'x': x, 'y': y} for x, y in (spawns or [])],
                'tilemaps': [{'name': k, 'cells': [{'x': x, 'y': y, 'a': a, 'f': f} for x, y, a, f in v]} for k, v in cells.items()],
                'props': [{'sprite': s, 'x': x, 'y': y} for s, x, y in self.props]}
        json.dump(data, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        return data

    def render(self, scale=1):
        from PIL import Image
        S = 32
        im = Image.new('RGBA', (self.w * S, self.h * S), (0, 0, 0, 255))
        T = self.theme
        order = ['terrain0', 'terrain1'] + ([T['pit_layer']] if T['pit_layer'] else []) + [T['wall_layer'], 'wall-open'] + ([T['rail'][0]] if T['rail'] else []) + ['props-deco', 'props-block']
        for name in order:
            for (x, y), c in self.layers.get(name, {}).items():
                ts = self.pack.tilesets.get(c[0]); t = ts.tile_image(c[1]) if ts else None
                if t is None: continue
                im.alpha_composite(t, (x * S, y * S - (t.height - S)))
        # 소품: 방 좌표(유닛, 발 기준 가운데) → 그림 좌표. 원점은 unity_cells와 같은 규칙
        ox, oy = self.unity_cells()[1]
        for s, x, y in self.props:
            path = s if os.path.isabs(s) else os.path.join(PROJECT, s)
            if not os.path.exists(path): continue
            p = Image.open(path).convert('RGBA')
            px = (x - ox) * S
            py = (self.h + oy + 0.5 - y) * S     # 세계 y → 그림 y (위가 0). 타일 칸은 세계 [cy+0.5, cy+1.5]
            im.alpha_composite(p, (int(px - p.width / 2), int(py - p.height)))
        if scale != 1: im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        return im
