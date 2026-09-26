# 1층 납골당(Crypt) 방 설계 목록.
#
# 안쪽 격자 글자: '.' 바닥(ground2)  ',' ground1 무늬  ':' 흙  'P' 구덩이  'W' 통로  'r' 난간  '#' 안쪽 벽
# 소품은 blocks 로 따로 둔다: (격자x, 격자y, 아틀라스 왼쪽위 id, 가로, 세로, 막는지). 좌표는 방 전체 격자(벽 포함) 기준.
from roombuild import build, CELL_W, CELL_H

# 아틀라스 블록 사전 — prop_blocks.png 도판으로 확인한 것만
COFFIN_V = [(360, 2, 3), (362, 2, 3), (364, 2, 3), (388, 2, 3), (390, 2, 3), (424, 2, 3), (428, 2, 3)]
COFFIN_H = [(366, 3, 2), (383, 3, 2), (419, 3, 2)]
CANDELABRUM = [(85, 2, 2), (87, 2, 2), (157, 2, 2)]
CANDLE = (84, 1, 1)
BANNER = [(89, 1, 2), (92, 1, 2), (95, 1, 2), (98, 1, 2), (161, 1, 2)]
RUG = [(229, 3, 1), (265, 3, 1), (301, 3, 1), (337, 3, 1)]
STATUE = [(576, 2, 2), (578, 2, 2), (580, 2, 2), (582, 2, 2), (584, 2, 2)]
STATUE_BIG = [(792, 2, 3), (794, 2, 3), (796, 2, 3), (798, 2, 3), (800, 2, 3), (838, 2, 3)]
PEDESTAL = [(648, 2, 2), (650, 2, 2), (652, 2, 2)]
VASE = [(587, 1, 1), (623, 1, 1), (695, 1, 1), (731, 1, 1)]
BENCH = [(670, 2, 2), (672, 2, 2), (674, 2, 2)]
BONES = [(73, 2, 2), (109, 3, 2), (217, 2, 2), (253, 2, 2), (289, 1, 1)]

def blk(x, y, spec, blocking=True):
    tid, w, h = spec
    return (x, y, tid, w, h, blocking)

def rows(*lines):
    return list(lines)

# ── 설계 ──────────────────────────────────────────────

def room_ossuary(name='Room_Cry_Ossuary'):
    """안치소 — 세로 관이 두 줄, 사이가 통로. 1x2 (20x22)."""
    inner = rows(
        '..................',
        '..................',
        '...,,,........,,,.',
        '...,,,........,,,.',
        '..................',
        '..................',
        '..................',
        '..................',
        '...,,,........,,,.',
        '...,,,........,,,.',
        '..................',
        '..................',
        '..................',
        '..................',
        '...,,,........,,,.',
        '...,,,........,,,.',
        '..................',
        '..................',
        '..................',
        '..................',
    )
    blocks = []
    for i, gy in enumerate((4, 10, 16)):
        blocks.append(blk(4, gy, COFFIN_V[i % len(COFFIN_V)]))
        blocks.append(blk(15, gy, COFFIN_V[(i + 3) % len(COFFIN_V)]))
    blocks += [blk(2, 3, CANDELABRUM[0]), blk(17, 3, CANDELABRUM[1]), blk(2, 15, CANDELABRUM[2]), blk(17, 15, CANDELABRUM[0])]
    blocks += [blk(5, 1, BANNER[0], False), blk(13, 1, BANNER[0], False), blk(5, 1, BANNER[2], False), blk(14, 1, BANNER[2], False)]
    blocks += [blk(8, 8, BONES[0], False), blk(12, 13, BONES[2], False)]
    spawns = [(-6, 6), (6, 6), (0, 3), (-6, -2), (6, -2), (0, -6), (-3, -8), (3, 1)]
    return build(name, (1, 2), inner, seed=101, spawns=spawns, blocks=blocks)

def room_gallery(name='Room_Cry_Gallery'):
    """긴 회랑 — 구덩이·다리 없이 바닥을 쭉 밀고 장식만 둔다. 2x1 (40x11)."""
    W = 38
    inner = rows(*(['.' * W] * 9))
    blocks = [
        # 위쪽 벽 앞 석상 4구 (위쪽 문 자리 7~12·27~32열은 비운다)
        blk(3, 2, STATUE[0]), blk(14, 2, STATUE[1]), blk(23, 2, STATUE[2]), blk(34, 2, STATUE[3]),
        blk(1, 3, CANDELABRUM[0]), blk(36, 3, CANDELABRUM[1]),
        blk(18, 1, BANNER[1], False), blk(21, 1, BANNER[1], False), blk(4, 1, BANNER[0], False), blk(35, 1, BANNER[0], False),
        # 가운데 융단 길과 받침대 한 쌍
        blk(9, 5, RUG[0], False), blk(18, 5, RUG[2], False), blk(27, 5, RUG[0], False),
        blk(16, 3, PEDESTAL[0]), blk(21, 3, PEDESTAL[1]),
        # 아래쪽 관 두 개, 뼈·항아리
        blk(13, 6, COFFIN_H[0]), blk(23, 6, COFFIN_H[2]),
        blk(6, 7, BONES[0], False), blk(31, 4, BONES[2], False), blk(19, 7, BONES[4], False),
        blk(2, 7, VASE[0], False), blk(36, 7, VASE[1], False), blk(2, 5, VASE[2], False), blk(36, 5, VASE[3], False),
    ]
    spawns = [(-15, 1), (15, 1), (-15, -3), (15, -3), (-6, 0), (6, 0), (0, 1), (0, -3.5)]
    return build(name, (2, 1), inner, seed=102, spawns=spawns, blocks=blocks)

