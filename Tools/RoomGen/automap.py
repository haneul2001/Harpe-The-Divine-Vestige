# Tiled 1.9 자동 매핑(automapping) 해석기.
#
# RafaelMatos 팩은 벽·난간 오토타일을 Tiled 규칙 맵(.tmx)으로 배포한다. Unity에는 그 규칙이 없으므로
# 여기서 규칙을 직접 돌려 "연필 타일로 그린 방" → "완성 타일" 변환을 한다.
# 셀은 (타일셋 이름, 로컬 id, 뒤집기 플래그) 튜플로 다룬다 — 맵마다 firstgid가 달라서 gid로는 비교할 수 없다.
import os, re, random
from tiled import TiledMap, FLIP_H, FLIP_V, FLIP_D, MASK

SPECIAL = 'automap-tiles.tsx'
NEGATE, IGNORE, NONEMPTY, EMPTY, OTHER = 0, 1, 2, 3, 4   # Tiled 내장 automap-tiles.tsx의 id

def norm(tmap, gid):
    """gid → (타일셋, 로컬id, 플래그). 0이면 None."""
    if not gid: return None
    ts, name, lid = tmap.owner(gid)
    return (name, lid, gid & ~MASK)

def is_special(cell):
    return cell is not None and cell[0] == SPECIAL

class Rule:
    def __init__(self, cells):
        self.cells = cells                 # 규칙 맵 좌표 집합
        self.x0 = min(x for x, _ in cells); self.y0 = min(y for _, y in cells)
        self.inputs = {}                   # target → list of {(dx,dy): cell}  (input_ 레이어마다 하나)
        self.inputnots = {}
        self.outputs = {}                  # target → {index: [ {(dx,dy): cell} ... ]}
        self.probability = 1.0
        self.disabled = False
        self.mod = (1, 1); self.offset = (0, 0)
        self.no_overlap = False
        self.out_probs = {}                # (target, index) → 확률 가중치

    def used_tiles(self, target):
        s = set()
        for lay in self.inputs.get(target, []) + self.inputnots.get(target, []):
            for c in lay.values():
                if c is not None and not is_special(c): s.add(c)
        return s

