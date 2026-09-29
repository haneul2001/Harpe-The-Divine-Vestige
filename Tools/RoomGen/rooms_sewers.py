# 3층 지하 하수도(Sewers) 방 설계 목록.
#
# 바닥은 팩의 마른 돌바닥(ground path) 한 가지로 민다 — 초록 오수 물길은 테마와 안 맞아 쓰지 않는다.
# 벽은 둥근 모서리 wall-1(물 없는 판). 문은 1층 문에 초록빛을 입힌다(FloorData.doorTint).
# 장식은 사용자가 카탈로그에서 고른 것만 쓴다:
#   연구실 — 깨진 캡슐·캡슐·캡슐(묶음 첫 장)·증기기관·선반·초록 거품 캡슐·초록 물약
#   해골 — 앉은 해골 셋 / 나무 상자 하나 / 거미줄 셋 / 횃불 둘(벽에 건다)
# 방 크기 구성은 2층과 같다 (1칸 5개, 가로 2칸 2개, 세로 2칸 1개 + 시작·상점·보물).
#
# 좌표는 방 중심 기준. put()은 "보이는 그림"의 밑변 가운데로 놓는다 — 팩 그림은 칸 안에 투명 여백이 제각각이라
# 프레임 기준으로 놓으면 발이 벽 위에 뜨거나 옆 벽을 넘는다. 막는 소품은 맨 아래 한 칸만 충돌(임포터가 붙인다),
# 윗부분은 캐릭터가 뒤로 지나가며 가려진다. check()가 막는 소품이 바닥 안에 있는지·문 앞을 막는지 검사한다.
import os, re
from PIL import Image
from roombuild import build

THEME = 'sewers'
PROJ = r'C:/GitHub/Harpe The Divine Vestige/'
SW = 'Assets/ThirdParty/RafaelMatos/ERW - Sewers/Props/'
IND = SW + 'atlas props - individual sprites/'
ART = {
    'capsule_broken': IND + 'laboratory - broken capsule.png',
    'capsule':        IND + 'laboratory - capusle.png',
    'capsule_s':      SW + 'Secret Lab - capsules only.png',
    'engine':         IND + 'laboratory - engine.png',
    'shelves':        IND + 'laboratory - shelves1.png',
    'vat':            SW + 'Secret Lab - active capsule.png',       # 초록 거품 — 8장 시트
    'potion':         IND + 'laboratory - potions_13.png',
    'skel1':          IND + 'bones_7.png',
    'skel2':          IND + 'bones_18.png',
    'skel3':          IND + 'bones - color scheme 2_2.png',
    'crate':          IND + 'wooden chests - topdown_4.png',
    'web_l':          IND + 'web - color scheme 1_8.png',
    'web_r':          IND + 'web - color scheme 1_11.png',
    'web_s':          IND + 'web - color scheme 1_26.png',
    'torch1':         SW + 'torch1-lit.png',                        # 8장 시트
    'torch2':         SW + 'torch2-lit.png',
}
ANIM = {'vat': 8, 'torch1': 10, 'torch2': 10}
# 바닥에 까는 것(충돌 없음, 캐릭터 아래) / 벽에 거는 것
FLAT = {'potion', 'skel1', 'skel2', 'skel3', 'web_l', 'web_r', 'web_s', 'torch1', 'torch2'}

_vis = {}
def vis(key):
    """(보이는 영역의 프레임 기준 가운데 x 어긋남, 밑 여백, 보이는 폭, 보이는 높이) — 유닛"""
    if key not in _vis:
        import roomgen
        im = roomgen.first_frame(PROJ + ART[key])
        x0, y0, x1, y1 = im.getbbox()
        _vis[key] = (((x0 + x1) / 2 - im.width / 2) / 32, (im.height - y1) / 32, (x1 - x0) / 32, (y1 - y0) / 32)
    return _vis[key]

def put(key, x, y):
    """보이는 그림의 밑변 가운데를 (x, y)에 둔다"""
    dx, pad, w, h = vis(key)
    return (ART[key], round(x - dx, 3), round(y - pad, 3), key in FLAT, None, None, ANIM.get(key, 0))

# 벽 안쪽 경계 (그림에서 잰 값): 위쪽 벽 밑선은 벽 윗변에서 3.7칸, 옆·아래 벽은 안쪽으로 1.9·2칸
def floor_rect(span):
    hw, hh = 10 * span[0], 5.5 * span[1]
    return (-hw + 1.9, hw - 1.9, -hh + 2.0, hh - 3.7)
