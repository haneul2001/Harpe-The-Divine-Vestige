# 1층 특수 방 — 보물방(낡은 감옥 창고), 보스방(심층 왕좌).
# 소품은 팩의 낱개 그림을 방 좌표(발 기준 가운데)에 놓는다. 임포터가 발밑에 콜라이더를 붙인다.
from roombuild import build

RM = 'Assets/ThirdParty/RafaelMatos'
OP = RM + '/ERW - Old Prison/Props/atlas props - individual sprites/'
DP = RM + '/ERW - The Depths/Props/Static/Props-individual sprites/'

def p(path, x, y, passable=False, broken=None, gold=None): return (path, x, y, passable, broken, gold)

def crate(n, x, y):
    """부서지는 나무 상자 — 'crate N'과 짝인 'crate - broken - N' 그림으로 부서진다. 1~4는 상자, 5~8은 작은 통"""
    return p(OP + f'crate {n}.png', x, y, broken=OP + f'crate - broken - {n}.png')

def room_treasure(name='Room_Cry_Treasure'):
    """감옥 창고 — 술통·금은 더미·궤짝. 1x1. 문은 왼쪽 하나만(막다른 방)이지만 전부 뚫어 두고 생성기가 막는다."""
    inner = ['.' * 18] * 9
    props = []
    # 문 앞 통로(위·아래 가운데 6칸, 좌우 끝 2칸)는 비운다 — 옆문은 x ±7 바깥, 아래 문은 y -2.5 아래
    # 금이 담긴 통 두 개 — 가운데 (나무 상자 ±3.5 사이)
    # 부서지면 골드 20. 같은 크기·같은 갈색 테의 부서진 통 그림과 짝
    props += [p(OP + 'barrel - 1 gold.png', -1.2, -1.3, broken=OP + 'barrel - broken - 12.png', gold=(20, 20)),
              p(OP + 'barrel - 2 gold.png', 1.2, -1.3, broken=OP + 'barrel - broken - 13.png', gold=(20, 20))]
    # 왼쪽 자루, 왼쪽 아래 돌 궤짝
    props += [p(OP + 'supply - 1.png', -6.0, 0.0), p(OP + 'stone chest 2.png', -6.0, -4.0)]
    # 부서지는 나무 상자 — 체력 100, 부서지면 골드 5~10
    props += [crate(1, 5.0, -4.0), crate(2, 6.2, -4.0), crate(3, 5.6, -3.0),
              crate(4, -3.5, -1.0), crate(1, 3.5, -1.0), crate(2, -4.3, -2.6)]
    # 매달린 새장 (벽 앞면에 건다)
    props += [p(OP + 'suspended cage - 1.png', 8.0, 2.6)]
    return build(name, (1, 1), inner, seed=201, kind='Treasure', theme='oldprison', props=props, spawns=[])

def room_boss(name='Room_Cry_Boss'):
    """심층 왕좌실 — 1x2(20x22). 바닥 전체가 깔개. 입구는 아래 가운데 하나(1칸 폭 방이라 아래 문이 곧 가운데)."""
    W = 18
    inner = []
    for y in range(20):                       # 안쪽 y 0 = 방 격자 1행
        inner.append(('.' if y < 3 else ';') * W)   # Depths 벽 앞면(3줄) 아래부터 끝까지 깔개
    props = []
    # 왕좌: 발 y 5 (그림 5~9, 윗부분이 벽 앞면에 기댄다). 좁은 융단은 앞으로 흘러내린다 — 밟고 지나간다
    props += [p(DP + 'boss-carpet.png', 0.0, 3.0, passable=True), p(DP + 'boss-throne1.png', 0.0, 5.0)]
    # 왕좌 양옆 제단(어두운 쪽 둘), 그 바깥 황금 석상
    props += [p(DP + 'stand1.png', -3.0, 5.0), p(DP + 'stand1.png', 3.0, 5.0)]
    props += [p(DP + 'golden statues_0.png', -6.5, 6.0), p(DP + 'golden statues_1.png', 6.5, 6.0)]
    # 모서리 결정·항아리
    props += [p(DP + 'Crystals1_0.png', -8.0, -8.5), p(DP + 'Crystals1_1.png', 8.0, -8.5)]
    props += [p(DP + 'pots1_0.png', -6.0, -8.5), p(DP + 'pots1_1.png', 6.0, -8.5)]
    spawns = [(0.0, 0.0)]
    # 문은 남쪽 입구 하나 — 생성기는 보스 방을 늘 아래 방에서 올라오게 놓는다. 나머지 자리는 처음부터 벽
    return build(name, (1, 2), inner, seed=202, kind='Boss', theme='depths', props=props, spawns=spawns, doors=[('down', 0)])

def room_dragon(name='Room_Cry_DragonLair'):
    """2층 용의 둥지 — 2x2(40x22). 용이 날아다니며 메테오·장판을 깔아야 해서 바닥은 통째로 비운다.
    소품은 네 귀퉁이와 윗벽 양끝에만 — 가운데로 들어오면 장판을 피할 자리를 가린다. 입구는 아래 가운데 하나."""
    W = 38
    inner = ['.' * W for _ in range(20)]
    props = []
    props += [p(DP + 'golden statues_0.png', -16.0, 6.0), p(DP + 'golden statues_1.png', 16.0, 6.0)]
    props += [p(DP + 'Crystals1_0.png', -17.5, -8.5), p(DP + 'Crystals1_1.png', 17.5, -8.5)]
    props += [p(DP + 'pots1_0.png', -15.5, -8.5), p(DP + 'pots1_1.png', 15.5, -8.5)]
    spawns = [(0.0, 1.5)]
    return build(name, (2, 2), inner, seed=303, kind='Boss', theme='depths', props=props, spawns=spawns, doors=[('down', -1)])

def room_reaper(name='Room_Sw_ReaperLair'):
    """3층 사신의 제단 — 2x2(40x22). 분신·결계·심판 안전지대가 방 전체를 쓰므로 바닥은 비운다. 입구는 아래 가운데 하나."""
    W = 38
    inner = ['.' * W for _ in range(20)]
    spawns = [(0.0, 1.5)]
    return build(name, (2, 2), inner, seed=404, kind='Boss', theme='sewers', props=[], spawns=spawns, doors=[('down', -1)])

ALL = [room_treasure, room_boss, room_dragon, room_reaper]

if __name__ == '__main__':
    import time
    from PIL import Image
    outd = r'C:/Temp/claude/C--GitHub-Harpe-The-Divine-Vestige/82f0c1ae-bd53-4737-8386-1836ba896e5c/scratchpad'
    ims = []
    for fn in ALL:
        t = time.time(); room, plugs = fn(); print(fn.__name__, '%.1fs' % (time.time() - t), 'plug', len(plugs)); ims.append(room.render())
    w = max(i.width for i in ims); h = sum(i.height for i in ims) + 10 * len(ims)
    sheet = Image.new('RGBA', (w, h), (80, 0, 0, 255)); y = 0
    for i in ims: sheet.paste(i, (0, y)); y += i.height + 10
    sheet.save(outd + '/special_rooms_v1.png'); print(sheet.size)
