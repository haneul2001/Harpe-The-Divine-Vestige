# 2층 심연 동굴(The Depths) 방 설계 목록.
#
# 장식은 사용자가 고른 것만 쓴다 — 수정 쌍 5종(파랑·분홍 무더기·초록·분홍·하늘), 황금 흉상 남녀, 촛대 2종, 황금 기념비 5종.
# 방 크기 구성은 1층과 같다 (1칸 5개, 가로 2칸 2개, 세로 2칸 1개 + 시작·상점·보물).
#
# 좌표는 방 중심 기준, 발밑. 심연 벽 앞면은 4줄이라 1칸 방에서 첫 바닥 줄은 y 0.5~1.5다 —
# 벽 앞에 세우는 것(흉상·촛대·기념비)은 발을 y 0.6에 둬야 벽 위에 떠 보이지 않는다.
# 문 앞 통로: 위·아래 문은 각 칸 가운데 x ±3 안, 옆문은 벽에서 두 칸 안쪽까지 y -3.5~2.5. 여기엔 막는 것을 두지 않는다.
from roombuild import build
from rooms_special import p, crate, OP

RM = 'Assets/ThirdParty/RafaelMatos'
DP = RM + '/ERW - The Depths/Props/Static/Props-individual sprites/'

def _cr(name): return DP + name
CRYSTALS = {
    'blue':  [_cr('Crystals1-improved refraction_1.png'), _cr('Crystals1-improved refraction_2.png')],
    'green': [_cr('Crystals3-improved refraction_1.png'), _cr('Crystals3-improved refraction_2.png')],
    'cyan':  [_cr('Crystals4-improved refraction_1.png'), _cr('Crystals4-improved refraction_3.png')],
    'pink':  [_cr('Crystals5-improved refraction_1.png'), _cr('Crystals5-improved refraction_2.png')],
}
PINK_CLUSTER = _cr('Crystals6_0.png')
BUST_M, BUST_W = DP + 'statues-men_0.png', DP + 'statues-women_0.png'
CANDLES = [DP + 'candelabrum_0.png', DP + 'candelabrum_1.png']
MONUMENT_BIG = [DP + 'golden monument_0.png', DP + 'golden monument_1.png']
MONUMENT_THIN = [DP + 'golden monument_2.png', DP + 'golden monument_3.png']
MONUMENT_RING = DP + 'golden monument_4.png'

WALL_FOOT = 0.6   # 1칸 높이 방의 첫 바닥 줄 (벽 앞에 세울 때 발 자리)
BOTTOM = -3.3     # 아래 벽 바로 위. 아래 벽 그림 윗테두리가 바닥 쪽으로 반 칸 넘게 올라와 있어 -4면 테두리에 걸쳐 보인다

def pair(color, x, y, flip=False):
    """수정 두 개를 붙여 한 무더기로. 서 있는 소품이라 맨 아래 한 칸만 막고, 윗부분 뒤로는 캐릭터가 가려진다"""
    a, b = CRYSTALS[color]
    dx = -0.8 if flip else 0.8
    return [p(a, x, y), p(b, x + dx, y - 0.35)]

def cluster(x, y):
    return [p(PINK_CLUSTER, x, y)]

def ring(x, y):
    """둥근 황금 기념비 — 바닥에 새겨진 문양처럼 깐다. 밟고 지나가며 캐릭터 아래에 그려진다"""
    return p(MONUMENT_RING, x, y, passable=True)

def wall_top(span):
    """방 높이에 따른 첫 바닥 줄 y (세로 2칸 방은 위로 5.5 더)"""
    return WALL_FOOT + (span[1] - 1) * 5.5

SPAWN_1x1 = [(-5, -0.5), (5, -0.5), (0, -1), (-5, -3), (5, -3), (0, -3), (-2.5, -2), (2.5, -2)]
SPAWN_2x1 = [(-15, -1), (15, -1), (-15, -3), (15, -3), (-6, -1), (6, -1), (0, -1), (0, -3.5)]
SPAWN_1x2 = [(-6, 5), (6, 5), (0, 3), (-6, -2), (6, -2), (0, -6), (-3, -8), (3, 1)]

def room_crystals(name='Room_Dep_Crystals'):
    """수정 굴 — 네 귀퉁이에 색이 다른 수정 무더기, 가운데 분홍 무더기."""
    props = pair('blue', -7.5, 0.3) + pair('green', 7.0, 0.3, flip=True) + pair('pink', -7.5, BOTTOM) + pair('cyan', 7.0, BOTTOM, flip=True)
    props += cluster(-1.5, -1.5)
    return build(name, (1, 1), ['.' * 18] * 9, seed=501, theme='depths', props=props, spawns=SPAWN_1x1)