TOP = 0.75       # 1칸 높이 방에서 위쪽 벽 앞에 세울 때 발 자리 (맨 아래 칸이 벽 밑선 1.8 아래에 온다)
BOT = -3.45      # 아래 벽 바로 위
WALL = 2.25      # 위쪽 벽 앞면에 거는 것(횃불)의 밑변

def check(name, span, props):
    """막는 소품의 충돌 칸(보이는 폭 × 맨 아래 1칸)이 바닥 안에 있고 문 앞(1.5칸)을 막지 않는지"""
    L, R, B, T = floor_rect(span)
    hw, hh = 10 * span[0], 5.5 * span[1]
    zones = []
    for s in range(span[0]):
        cx = -hw + 10 + 20 * s
        zones += [(cx - 3.2, cx + 3.2, T - 1.5, T + 5), (cx - 3.2, cx + 3.2, B - 5, B + 1.5)]
    for s in range(span[1]):
        cy = -hh + 5.0 + 11 * s
        zones += [(L - 5, L + 1.5, cy - 1.8, cy + 1.5), (R - 1.5, R + 5, cy - 1.8, cy + 1.5)]
    bad = []
    for pr in props:
        key = next((k for k, v in ART.items() if v == pr[0]), None)
        if key is None or key in FLAT: continue   # 다른 팩 것(보물방 통·상자)은 1층 배치 그대로
        dx, pad, w, h = vis(key)
        x0, x1 = pr[1] + dx - w * 0.45, pr[1] + dx + w * 0.45
        y0 = pr[2] + pad; y1 = y0 + min(1.0, h)
        if x0 < L - 0.05 or x1 > R + 0.05 or y0 < B - 0.05 or y1 > T + 0.05:
            bad.append(f'{key}@({pr[1]},{pr[2]}) 바닥 밖 [{x0:.2f},{x1:.2f}]x[{y0:.2f},{y1:.2f}] 바닥 [{L},{R}]x[{B},{T}]')
        for zx0, zx1, zy0, zy1 in zones:
            if x0 < zx1 and x1 > zx0 and y0 < zy1 and y1 > zy0:
                bad.append(f'{key}@({pr[1]},{pr[2]}) 문 앞을 막음'); break
    if bad: print('  !!', name, *bad, sep='\n    ')
    return props

SPAWN_1x1 = [(-5, -0.5), (5, -0.5), (0, -1), (-5, -2.5), (5, -2.5), (0, -3), (-2.5, -2), (2.5, -2)]
SPAWN_2x1 = [(-15, -1), (15, -1), (-15, -2.5), (15, -2.5), (-6, -1), (6, -1), (0, -1), (0, -3)]
SPAWN_1x2 = [(-5, 4), (5, 4), (0, 2), (-5, -2), (5, -2), (0, -6), (-3, -8), (3, 1)]

def torches(xs, y=WALL, alt=False):
    return [put('torch2' if (alt and i % 2) else 'torch1', x, y) for i, x in enumerate(xs)]

def webs(span, y_top):
    """위쪽 벽 두 모서리에 거미줄. 벽 앞면 윗선(y_top)에 매단다"""
    hw = 10 * span[0] - 1.35
    return [put('web_l', -hw + vis('web_l')[2] / 2, y_top - vis('web_l')[3]),
            put('web_r', hw - vis('web_r')[2] / 2, y_top - vis('web_r')[3])]

def one(name, seed, props, **kw):
    return build(name, (1, 1), ['.' * 18] * 9, seed=seed, theme=THEME, props=check(name, (1, 1), props), **kw)
def wide(name, seed, props, **kw):
    return build(name, (2, 1), ['.' * 38] * 9, seed=seed, theme=THEME, props=check(name, (2, 1), props), **kw)
def tall(name, seed, props, **kw):
    return build(name, (1, 2), ['.' * 18] * 20, seed=seed, theme=THEME, props=check(name, (1, 2), props), **kw)

WEB_TOP = 3.85   # 1칸 높이 방의 위쪽 벽 앞면 윗선

def room_shelves(name='Room_Sw_Shelves'):
    """연구 선반 — 위쪽 벽 앞에 선반 한 쌍, 문 옆 횃불. 아래 모서리엔 상자와 앉은 해골."""
    P = [put('shelves', -5.4, TOP), put('shelves', 5.4, TOP)] + torches([-3.7, 3.7])
    P += webs((1, 1), WEB_TOP)
    P += [put('crate', 7.4, BOT), put('crate', 6.5, BOT)]
    P += [put('skel1', -7.3, BOT), put('potion', -6.3, BOT + 0.1), put('potion', 4.6, -1.6)]
    return one(name, 601, P, spawns=SPAWN_1x1)

