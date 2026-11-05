"""Expand the nine authored layouts without stretching sprites or actors.

Run from the repository root: python3 Tools/resize_level_layouts.py --apply
The original column counts are checked so this migration cannot expand twice.
No Unity process or external Python package is required.
"""
from pathlib import Path
from collections import Counter
import argparse
import math
import re

ROOT = Path(__file__).resolve().parents[1]
CELL = .74
PLAYER = '1e84755244ded9644b5566b68794635d'
CHEST = '2819a1c7437f97e4dbdeb96c27a2887d'
HALL = '3c1ef52d22f352fc967d29bebcf19891'
HUD = '08c14b1224104fe429c3da93714f6dd0'
COUNTS = dict(Tutorial=53, **{f'CombatLevel{i}': 57 if i == 6 else 69 for i in range(1, 7)}, Lobby=78, ShopTest=26)


def number(value):
    return f'{value:.7f}'.rstrip('0').rstrip('.') if abs(value) > .00000005 else '0'


class Scene:
    def __init__(self, path):
        self.path = path
        self.parts = re.split(r'(?=^--- !u!)', path.read_text(), flags=re.M)
        self.docs, self.names, self.transforms, self.components = {}, {}, {}, {}
        self.extra = []
        for index, body in enumerate(self.parts):
            header = re.match(r'--- !u!(\d+) &(-?\d+)', body)
            if not header:
                continue
            kind, identifier = header.groups()
            self.docs[identifier] = kind, index
            name = re.search(r'^  m_Name: (.*)$', body, re.M)
            owner = re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}', body, re.M)
            if kind == '1' and name:
                self.names[identifier] = name[1]
            if owner:
                self.components.setdefault(owner[1], []).append(identifier)
                if kind == '4' and 'm_LocalPosition:' in body:
                    self.transforms[owner[1]] = identifier

    def body(self, identifier):
        return self.parts[self.docs[identifier][1]]

    def put(self, identifier, body):
        self.parts[self.docs[identifier][1]] = body

    def named(self, name):
        matches = [i for i, label in self.names.items() if label == name]
        assert len(matches) == 1, (self.path, name)
        return matches[0]

    def component(self, owner, kind):
        matches = [i for i in self.components[owner] if self.docs[i][0] == kind]
        assert len(matches) == 1, (owner, kind)
        return matches[0]

    def vector(self, identifier, field):
        match = re.search(r'^  ' + field + r': \{([^}]+)\}', self.body(identifier), re.M)
        assert match, (identifier, field)
        return {key: float(value) for key, value in re.findall(r'(\w+): ([^,}]+)', match[1])}

    def set_vector(self, identifier, field, **values):
        def replace(match):
            fields = re.sub(r'(\w+): ([^,}]+)', lambda m: m[1] + ': ' + number(values[m[1]]) if m[1] in values else m[0], match[1])
            return '  ' + field + ': {' + fields + '}'
        body, count = re.subn(r'^  ' + field + r': \{([^}]+)\}', replace, self.body(identifier), flags=re.M)
        assert count == 1
        self.put(identifier, body)

    def scalar(self, identifier, field, value):
        body = self.body(identifier)
        line = '  ' + field + ': ' + (number(value) if isinstance(value, (int, float)) else value)
        if re.search(r'^  ' + field + ':', body, re.M):
            body = re.sub(r'^  ' + field + r':[^\n]*', line, body, flags=re.M)
        else:
            body = body.rstrip() + '\n' + line + '\n'
        self.put(identifier, body)

    def children(self, transform, children):
        body, count = re.subn(r'^  m_Children:(?: \[\])?\n(?:  - \{fileID: [^\n]+\n)*',
            '  m_Children:\n' + ''.join(f'  - {{fileID: {i}}}\n' for i in children), self.body(transform), flags=re.M)
        assert count == 1
        self.put(transform, body)

    def text(self):
        text = ''.join(self.parts)
        marker = '--- !u!1660057539 &9223372036854775807\n'
        assert marker in text
        return text.replace(marker, ''.join(self.extra) + marker, 1)


def override(body, prop, value, target=None):
    pattern = (r'(    - target: \{fileID: ' + (str(target) if target else r'-?\d+') +
               r', guid: \w+, type: 3\}\n      propertyPath: ' + re.escape(prop) + r'\n      value: )([^\n]*)')
    body, count = re.subn(pattern, lambda m: m[1] + str(value), body)
    assert count == 1, (prop, target, count)
    return body