def room_busts(name='Room_Dep_Busts'):
    """흉상 방 — 벽 앞에 황금 흉상 한 쌍, 그 바깥에 촛대. 아래 귀퉁이 수정."""
    y = WALL_FOOT
    props = [p(BUST_M, -5.0, y), p(BUST_W, 5.0, y), p(CANDLES[0], -7.2, y), p(CANDLES[1], 7.2, y)]
    props += pair('cyan', -7.5, BOTTOM) + pair('blue', 7.0, BOTTOM, flip=True)
    return build(name, (1, 1), ['.' * 18] * 9, seed=502, theme='depths', props=props, spawns=SPAWN_1x1)

def room_altar(name='Room_Dep_Altar'):
    """황금 제단 — 방 가운데 큰 황금 기념비, 양옆 촛대. 싸울 때 가운데를 돌아 움직이게 된다."""
    props = [p(MONUMENT_BIG[0], 0.0, -1.3), p(CANDLES[0], -2.6, -1.0), p(CANDLES[1], 2.6, -1.0)]
    props += pair('pink', -7.5, 0.3) + pair('green', 7.0, 0.3, flip=True) + cluster(-6.5, BOTTOM) + pair('blue', 6.5, BOTTOM, flip=True)
    return build(name, (1, 1), ['.' * 18] * 9, seed=503, theme='depths', props=props, spawns=[(-6, -1), (6, -1), (-4, -3.5), (4, -3.5), (0, BOTTOM), (-6, -3), (6, -3), (0, 0.5)])

def room_candles(name='Room_Dep_Candles'):
    """촛불 방 — 촛대 네 개가 네모로 서서 엄폐물이 된다."""
    y = WALL_FOOT
    props = [p(CANDLES[0], -5.0, y), p(CANDLES[1], 5.0, y), p(CANDLES[1], -5.0, -3.2), p(CANDLES[0], 5.0, -3.2)]
    props += pair('green', -7.8, BOTTOM) + pair('pink', 7.3, 0.2, flip=True) + cluster(1.0, -1.8)
    return build(name, (1, 1), ['.' * 18] * 9, seed=504, theme='depths', props=props, spawns=SPAWN_1x1)

def room_monuments(name='Room_Dep_Monuments'):
    """기념비 방 — 벽 앞에 가는 황금 기념비 한 쌍, 아래 귀퉁이에 둥근 기념비."""
    y = WALL_FOOT
    props = [p(MONUMENT_THIN[0], -5.0, y), p(MONUMENT_THIN[1], 5.0, y), ring(-6.5, -2.6), ring(6.5, -2.6)]
    props += pair('blue', -7.8, 0.2) + pair('cyan', 7.4, 0.2, flip=True)
    return build(name, (1, 1), ['.' * 18] * 9, seed=505, theme='depths', props=props, spawns=SPAWN_1x1)

def room_long(name='Room_Dep_Long'):
    """긴 동굴 — 가로 2칸. 위 벽 앞에 흉상·촛대가 번갈아 서고, 수정이 흩어진다.
    위쪽 문 자리는 각 칸 가운데(x -10, +10 기준 ±3)라 그 사이(±4, ±16)에만 세운다."""
    y = WALL_FOOT
    props = [p(BUST_M, -16.0, y), p(CANDLES[0], -4.0, y), p(CANDLES[1], 4.0, y), p(BUST_W, 16.0, y)]
    props += pair('blue', -17.5, BOTTOM) + pair('pink', 17.0, BOTTOM, flip=True) + cluster(-9.0, -1.5) + pair('green', 9.5, -1.8)
    props += pair('cyan', 0.0, BOTTOM)
    return build(name, (2, 1), ['.' * 38] * 9, seed=506, theme='depths', props=props, spawns=SPAWN_2x1)

def room_shrine_hall(name='Room_Dep_ShrineHall'):
    """기념비 회랑 — 가로 2칸. 가운데 큰 기념비 두 개가 기둥처럼 서서 방을 세 구역으로 나눈다."""
    y = WALL_FOOT
    props = [p(MONUMENT_BIG[1], -5.0, -1.4), p(MONUMENT_BIG[0], 5.0, -1.4)]
    props += [p(MONUMENT_THIN[0], -16.0, y), p(MONUMENT_THIN[1], 16.0, y), p(CANDLES[0], -4.0, y), p(CANDLES[1], 4.0, y)]
    props += pair('pink', -17.5, BOTTOM) + pair('cyan', 17.0, BOTTOM, flip=True) + cluster(0.0, BOTTOM)
    return build(name, (2, 1), ['.' * 38] * 9, seed=507, theme='depths', props=props, spawns=SPAWN_2x1)