def room_engine(name='Room_Sw_Engine'):
    """증기기관실 — 가운데 조금 아래 증기기관이 엄폐물. 위쪽 벽 앞 캡슐 둘."""
    P = [put('engine', 0.0, -1.95), put('capsule', -5.2, TOP), put('capsule_broken', 5.2, TOP)]
    P += torches([-3.7, 3.7], alt=True) + webs((1, 1), WEB_TOP)
    P += [put('skel2', 7.3, BOT), put('potion', -7.0, BOT + 0.2)]
    return one(name, 602, P, spawns=[(-5, -0.5), (5, -0.5), (-6, -2.5), (6, -2.5), (-3, -1), (3, -1), (0, 0), (-2.5, 0.2)])

def room_capsules(name='Room_Sw_Capsules'):
    """캡슐 보관실 — 위쪽 벽 앞에 캡슐이 줄지어 서고, 초록 거품 캡슐이 섞여 있다."""
    P = [put('capsule', -5.9, TOP), put('vat', -4.8, TOP), put('capsule_s', 4.8, TOP), put('capsule_broken', 5.9, TOP)]
    P += torches([-3.7, 3.7]) + [put('web_s', -7.4, 2.4), put('web_s', 7.4, 2.4)]
    P += [put('crate', -7.4, BOT), put('skel3', 7.3, BOT)]
    return one(name, 603, P, spawns=SPAWN_1x1)

def room_ossuary(name='Room_Sw_Ossuary'):
    """해골 수로 — 벽 밑에 기대앉은 해골들, 가운데 상자 무더기."""
    P = [put('skel1', -6.2, 1.0), put('skel2', 5.2, 1.0), put('skel3', 6.4, 1.0), put('skel3', -7.3, BOT)]
    P += [put('crate', -0.5, -1.4), put('crate', 0.4, -1.4)]
    P += torches([-3.7, 3.7], alt=True) + webs((1, 1), WEB_TOP) + [put('potion', 7.2, BOT + 0.1)]
    return one(name, 604, P, spawns=SPAWN_1x1)

def room_vats(name='Room_Sw_Vats'):
    """초록 거품 방 — 위쪽 벽 앞 거품 캡슐 둘과 선반, 아래 모서리 상자."""
    P = [put('vat', -5.0, TOP), put('shelves', 5.4, TOP), put('vat', -6.1, TOP)]
    P += torches([-3.7, 3.7]) + [put('web_l', -7.1, 2.35)]
    P += [put('crate', 7.4, BOT), put('crate', -7.4, BOT), put('skel2', -6.4, BOT), put('potion', 6.4, BOT + 0.1)]
    return one(name, 605, P, spawns=SPAWN_1x1)

def room_lab_long(name='Room_Sw_LabLong'):
    """긴 연구실 — 가로 2칸. 위쪽 벽 앞에 선반·캡슐·증기기관이 문 사이사이에 선다 (위쪽 문은 x ±10)."""
    P = [put('shelves', -15.5, TOP), put('capsule', -4.6, TOP), put('vat', -3.5, TOP), put('engine', 1.5, TOP),
         put('capsule_s', 14.6, TOP), put('capsule_broken', 15.7, TOP)]
    P += torches([-13.6, -6.4, 6.4, 13.6], alt=True) + webs((2, 1), WEB_TOP)
    P += [put('crate', -17.4, BOT), put('crate', -16.5, BOT), put('skel1', 17.3, BOT), put('potion', 0.0, -2.2)]
    P += [put('capsule', -6.0, -2.4), put('capsule', 6.0, -2.4)]
    return wide(name, 606, P, spawns=SPAWN_2x1)

def room_ossuary_long(name='Room_Sw_OssuaryLong'):
    """해골 회랑 — 가로 2칸. 가운데 증기기관 두 대가 기둥처럼 방을 나누고, 벽 밑엔 해골들."""
    P = [put('engine', -4.5, -2.0), put('engine', 4.5, -2.0)]
    P += [put('skel1', -16.4, 1.0), put('skel3', -3.5, 1.0), put('skel2', 3.5, 1.0), put('skel1', 16.4, 1.0)]
    P += torches([-13.6, -6.4, 6.4, 13.6]) + webs((2, 1), WEB_TOP)
    P += [put('crate', 17.4, BOT), put('crate', -17.4, BOT), put('potion', 0.0, BOT + 0.2), put('skel2', 16.4, BOT)]
    return wide(name, 607, P, spawns=SPAWN_2x1)