def prefab_position(body, axis):
    return float(re.search(r'propertyPath: m_LocalPosition\.' + axis + r'\n      value: ([^\n]+)', body)[1])


def tile_entries(scene, tilemap):
    return {(int(m[1]), int(m[2])): m[3] for m in re.finditer(
        r'^  - first: \{x: (-?\d+), y: (-?\d+), z: 0\}\n([\s\S]*?)(?=^  - first:|^  m_AnimatedTiles:)', scene.body(tilemap), re.M)}


def serialize_tiles(scene, tilemap, tiles, width):
    body = scene.body(tilemap)
    entries = ''.join(f'  - first: {{x: {x}, y: {y}, z: 0}}\n' + tiles[x, y] for x, y in sorted(tiles, key=lambda p: (p[1], p[0])))
    body, count = re.subn(r'^  m_Tiles:\n[\s\S]*?(?=^  m_AnimatedTiles:)', '  m_Tiles:\n' + entries, body, flags=re.M)
    assert count == 1
    for array, field in [('m_TileAssetArray', 'm_TileIndex'), ('m_TileSpriteArray', 'm_TileSpriteIndex'),
                         ('m_TileMatrixArray', 'm_TileMatrixIndex'), ('m_TileColorArray', 'm_TileColorIndex')]:
        counts = Counter(int(re.search(field + r': (\d+)', value)[1]) for value in tiles.values())
        def change(match):
            index = -1
            def replace(_):
                nonlocal index
                index += 1
                return '  - m_RefCount: ' + str(counts[index])
            return '  ' + array + ':\n' + re.sub(r'^  - m_RefCount: \d+', replace, match[1], flags=re.M)
        body = re.sub(r'^  ' + array + r':\n([\s\S]*?)(?=^  \w)', change, body, flags=re.M)
    scene.put(tilemap, body)
    bottom, top = min(y for x, y in tiles), max(y for x, y in tiles)
    scene.set_vector(tilemap, 'm_Origin', x=0, y=bottom)
    scene.set_vector(tilemap, 'm_Size', x=width, y=top-bottom+1)


def reachable(platforms, width):
    """Conservative jump connections: 3-tile rises, normal movement, no dash."""
    reached = set()
    sources = [(0, width, 0)]
    # Semi-implicit integration applies gravity before each position step.
    velocity = 15.64 - 39.24*.02*.5
    while True:
        added = []
        for i, (left, right, height) in enumerate(platforms):
            if i in reached:
                continue
            for a, b, h in sources:
                rise = (height-h) * CELL
                if rise < -.01 or rise > 3*CELL+.001:
                    continue
                time = (velocity + math.sqrt(velocity**2 - 2*39.24*rise))/39.24
                gap = max(0, left-b, a-right) * CELL
                if gap + .4 <= 7*time:
                    added.append(i)
                    break
        if not added:
            return reached
        reached.update(added)
        sources.extend(platforms[i] for i in added)


def connect_platforms(platforms, width, low=0, high=None):
    high = width if high is None else high
    links = []
    while True:
        all_platforms = platforms + links
        reached = reachable(all_platforms, width)
        missing = [p for i, p in enumerate(platforms) if i not in reached]
        if not missing:
            return links
        left, right, height = min(missing, key=lambda p: (p[2], p[0]))
        # Reuse nearby reachable tiers before building a staircase from the floor.
        candidates = []
        for source in [(0, width, 0)] + [all_platforms[i] for i in reached]:
            if not 0 <= height-source[2] <= 6:
                continue
            h = source[2] + (height-source[2])//2
            for x in [left-3, right]:
                if x < low or x+3 > high:
                    continue
                link = (x, x+3, h)
                proposed = all_platforms+[link]
                connected = reachable(proposed, width)
                if len(proposed)-1 in connected and all_platforms.index((left,right,height)) in connected:
                    candidates.append(link)
        if candidates:
            links.append(min(candidates, key=lambda p: (p[2],p[0])))
            continue
        steps = list(range(3, height, 3))
        previous_count = len(links)
        # Keep the original ledge and approach it on a short adjacent staircase.
        go_left = left - len(steps)*3 >= low
        for i, h in enumerate(steps):
            x = left - (len(steps)-i)*3 if go_left else right + (len(steps)-i-1)*3
            x = max(low, min(high-3, x))
            link = (x, x+3, h)
            if link not in all_platforms and link not in links:
                links.append(link)
        assert previous_count < len(links) < 60 and steps, ('Cannot connect platform', missing)