def room_tower(name='Room_Dep_Tower'):
    """심연 탑 — 세로 2칸. 위 벽 앞 흉상 한 쌍, 가운데 둥근 기념비, 양옆 촛대 줄."""
    top = wall_top((1, 2))
    props = [p(BUST_M, -5.0, top), p(BUST_W, 5.0, top)]
    props += [ring(0.0, 0.0), p(CANDLES[0], -6.0, -2.5), p(CANDLES[1], 6.0, -2.5), p(CANDLES[1], -6.0, 3.5), p(CANDLES[0], 6.0, 3.5)]
    props += pair('blue', -7.5, BOTTOM - 5.5) + pair('green', 7.0, BOTTOM - 5.5, flip=True) + cluster(-3.5, -6.0) + pair('pink', 4.0, 6.0)
    return build(name, (1, 2), ['.' * 18] * 20, seed=508, theme='depths', props=props, spawns=SPAWN_1x2)

def room_start(name='Room_Dep_Start'):
    """시작 방 — 적 없음. 벽 앞 촛대 한 쌍과 수정만."""
    y = WALL_FOOT
    props = [p(CANDLES[0], -5.0, y), p(CANDLES[1], 5.0, y)] + pair('blue', -7.5, BOTTOM) + pair('cyan', 7.0, BOTTOM, flip=True)
    return build(name, (1, 1), ['.' * 18] * 9, seed=509, theme='depths', props=props, spawns=[], kind='Start')

def room_shop(name='Room_Dep_Shop'):
    """상점 — 상점 물건은 템플릿이 놓는다. 장식은 벽 앞 흉상 한 쌍과 촛대."""
    y = WALL_FOOT
    props = [p(BUST_M, -6.5, y), p(BUST_W, 6.5, y), p(CANDLES[0], -4.5, y), p(CANDLES[1], 4.5, y)] + pair('pink', -7.5, BOTTOM) + pair('green', 7.0, BOTTOM, flip=True)
    return build(name, (1, 1), ['.' * 18] * 9, seed=510, theme='depths', props=props, spawns=[], kind='Shop')

def room_treasure(name='Room_Dep_Treasure'):
    """보물방 — 부서지는 금 통·나무 상자는 1층과 같다(보상이라 장식이 아니다). 장식만 심연 것으로."""
    props = [p(OP + 'barrel - 1 gold.png', -1.2, -1.3, broken=OP + 'barrel - broken - 12.png', gold=(20, 20)),
             p(OP + 'barrel - 2 gold.png', 1.2, -1.3, broken=OP + 'barrel - broken - 13.png', gold=(20, 20))]
    props += [crate(1, 5.0, -3.4), crate(2, 6.2, -3.4), crate(3, 5.6, -2.4), crate(4, -3.5, -1.0), crate(1, 3.5, -1.0), crate(2, -4.3, -2.6)]
    props += [p(MONUMENT_THIN[0], -5.0, WALL_FOOT), p(MONUMENT_THIN[1], 5.0, WALL_FOOT)] + cluster(-7.0, BOTTOM)
    return build(name, (1, 1), ['.' * 18] * 9, seed=511, theme='depths', props=props, spawns=[], kind='Treasure')

NORMAL = [room_crystals, room_busts, room_altar, room_candles, room_monuments, room_long, room_shrine_hall, room_tower]
SPECIAL = [room_start, room_shop, room_treasure]
ALL = NORMAL + SPECIAL

if __name__ == '__main__':
    import time, sys
    from PIL import Image
    outd = r'C:/Temp/claude/C--GitHub-Harpe-The-Divine-Vestige/82f0c1ae-bd53-4737-8386-1836ba896e5c/scratchpad'
    ims = []
    for fn in ALL:
        t = time.time(); room, plugs = fn(); print(fn.__name__, '%.1fs' % (time.time() - t), 'plugs', len(plugs)); sys.stdout.flush()
        ims.append(room.render())
    w = max(i.width for i in ims); h = sum(i.height for i in ims) + 10 * len(ims)
    sheet = Image.new('RGBA', (w, h), (40, 0, 0, 255)); y = 0
    for i in ims: sheet.paste(i, (0, y)); y += i.height + 10
    sheet.save(outd + '/depths_rooms_v1.png'); print(sheet.size)