def room_hall(name='Room_Cry_Hall'):
    """석상 홀 — 양옆 석상 열과 가운데 카펫. 2x1 (40x11)."""
    W = 38
    inner = rows(
        '.' * W,
        '.' * W,
        '.' * 15 + ':' * 8 + '.' * 15,
        '.' * 15 + ':' * 8 + '.' * 15,
        '.' * 15 + ':' * 8 + '.' * 15,
        '.' * 15 + ':' * 8 + '.' * 15,
        '.' * 15 + ':' * 8 + '.' * 15,
        '.' * W,
        '.' * W,
    )
    blocks = []
    for i, gx in enumerate((3, 9, 28, 34)):
        blocks.append(blk(gx, 3, STATUE_BIG[i % len(STATUE_BIG)]))
    blocks += [blk(18, 5, RUG[0], False), blk(2, 8, VASE[0], False), blk(37, 8, VASE[1], False), blk(19, 1, BANNER[0], False), blk(20, 1, BANNER[0], False)]
    spawns = [(-14, 1), (14, 1), (-14, -3), (14, -3), (0, 1), (-7, -1), (7, -1), (0, -3.5)]
    return build(name, (2, 1), inner, seed=103, spawns=spawns, blocks=blocks)

def room_candles(name='Room_Cry_Candles'):
    """촛불 회랑 — 1x1. 양옆 촛대 열, 가운데 카펫, 위 배너."""
    inner = rows(*(['.' * 18] * 9))
    blocks = [blk(2, 3, CANDELABRUM[0]), blk(16, 3, CANDELABRUM[1]), blk(2, 7, CANDELABRUM[2]), blk(16, 7, CANDELABRUM[0]),
              blk(8, 5, RUG[3], False), blk(5, 1, BANNER[0], False), blk(13, 1, BANNER[0], False),
              blk(9, 8, BONES[4], False), blk(5, 6, VASE[2], False), blk(14, 6, VASE[3], False)]
    spawns = [(-5, 1), (5, 1), (0, 0), (-5, -3), (5, -3), (0, -3), (-2, -1), (2, -1)]
    return build(name, (1, 1), inner, seed=111, spawns=spawns, blocks=blocks)

def room_graves(name='Room_Cry_Graves'):
    """열린 무덤 — 1x1. 뚜껑 열린 관들과 흩어진 뼈, 붉은 무늬 바닥."""
    inner = rows(
        '..................',
        '..................',
        '..,,,,......,,,,..',
        '..,,,,......,,,,..',
        '..................',
        '..................',
        '..,,,,......,,,,..',
        '..,,,,......,,,,..',
        '..................',
    )
    blocks = [blk(3, 3, COFFIN_H[1]), blk(13, 3, COFFIN_H[2]), blk(3, 7, COFFIN_H[0]), blk(13, 7, COFFIN_H[1]),
              blk(8, 4, BONES[1], False), blk(8, 7, BONES[3], False), blk(6, 2, BONES[4], False), blk(12, 8, BONES[4], False),
              blk(5, 1, BANNER[2], False), blk(14, 1, BANNER[2], False)]
    spawns = [(-4, 1), (4, 1), (0, 1), (-4, -3), (4, -3), (0, -3), (-7, -1), (7, -1)]
    return build(name, (1, 1), inner, seed=112, spawns=spawns, blocks=blocks)

def room_start(name='Room_Cry_Start'):
    """시작 방 — 1x1. 적 없음. 위쪽 문 대신 층 입구 느낌의 석상·촛대."""
    inner = rows(*(['.' * 18] * 9))
    blocks = [blk(3, 2, STATUE[0]), blk(1, 2, CANDELABRUM[0]), blk(16, 2, CANDELABRUM[1]), blk(8, 6, RUG[1], False),
              blk(5, 1, BANNER[1], False), blk(13, 1, BANNER[1], False)]
    return build(name, (1, 1), inner, seed=113, spawns=[], blocks=blocks, kind='Start')