def rebuild_hall(scene, left, width):
    instances = [i for i, (kind, _) in scene.docs.items() if kind == '1001' and f'guid: {HALL},' in scene.body(i)]
    templates = [scene.body(i) for i in instances]
    assert templates
    for i in instances:
        scene.put(i, '')
    for i, (kind, _) in scene.docs.items():
        body = scene.body(i)
        if kind == '4' and 'stripped\n' in body and f'guid: {HALL},' in body:
            scene.put(i, '')
    children = []
    count = math.ceil(width/7.4)+2
    # A room has one floor and ceiling. Height comes from modular wall/column pieces.
    for row in range(1):
        for column in range(count):
            index = row*count+column
            identifier = 6200000010+index*10
            strip = identifier+1
            body = re.sub(r'^--- !u!1001 &\d+', f'--- !u!1001 &{identifier}', templates[column % len(templates)])
            body = override(body, 'm_Name', f'Interior Bay {row}-{column:02d}', 1000)
            body = override(body, 'm_LocalPosition.x', number(left-3.7+column*7.4), 1001)
            body = override(body, 'm_LocalPosition.y', number(row*8.14), 1001)
            scene.extra.append(body)
            scene.extra.append(f'''--- !u!4 &{strip} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 1001, guid: {HALL}, type: 3}}
  m_PrefabInstance: {{fileID: {identifier}}}
  m_PrefabAsset: {{fileID: 0}}
''')
            children.append(strip)
    scene.children(scene.transforms[scene.named('Interior Scenery')], children)


