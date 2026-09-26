# Tiled(.tmx/.tsx) 읽기 + 픽셀 렌더. RafaelMatos 팩 전용으로 단순하게 둔다.
import os, re
from PIL import Image, ImageDraw

FLIP_H, FLIP_V, FLIP_D = 0x80000000, 0x40000000, 0x20000000
MASK = 0x0FFFFFFF

class Tileset:
    def __init__(self, tsx_path):
        self.path = tsx_path
        s = open(tsx_path, encoding='utf-8', errors='ignore').read()
        m = re.search(r'<tileset [^>]*name="([^"]*)"[^>]*tilewidth="(\d+)" tileheight="(\d+)" tilecount="(\d+)" columns="(\d+)"', s)
        self.name, self.tw, self.th, self.count, self.columns = m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5))
        img = re.search(r'<image source="([^"]+)"', s)
        self.image_path = os.path.normpath(os.path.join(os.path.dirname(tsx_path), img.group(1))) if img and self.columns > 0 else None
        # 이미지 컬렉션(columns=0): id → 개별 파일
        self.collection = {}
        for tm in re.finditer(r'<tile id="(\d+)">\s*<image width="(\d+)" height="(\d+)" source="([^"]+)"', s):
            self.collection[int(tm.group(1))] = os.path.normpath(os.path.join(os.path.dirname(tsx_path), tm.group(4)))
        self._img = None
        self._cache = {}
    @property
    def image(self):
        if self._img is None and self.image_path:
            self._img = Image.open(self.image_path).convert('RGBA')
        return self._img
    def tile_image(self, local_id):
        if local_id in self._cache: return self._cache[local_id]
        if self.columns == 0:
            p = self.collection.get(local_id)
            im = Image.open(p).convert('RGBA') if p and os.path.exists(p) else None
        else:
            c, r = local_id % self.columns, local_id // self.columns
            im = self.image.crop((c*self.tw, r*self.th, (c+1)*self.tw, (r+1)*self.th))
        self._cache[local_id] = im
        return im

class TiledMap:
    def __init__(self, tmx_path):
        self.path = tmx_path
        s = open(tmx_path, encoding='utf-8', errors='ignore').read()
        m = re.search(r'<map [^>]*width="(\d+)" height="(\d+)" tilewidth="(\d+)" tileheight="(\d+)"', s)
        self.w, self.h, self.tw, self.th = map(int, m.groups())
        self.tilesets = []   # (firstgid, Tileset)
        for fg, src in re.findall(r'<tileset firstgid="(\d+)" source="([^"]+)"', s):
            p = os.path.normpath(os.path.join(os.path.dirname(tmx_path), src))
            self.tilesets.append((int(fg), Tileset(p) if os.path.exists(p) else None, os.path.basename(src)))
        self.layers = []     # (name, list[int]) 순서 유지
        for lm in re.finditer(r'<layer [^>]*name="([^"]+)"[^>]*>\s*<data encoding="csv">([^<]*)</data>', s):
            vals = [int(v) for v in lm.group(2).replace('\n', '').split(',') if v.strip()]
            self.layers.append((lm.group(1), vals))
        self.objects = []    # (gid, x, y, w, h)  — y는 바닥 기준(Tiled)
        for om in re.finditer(r'<objectgroup [^>]*name="([^"]+)"[^>]*>(.*?)</objectgroup>', s, re.S):
            for o in re.finditer(r'<object [^>]*gid="(\d+)"[^>]*x="([\d.\-]+)" y="([\d.\-]+)"(?: width="([\d.]+)" height="([\d.]+)")?', om.group(2)):
                self.objects.append((int(o.group(1)), float(o.group(2)), float(o.group(3)),
                                     float(o.group(4) or 0), float(o.group(5) or 0)))
    def layer(self, name):
        for n, v in self.layers:
            if n == name: return v
        return None
    def owner(self, gid):
        g = gid & MASK
        best = None
        for fg, ts, nm in self.tilesets:
            if fg <= g: best = (fg, ts, nm)
        if best is None: return None, None, g
        return best[1], best[2], g - best[0]
    def render(self, layer_names=None, scale=1, objects=True, grid=False):
        out = Image.new('RGBA', (self.w*self.tw, self.h*self.th), (0, 0, 0, 255))
        for name, vals in self.layers:
            if layer_names and name not in layer_names: continue
            for i, gid in enumerate(vals):
                if not gid: continue
                ts, nm, lid = self.owner(gid)
                if ts is None: continue
                im = ts.tile_image(lid)
                if im is None: continue
                if gid & FLIP_D: im = im.transpose(Image.TRANSPOSE)
                if gid & FLIP_H: im = im.transpose(Image.FLIP_LEFT_RIGHT)
                if gid & FLIP_V: im = im.transpose(Image.FLIP_TOP_BOTTOM)
                x, y = (i % self.w)*self.tw, (i // self.w)*self.th
                out.alpha_composite(im, (x, y - (im.height - self.th)))
        if objects:
            for gid, x, y, w, h in self.objects:
                ts, nm, lid = self.owner(gid)
                if ts is None: continue
                im = ts.tile_image(lid)
                if im is None: continue
                if w and h and (im.width != int(w) or im.height != int(h)):
                    im = im.resize((max(1, int(w)), max(1, int(h))), Image.NEAREST)
                out.alpha_composite(im, (int(x), int(y) - im.height))
        if grid:
            d = ImageDraw.Draw(out)
            for x in range(0, out.width, self.tw): d.line([(x, 0), (x, out.height)], fill=(255, 255, 255, 40))
            for y in range(0, out.height, self.th): d.line([(0, y), (out.width, y)], fill=(255, 255, 255, 40))
        if scale != 1:
            out = out.resize((int(out.width*scale), int(out.height*scale)), Image.NEAREST)
        return out

def labeled_tileset(ts, scale=2, max_rows=None):
    """타일셋 그림에 로컬 id를 적어 넣은 도판."""
    cols, rows = ts.columns, (ts.count + ts.columns - 1)//ts.columns
    if max_rows: rows = min(rows, max_rows)
    cw, ch = ts.tw*scale, ts.th*scale
    out = Image.new('RGBA', (cols*cw, rows*ch), (30, 30, 40, 255))
    d = ImageDraw.Draw(out)
    for lid in range(min(ts.count, cols*rows)):
        im = ts.tile_image(lid)
        c, r = lid % cols, lid // cols
        if im is not None:
            out.alpha_composite(im.resize((cw, ch), Image.NEAREST), (c*cw, r*ch))
        d.rectangle([c*cw, r*ch, c*cw+cw-1, r*ch+ch-1], outline=(255, 255, 255, 60))
        d.text((c*cw+2, r*ch+1), str(lid), fill=(255, 255, 0, 255))
    return out