class RuleMap:
    def __init__(self, tmx_path):
        self.tmap = TiledMap(tmx_path)
        self.path = tmx_path
        s = open(tmx_path, encoding='utf-8', errors='ignore').read()
        head = s[:s.find('<layer')] if '<layer' in s else s
        self.props = dict(re.findall(r'<property name="([^"]+)"(?: type="\w+")? value="([^"]*)"', head))
        self.match_in_order = self.props.get('MatchInOrder', 'false') == 'true'
        self.delete_tiles = self.props.get('DeleteTiles', 'false') == 'true'
        self.auto_empty = self.props.get('AutoEmpty', self.props.get('StrictEmpty', 'false')) == 'true'
        self.match_outside = self.props.get('MatchOutsideMap', 'false') == 'true'
        w, h = self.tmap.w, self.tmap.h

        # 레이어 분류 + 레이어별 Probability 속성
        self.layers = []   # (kind, index, target, grid dict {(x,y): cell}, prob)
        for m in re.finditer(r'<layer [^>]*name="([^"]+)"[^>]*>(.*?)</layer>', s, re.S):
            name, body = m.group(1), m.group(2)
            lm = re.match(r'(input|inputnot|output)(\d*)_(.+)', name)
            if not lm: continue
            kind, idx, target = lm.group(1), lm.group(2), lm.group(3)
            prob = 1.0
            pm = re.search(r'<property name="Probability"[^>]*value="([\d.]+)"', body)
            if pm: prob = float(pm.group(1))
            vals = self.tmap.layer(name)
            grid = {}
            for i, gid in enumerate(vals):
                if gid: grid[(i % w, i // w)] = norm(self.tmap, gid)
            self.layers.append((kind, idx, target, grid, prob))

        # 규칙 영역: 모든 input/output 레이어의 비어 있지 않은 칸 → 8방향 연결 성분
        filled = set()
        for _, _, _, grid, _ in self.layers: filled |= set(grid.keys())
        self.rules = []
        seen = set()
        for p in sorted(filled, key=lambda c: (c[1], c[0])):
            if p in seen: continue
            comp = []; st = [p]; seen.add(p)
            while st:
                x, y = st.pop(); comp.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        q = (x + dx, y + dy)
                        if q in filled and q not in seen: seen.add(q); st.append(q)
            self.rules.append(Rule(set(comp)))
        # Tiled 적용 순서: y 작은 것 먼저, 같으면 x
        self.rules.sort(key=lambda r: (r.y0, r.x0))

        for rule in self.rules:
            for kind, idx, target, grid, prob in self.layers:
                sub = {(x - rule.x0, y - rule.y0): grid[(x, y)] for (x, y) in rule.cells if (x, y) in grid}
                if not sub: continue
                if kind == 'input': rule.inputs.setdefault(target, []).append(sub)
                elif kind == 'inputnot': rule.inputnots.setdefault(target, []).append(sub)
                else:
                    rule.outputs.setdefault(target, {}).setdefault(idx, []).append(sub)
                    rule.out_probs[(target, idx)] = prob

        # rule_options: 사각형 오브젝트가 덮는 규칙에 속성 적용
        tw, th = self.tmap.tw, self.tmap.th
        for om in re.finditer(r'<objectgroup [^>]*name="rule_options"([^>]*)>(.*?)</objectgroup>', s, re.S):
            ox = float((re.search(r'offsetx="([\d.\-]+)"', om.group(1)) or [None, 0])[1])
            oy = float((re.search(r'offsety="([\d.\-]+)"', om.group(1)) or [None, 0])[1])
            for o in re.finditer(r'<object [^>]*x="([\d.\-]+)" y="([\d.\-]+)" width="([\d.]+)" height="([\d.]+)"[^>]*>(.*?)</object>', om.group(2), re.S):
                x, y, w_, h_ = (float(o.group(i)) for i in range(1, 5))
                x += ox; y += oy
                props = dict(re.findall(r'<property name="([^"]+)"(?: type="\w+")? value="([^"]*)"', o.group(5)))
                for rule in self.rules:
                    hit = any(x < cx * tw + tw and x + w_ > cx * tw and y < cy * th + th and y + h_ > cy * th for cx, cy in rule.cells)
                    if not hit: continue
                    if 'Probability' in props: rule.probability = float(props['Probability'])
                    if 'Disabled' in props: rule.disabled = props['Disabled'] == 'true'
                    if 'ModX' in props or 'ModY' in props:
                        rule.mod = (int(props.get('ModX', 1)), int(props.get('ModY', 1)))
                    if 'OffsetX' in props or 'OffsetY' in props:
                        rule.offset = (int(props.get('OffsetX', 0)), int(props.get('OffsetY', 0)))
                    if 'NoOverlappingOutput' in props: rule.no_overlap = props['NoOverlappingOutput'] == 'true'
        # 규칙 맵 자체 속성으로 준 기본값
        for rule in self.rules:
            if 'Probability' in self.props and rule.probability == 1.0: rule.probability = float(self.props['Probability'])

    # ── 적용 ──
    def apply(self, layers, w, h, seed=0):
        """layers: {target: {(x,y): cell}} — 제자리에서 고친다.
        난수는 (seed, 규칙, 칸)으로 정해진다 — 다른 자리의 지형이 바뀌어도 이 자리 결과는 같다."""
        from wang import cell_rng
        for target in {t for r in self.rules for t in list(r.inputs) + list(r.inputnots) + list(r.outputs)}:
            layers.setdefault(target, {})
        snapshot = {t: dict(g) for t, g in layers.items()} if not self.match_in_order else None

        for ri, rule in enumerate(self.rules):
            if rule.disabled: continue
            src = layers if self.match_in_order else snapshot
            matches = []
            for y in range(-rule_extent(rule)[1], h):
                for x in range(-rule_extent(rule)[0], w):
                    if (x - rule.offset[0]) % rule.mod[0] or (y - rule.offset[1]) % rule.mod[1]: continue
                    if self._match(rule, src, x, y, w, h): matches.append((x, y))
            written = set()
            for x, y in matches:
                rng = cell_rng(seed, x, y, ri + 1)
                if rule.probability < 1.0 and rng.random() > rule.probability: continue
                cells_out = self._pick_output(rule, rng)
                if cells_out is None: continue
                if rule.no_overlap:
                    pos = {(x + dx, y + dy) for _, sub in cells_out for (dx, dy) in sub}
                    if pos & written: continue
                    written |= pos
                if self.delete_tiles:
                    for target in rule.outputs:
                        for (dx, dy) in rule.cells_rel():
                            layers[target].pop((x + dx, y + dy), None)
                for target, sub in cells_out:
                    for (dx, dy), cell in sub.items():
                        p = (x + dx, y + dy)
                        if not (0 <= p[0] < w and 0 <= p[1] < h): continue
                        if is_special(cell):
                            if cell[1] == EMPTY: layers[target].pop(p, None)
                            continue
                        layers[target][p] = cell

    def _pick_output(self, rule, rng):
        # 인덱스별 후보를 모아 확률 가중치로 하나 고른다. 인덱스 없는 출력은 항상 적용.
        always = [(t, sub) for t, byidx in rule.outputs.items() for idx, subs in byidx.items() if idx == '' for sub in subs]
        indexed = {}
        for t, byidx in rule.outputs.items():
            for idx, subs in byidx.items():
                if idx != '': indexed.setdefault(idx, []).extend((t, sub) for sub in subs)
        chosen = []
        if indexed:
            idxs = list(indexed.keys())
            weights = [max(rule.out_probs.get((t, i), 1.0) for t, _ in indexed[i]) for i in idxs]
            pick = rng.choices(idxs, weights=weights, k=1)[0]
            chosen = indexed[pick]
        return always + chosen

    def _match(self, rule, src, x, y, w, h):
        for target in set(list(rule.inputs) + list(rule.inputnots)):
            grid = src.get(target, {})
            used = rule.used_tiles(target)
            ins = rule.inputs.get(target, []); nots = rule.inputnots.get(target, [])
            # 규칙이 조건을 둔 모든 상대 좌표
            coords = set()
            for lay in ins + nots: coords |= set(lay.keys())
            for (dx, dy) in coords:
                px, py = x + dx, y + dy
                inside = 0 <= px < w and 0 <= py < h
                if not inside and not self.match_outside: return False
                cur = grid.get((px, py))
                pos_conds, neg_conds = [], []
                negate = False
                for lay in ins:
                    c = lay.get((dx, dy))
                    if c is None: continue
                    if is_special(c) and c[1] == NEGATE: negate = True; continue
                    pos_conds.append(c)
                for lay in nots:
                    c = lay.get((dx, dy))
                    if c is None: continue
                    if is_special(c) and c[1] == NEGATE: negate = True; continue
                    neg_conds.append(c)
                if negate: pos_conds, neg_conds = neg_conds, pos_conds
                pos_conds = [c for c in pos_conds if not (is_special(c) and c[1] == IGNORE)]
                neg_conds = [c for c in neg_conds if not (is_special(c) and c[1] == IGNORE)]
                if pos_conds and not any(self._cond(c, cur, used) for c in pos_conds): return False
                if any(self._cond(c, cur, used) for c in neg_conds): return False
            if self.auto_empty:
                for (dx, dy) in rule.cells_rel():
                    if (dx, dy) in coords: continue
                    if grid.get((x + dx, y + dy)) is not None: return False
        return True

    @staticmethod
    def _cond(c, cur, used):
        if is_special(c):
            k = c[1]
            if k == EMPTY: return cur is None
            if k == NONEMPTY: return cur is not None
            if k == OTHER: return cur is not None and cur not in used
            return True
        return cur == c

def rule_extent(rule):
    xs = [dx for (dx, dy) in rule.cells_rel()]; ys = [dy for (dx, dy) in rule.cells_rel()]
    return (max(xs), max(ys))

Rule.cells_rel = lambda self: [(x - self.x0, y - self.y0) for (x, y) in self.cells]

def load_rule_list(rules_txt):
    base = os.path.dirname(rules_txt)
    out = []
    for line in open(rules_txt, encoding='utf-8', errors='ignore'):
        line = line.strip()
        if not line or line.startswith('#'): continue
        p = os.path.join(base, line.replace('\\', '/'))
        if os.path.exists(p): out.append(p)
        else: print('규칙 파일 없음:', p)
    return out

_rule_cache = {}
def run(rule_paths, layers, w, h, seed=0):
    for i, p in enumerate(rule_paths):
        if p not in _rule_cache: _rule_cache[p] = RuleMap(p)
        _rule_cache[p].apply(layers, w, h, seed * 31 + i)
    return layers

def map_layers(tmap):
    """TiledMap의 타일 레이어를 {name: {(x,y): cell}}로."""
    out = {}
    for name, vals in tmap.layers:
        g = {}
        for i, gid in enumerate(vals):
            if gid: g[(i % tmap.w, i // tmap.w)] = norm(tmap, gid)
        out[name] = g
    return out