def room_shop(name='Room_Cry_Shop'):
    """상점 — 1x1. 탁자·의자·꽃병·카펫. 적 없음."""
    inner = rows(*(['.' * 18] * 9))
    blocks = [blk(4, 3, BENCH[0]), blk(9, 3, BENCH[1]), blk(14, 3, BENCH[2]), blk(8, 7, RUG[0], False),
              blk(3, 6, VASE[0], False), blk(16, 6, VASE[1], False), blk(2, 2, CANDELABRUM[2]), blk(16, 2, CANDELABRUM[0]),
              blk(5, 1, BANNER[4], False), blk(14, 1, BANNER[4], False)]
    return build(name, (1, 1), inner, seed=114, spawns=[], blocks=blocks, kind='Shop')

def room_pillars(name='Room_Cry_Pillars'):
    """기둥 홀 — 1x1. 받침대 4개가 격자로 서서 엄폐물이 된다."""
    inner = rows(*(['.' * 18] * 9))
    blocks = [blk(4, 3, PEDESTAL[0]), blk(12, 3, PEDESTAL[1]), blk(4, 6, PEDESTAL[2]), blk(12, 6, PEDESTAL[0]),
              blk(5, 1, BANNER[3], False), blk(14, 1, BANNER[3], False), blk(8, 5, BONES[4], False), blk(15, 8, VASE[2], False), blk(2, 8, VASE[0], False)]
    spawns = [(-7, 1), (7, 1), (0, 1), (-7, -3), (7, -3), (0, -3), (-3, -1), (3, -1)]
    return build(name, (1, 1), inner, seed=115, spawns=spawns, blocks=blocks)

def room_altar(name='Room_Cry_Altar'):
    """제단 — 1x1. 가운데 큰 석상, 양옆 촛불, 붉은 카펫 길."""
    inner = rows(
        '..................',
        '..................',
        '.......,,,,.......',
        '.......,,,,.......',
        '.......,,,,.......',
        '.......,,,,.......',
        '.......,,,,.......',
        '..................',
        '..................',
    )
    blocks = [blk(8, 2, STATUE_BIG[5]), blk(6, 3, CANDLE, False), blk(11, 3, CANDLE, False), blk(6, 5, CANDLE, False), blk(11, 5, CANDLE, False),
              blk(8, 7, RUG[0], False), blk(3, 2, BANNER[0], False), blk(16, 2, BANNER[0], False), blk(2, 7, BONES[0], False), blk(14, 8, BONES[4], False)]
    spawns = [(-6, 1), (6, 1), (-6, -3), (6, -3), (-2, -3), (2, -3), (-8, -1), (8, -1)]
    return build(name, (1, 1), inner, seed=116, spawns=spawns, blocks=blocks)

def room_bonepit(name='Room_Cry_Bonepit'):
    """뼈 무더기 — 1x1. 가로 관 두 개와 뼈가 널린 흙바닥."""
    inner = rows(
        '..................',
        '..................',
        '.,,,,........,,,,.',
        '.,,,,........,,,,.',
        '..................',
        '....,,,,,,,,,,....',
        '....,,,,,,,,,,....',
        '..................',
        '..................',
    )
    blocks = [blk(2, 3, COFFIN_H[0]), blk(13, 3, COFFIN_H[2]), blk(5, 5, BONES[1], False), blk(10, 6, BONES[0], False), blk(8, 4, BONES[4], False),
              blk(13, 7, BONES[3], False), blk(3, 7, BONES[2], False), blk(5, 1, BANNER[2], False), blk(2, 1, CANDELABRUM[2]), blk(16, 1, CANDELABRUM[2])]
    spawns = [(-6, 1), (6, 1), (0, 0), (-6, -3), (6, -3), (0, -3), (-3, -1.5), (3, -1.5)]
    return build(name, (1, 1), inner, seed=117, spawns=spawns, blocks=blocks)

ALL = [room_ossuary, room_gallery, room_hall, room_candles, room_graves, room_start, room_shop, room_pillars, room_altar, room_bonepit]

if __name__ == '__main__':
    import sys, time
    from PIL import Image
    outd = r'C:/Temp/claude/C--GitHub-Harpe-The-Divine-Vestige/82f0c1ae-bd53-4737-8386-1836ba896e5c/scratchpad'
    ims = []
    for fn in ALL:
        t = time.time(); room, plugs = fn(); print(fn.__name__, '%.1fs' % (time.time() - t), 'plug', len(plugs))
        ims.append(room.render())
    w = max(i.width for i in ims); h = sum(i.height for i in ims) + 10 * len(ims)
    sheet = Image.new('RGBA', (w, h), (80, 0, 0, 255)); y = 0
    for i in ims: sheet.paste(i, (0, y)); y += i.height + 10
    sheet.save(outd + '/crypt_rooms_v1.png'); print(sheet.size)