def room_tower(name='Room_Sw_Tower'):
    """캡슐 탑 — 세로 2칸. 위쪽 벽 앞 선반 한 쌍, 가운데 거품 캡슐 기둥 네 개가 네모로 선다."""
    up = 5.5     # 세로 2칸이면 위쪽 벽이 5.5 위, 아래쪽 벽이 5.5 아래
    P = [put('shelves', -5.4, TOP + up), put('shelves', 5.4, TOP + up)] + torches([-3.7, 3.7], y=WALL + up)
    P += webs((1, 2), WEB_TOP + up)
    P += [put('vat', -3.0, 2.0), put('vat', 3.0, 2.0), put('capsule', -3.0, -3.0), put('capsule', 3.0, -3.0)]
    P += [put('crate', -7.4, BOT - 5.5), put('crate', 7.4, BOT - 5.5), put('skel1', -7.2, 1.0), put('skel3', 7.2, -1.0), put('potion', 0.0, -0.5)]
    return tall(name, 608, P, spawns=SPAWN_1x2)

def room_start(name='Room_Sw_Start'):
    """시작 방 — 적 없음. 문 옆 횃불과 벽 밑 해골 하나, 모서리 거미줄."""
    P = torches([-3.7, 3.7]) + webs((1, 1), WEB_TOP) + [put('skel1', -6.4, 1.0), put('crate', 7.4, BOT)]
    return one(name, 609, P, spawns=[], kind='Start')

def room_shop(name='Room_Sw_Shop'):
    """상점 — 상점 물건은 템플릿이 놓는다. 장식은 벽 앞 선반과 캡슐, 횃불."""
    P = [put('shelves', -5.6, TOP), put('capsule', 4.5, TOP), put('vat', 5.6, TOP)] + torches([-3.7, 3.7], alt=True)
    P += [put('web_s', -7.4, 2.4), put('crate', -7.4, BOT), put('potion', 7.0, BOT + 0.1)]
    return one(name, 610, P, spawns=[], kind='Shop')

def room_treasure(name='Room_Sw_Treasure'):
    """보물방 — 부서지는 금 통·나무 상자는 1층과 같다(보상이라 장식이 아니다). 장식만 하수도 것으로."""
    from rooms_special import p, crate, OP
    P = [p(OP + 'barrel - 1 gold.png', -1.2, -1.3, broken=OP + 'barrel - broken - 12.png', gold=(20, 20)),
         p(OP + 'barrel - 2 gold.png', 1.2, -1.3, broken=OP + 'barrel - broken - 13.png', gold=(20, 20))]
    P += [crate(1, 5.0, -3.4), crate(2, 6.2, -3.4), crate(3, 5.6, -2.4), crate(4, -3.5, -1.0), crate(1, 3.5, -1.0), crate(2, -4.3, -2.6)]
    P += torches([-3.7, 3.7]) + webs((1, 1), WEB_TOP) + [put('skel2', -7.3, BOT)]
    return one(name, 611, P, spawns=[], kind='Treasure')

NORMAL = [room_shelves, room_engine, room_capsules, room_ossuary, room_vats, room_lab_long, room_ossuary_long, room_tower]
SPECIAL = [room_start, room_shop, room_treasure]
ALL = NORMAL + SPECIAL

if __name__ == '__main__':
    import time, sys
    outd = r'C:/Temp/claude/C--GitHub-Harpe-The-Divine-Vestige/82f0c1ae-bd53-4737-8386-1836ba896e5c/scratchpad'
    only = sys.argv[1:]
    fns = [f for f in ALL if not only or f.__name__ in only]
    if '--check' in sys.argv:
        # 빠른 검사: build 없이 배치 검사만
        globals()['build'] = lambda *a, **k: (None, {})
        for fn in ALL: fn()
        sys.exit()
    ims = []
    for fn in fns:
        t = time.time(); room, plugs = fn(); print(fn.__name__, '%.1fs' % (time.time() - t)); sys.stdout.flush()
        ims.append(room.render())
    w = max(i.width for i in ims); h = sum(i.height for i in ims) + 10 * len(ims)
    sheet = Image.new('RGBA', (w, h), (40, 0, 0, 255)); y = 0
    for i in ims: sheet.paste(i, (0, y)); y += i.height + 10
    sheet.save(outd + '/sewers_rooms_v2.png'); print(sheet.size)
