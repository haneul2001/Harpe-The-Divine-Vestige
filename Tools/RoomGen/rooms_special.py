# 1층 특수 방 — 보물방(낡은 감옥 창고), 보스방(심층 왕좌).
# 소품은 팩의 낱개 그림을 방 좌표(발 기준 가운데)에 놓는다. 임포터가 발밑에 콜라이더를 붙인다.
from roombuild import build

RM = 'Assets/ThirdParty/RafaelMatos'
OP = RM + '/ERW - Old Prison/Props/atlas props - individual sprites/'
DP = RM + '/ERW - The Depths/Props/Static/Props-individual sprites/'

def p(path, x, y): return (path, x, y)

def room_treasure(name='Room_Cry_Treasure'):
    """감옥 창고 — 술통·금은 더미·궤짝. 1x1. 문은 왼쪽 하나만(막다른 방)이지만 전부 뚫어 두고 생성기가 막는다."""
    inner = ['.' * 18] * 9
    props = []
    # 위쪽 벽 앞 술통 열 (2칸 높이 그림, 벽 앞면 아래에 서게)
    for i, n in enumerate(['barrel - 10', 'barrel - 1 gold', 'barrel - 11', 'barrel - 22 silver', 'barrel - 12', 'barrel - 2 gold']):
        props.append(p(OP + n + '.png', -7.5 + i * 3.0, 1.0))
    # 큰 통 두 개(2x3 그림)
    props += [p(OP + 'barrel - color scheme 1 - 1.png', -3.0, -0.5), p(OP + 'barrel - color scheme 1 - 2.png', 3.0, -0.5)]
    # 금·은 더미
    props += [p(OP + 'gold 1.png', 0.5, -3.5), p(OP + 'silver 1.png', 5.5, -2.0), p(OP + 'gold 2.png', -5.5, -2.5)]
    # 상자·궤짝·자루
    props += [p(OP + 'chest - 1.png', 0.0, -1.5), p(OP + 'stone chest 2.png', -6.5, -4.0),
              p(OP + 'crate 1.png', 7.5, -4.0), p(OP + 'crate 2.png', 8.5, -4.0), p(OP + 'crate 3.png', 8.0, -3.0),
              p(OP + 'supply - 1.png', -8.0, 0.0), p(OP + 'supply - 2.png', 7.5, 0.0), p(OP + 'supply - 10.png', 8.5, -1.5)]
    # 매달린 새장 (벽 앞)
    props += [p(OP + 'suspended cage - 1.png', 8.5, 1.0)]
    return build(name, (1, 1), inner, seed=201, kind='Treasure', theme='oldprison', props=props, spawns=[])

def room_boss(name='Room_Cry_Boss'):
    """심층 왕좌실 — 2x2(40x22). 위쪽 가운데 카펫 끝에 왕좌, 좌우 황금 석상, 위 벽 앞 기둥, 모서리 결정."""
    W = 38
    inner = []
    for y in range(20):
        row = ['.'] * W
        if 2 <= y <= 15:
            for x in range(15, 23): row[x] = ','    # 가운데 통로 무늬
        inner.append(''.join(row))
    props = []
    # 왕좌 + 카펫 (위 가운데). 카펫은 장식이라 막지 않는다 — 임포터는 소품 전부에 콜라이더를 붙이므로 카펫은 타일 대신 그림으로 두되 위치를 왕좌 밑으로
    props += [p(DP + 'boss-carpet.png', 0.0, 4.5), p(DP + 'boss-throne1.png', 0.0, 6.5)]
    # 좌우 황금 석상
    props += [p(DP + 'golden statues_0.png', -6.0, 6.0), p(DP + 'golden statues_1.png', 6.0, 6.0)]
    # 위 벽 앞 기둥들
    for x in (-15.0, -10.0, 10.0, 15.0): props.append(p(DP + 'pillars-bg_1.png', x, 6.0))
    # 불단지 (왕좌 양옆), 결정(모서리), 항아리
    props += [p(DP + 'stand1.png', -3.0, 5.0), p(DP + 'stand2.png', 3.0, 5.0)]
    props += [p(DP + 'Crystals1_0.png', -17.5, -8.5), p(DP + 'Crystals1_1.png', 17.5, -8.5), p(DP + 'Crystals1_2.png', -17.5, 7.0), p(DP + 'Crystals1_3.png', 17.5, 7.0)]
    props += [p(DP + 'pots1_0.png', -12.0, -8.5), p(DP + 'pots1_1.png', 12.0, -8.5), p(DP + 'sword stuck in the ground.png', 0.0, -8.0)]
    spawns = [(0.0, 0.0)]
    return build(name, (2, 2), inner, seed=202, kind='Boss', theme='depths', props=props, spawns=spawns)

ALL = [room_treasure, room_boss]

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