def resize(name):
    designed = name not in ['Lobby', 'ShopTest']
    path = ROOT/'Assets/Scenes/Gameplay'/('Designed' if designed else '')/(name+'.unity')
    scene = Scene(path)
    tilemap = next(i for i, (kind, _) in scene.docs.items() if kind == '1839735485')
    old_columns = int(scene.vector(tilemap, 'm_Size')['x'])
    if old_columns == COUNTS[name]*2:
        print(name, 'already expanded')
        return None
    assert old_columns == COUNTS[name], (name, old_columns)
    terrain = scene.transforms[scene.named('Platformer Terrain' if designed else 'Platformer Floor')]
    grid = scene.vector(terrain, 'm_LocalPosition')
    side = scene.vector(terrain, 'm_LocalScale')['x']
    left, floor = grid['x'], grid['y']
    width = old_columns*side
    pivot = left if designed else left+width*.5
    project = lambda x: pivot+2*(x-pivot)
    new_left = project(left)
    scene.set_vector(terrain, 'm_LocalPosition', x=new_left)
    old_tiles = tile_entries(scene, tilemap)
    templates = {int(re.search(r'm_TileIndex: (\d+)', b)[1]): b for b in old_tiles.values()}
    original_platforms = []
    collider_parent = scene.transforms[scene.named('Terrain Colliders')] if designed else None
    enemy_parent = scene.transforms[scene.named('Enemies')] if designed else None
    ledges = []
    for owner, transform in scene.transforms.items():
        if scene.names[owner] != 'One-way ledge':
            continue
        position = scene.vector(transform, 'm_LocalPosition')
        box = scene.component(owner, '61')
        size = scene.vector(box, 'm_Size')
        a = round((position['x']-size['x']*.5-left)/side)
        b = round((position['x']+size['x']*.5-left)/side)
        h = round((position['y']+size['y']*.5-floor)/side)
        original_platforms.append((a, b, h))
        ledges.append((owner, transform, box, position, size))
    expanded = [(a*2, b*2, h*2) for a, b, h in original_platforms]
    low, high = 0, old_columns*2
    if name == 'CombatLevel6':
        seals = [scene.vector(scene.transforms[scene.named(n)], 'm_LocalPosition')['x'] for n in ['Entrance Seal', 'Exit Seal']]
        low, high = math.ceil((project(seals[0])-new_left)/side)+1, math.floor((project(seals[1])-new_left)/side)-1
    links = connect_platforms(expanded, old_columns*2, low, high) if designed else []
    highest = max([p[2] for p in expanded+links] or [0])*side+floor

    def support(x, feet):
        surfaces = [floor] + [floor+h*side for a, b, h in original_platforms if left+a*side-.05 <= x <= left+b*side+.05]
        return min(surfaces, key=lambda y: abs(feet-y))

    # Geometry expands; actor dimensions and their offset above the supporting surface do not.
    for owner, transform in scene.transforms.items():
        label = scene.names[owner]
        position = scene.vector(transform, 'm_LocalPosition')
        parent = re.search(r'm_Father: \{fileID: (-?\d+)\}', scene.body(transform))[1]
        if designed and parent == enemy_parent:
            body = next(i for i in scene.components[owner] if scene.docs[i][0] in ['70', '61'])
            half = scene.vector(body, 'm_Size')['y']*abs(scene.vector(transform, 'm_LocalScale')['y'])*.5
            surface = support(position['x'], position['y']-half)
            scene.set_vector(transform, 'm_LocalPosition', x=project(position['x']), y=position['y']+surface-floor)
        elif label in ['Return Door', 'Tutorial Sign', 'Foundation Anchor'] or label.startswith('Branch Slot '):
            y = position['y']
            if label == 'Tutorial Sign':
                surfaces = [floor] + [floor+h*side for a,b,h in original_platforms if left+a*side-1 <= position['x'] <= left+b*side+1 and floor+h*side <= y-1.5]
                y += max(surfaces)-floor
            scene.set_vector(transform, 'm_LocalPosition', x=project(position['x']), y=y)
        elif label == 'Ground':
            scene.set_vector(transform, 'm_LocalPosition', x=project(position['x']))
            box = scene.component(owner, '61')
            scene.set_vector(box, 'm_Size', x=scene.vector(box, 'm_Size')['x']*2)
        elif label in ['Left boundary', 'Right boundary', 'Room boundary', 'Entrance Seal', 'Exit Seal']:
            top = max(12, highest+6)
            bottom = floor-1
            box = scene.component(owner, '61')
            old_height = scene.vector(box, 'm_Size')['y']
            new_height = top-bottom
            # Room boundary transforms have unit scale; seals have separate visual strips.
            x = project(position['x'])
            if label == 'Left boundary': x = new_left-.25
            if label == 'Right boundary': x = new_left+width*2+.25
            scene.set_vector(transform, 'm_LocalPosition', x=x, y=(top+bottom)*.5)
            scene.set_vector(box, 'm_Size', y=new_height)
            if 'Seal' in label:
                for child_owner, child in scene.transforms.items():
                    if f'm_Father: {{fileID: {transform}}}' in scene.body(child):
                        scale = scene.vector(child, 'm_LocalScale')
                        scene.set_vector(child, 'm_LocalScale', y=scale['y']*new_height/old_height)

    for owner, transform, box, pos, size in ledges:
        top = floor+2*(pos['y']+size['y']*.5-floor)
        scene.set_vector(transform, 'm_LocalPosition', x=project(pos['x']), y=top-size['y']*.5)
        scene.set_vector(box, 'm_Size', x=size['x']*2)

    # Player/chest/shop instances keep their prefab links and all gameplay overrides.
    for identifier, (kind, _) in scene.docs.items():
        body = scene.body(identifier)
        if kind == '1001' and body and not any(f'guid: {g},' in body for g in [HALL, HUD]):
            guid = re.search(r'm_SourcePrefab:.*guid: (\w+)', body)[1]
            x, y = prefab_position(body, 'x'), prefab_position(body, 'y')
            if guid in [PLAYER, CHEST] and designed:
                surface = support(x, y-(1 if guid == PLAYER else .45))
                body = override(body, 'm_LocalPosition.y', number(y+surface-floor))
            body = override(body, 'm_LocalPosition.x', number(project(x)))
            scene.put(identifier, body)
        elif kind == '114':
            if '::LobbyCameraFollow' in body:
                min_x = project(float(re.search(r'MinX: ([^\n]+)', body)[1])) if name == 'Lobby' else new_left
                max_x = project(float(re.search(r'MaxX: ([^\n]+)', body)[1])) if name == 'Lobby' else new_left+2*width
                scene.scalar(identifier, 'MinX', min_x)
                scene.scalar(identifier, 'MaxX', max_x)
                if designed:
                    scene.scalar(identifier, 'FollowVertical', 1)
                    scene.scalar(identifier, 'MinY', 0)
                    scene.scalar(identifier, 'MaxY', max(0, highest+2))
            if '::LobbySceneController' in body:
                if name == 'Lobby':
                    scene.scalar(identifier, 'lobbyMinX', project(-23))
                    scene.scalar(identifier, 'lobbyMaxX', project(31))
                    scene.set_vector(identifier, 'lobbyTitlePosition', x=project(-15), y=2.3) if 'lobbyTitlePosition:' in scene.body(identifier) else scene.scalar(identifier, 'lobbyTitlePosition', '{x: '+number(project(-15))+', y: 2.3}')
                else:
                    scene.scalar(identifier, 'stageMinX', new_left+.6)
                    scene.scalar(identifier, 'stageMaxX', new_left+width*2-.6)
                    scene.scalar(identifier, 'stageMaxY', max(8, highest+5))
            if '::DesignedLevelEncounter' in body and name == 'CombatLevel6':
                scene.scalar(identifier, 'arenaMinY', floor-.5)
                scene.scalar(identifier, 'arenaMaxY', highest+3)

    # Clone the authored one-way collider/effector setup for the connecting steps.
    if links:
        template_owner = ledges[0][0]
        originals = scene.components[template_owner]
        children = re.findall(r'^  - \{fileID: (\d+)\}', scene.body(collider_parent), re.M)
        for index, (a, b, h) in enumerate(links):
            base = 6300000000+index*10
            mapping = {template_owner: str(base)}
            mapping.update({i: str(base+{'4':1,'251':2,'61':3}[scene.docs[i][0]]) for i in originals})
            for old in [template_owner]+originals:
                body = scene.body(old)
                body = re.sub(r'^--- !u!(\d+) &\d+', lambda m: '--- !u!'+m[1]+' &'+mapping[old], body, flags=re.M)
                body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: '+mapping.get(m[1],m[1]), body)
                kind = '1' if old == template_owner else scene.docs[old][0]
                if kind == '1': body = body.replace('m_Name: One-way ledge', f'm_Name: Jump Link {index+1:02d}')
                if kind == '4': body = re.sub(r'^  m_LocalPosition:.*', f'  m_LocalPosition: {{x: {number(new_left+(a+b)*.5*side)}, y: {number(floor+h*side-.1)}, z: 0}}', body, flags=re.M)
                if kind == '61': body = re.sub(r'^  m_Size:.*', f'  m_Size: {{x: {number((b-a)*side)}, y: 0.2}}', body, flags=re.M)
                scene.extra.append(body)
            children.append(str(base+1))
        scene.children(collider_parent, children)

    player_instance = next(i for i, (kind, _) in scene.docs.items()
                           if kind == '1001' and f'm_SourcePrefab: {{fileID: 100100000, guid: {PLAYER},' in scene.body(i))
    camera_transform = scene.transforms[scene.named('Main Camera')]
    scene.set_vector(camera_transform, 'm_LocalPosition', x=prefab_position(scene.body(player_instance), 'x')+2, y=0)

    # Tile art stays square and single-row ledges retain their original thickness.
    tiles = {}
    depth = abs(int(scene.vector(tilemap, 'm_Origin')['y']))*2
    for y in range(-depth, 0):
        for x in range(old_columns*2):
            tiles[x,y] = templates[(x % 4)+(0 if y == -1 else 4)]
    for a,b,h in expanded+links:
        for x in range(a,b):
            tiles[x,h-1] = templates[8 if x == a else 10 if x == b-1 else 9]
    serialize_tiles(scene, tilemap, tiles, old_columns*2)
    rebuild_hall(scene, new_left, width*2)
    report = dict(name=name, oldColumns=old_columns, columns=old_columns*2, platforms=len(expanded), links=links,
                  oldPlatforms=original_platforms, newPlatforms=expanded, cell=side, highest=highest)
    print(name, f'{old_columns} -> {old_columns*2} columns; {len(expanded)} ledges, {len(links)} links; top {number(highest)}')
    return scene, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    # Compute every scene before writing; unexpected source layouts abort the migration.
    results = [result for name in COUNTS if (result := resize(name)) is not None]
    if args.apply:
        for scene, _ in results:
            scene.path.write_text(scene.text())
