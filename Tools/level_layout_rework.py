"""Apply the workbook layout to the nine owned gameplay scenes.

Run from the repository root. The default command reports the scope; use
--apply to update only the nine scenes listed in this file. The script is
idempotent and imports serialization helpers from the read-only legacy tool.
"""
from pathlib import Path
import argparse
import math
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
import resize_level_layouts as layout  # Existing read-only YAML helpers.


DESIGNED = ["Tutorial"] + [f"CombatLevel{i}" for i in range(1, 7)]
SCENES = DESIGNED + ["Lobby", "ShopTest"]
PLAYER_GUID = layout.PLAYER
SHOP_GUID = "2c1040f22a00d2745a828817ef6e8628"
SCENE_ROOT_ID = "9223372036854775807"
ANCHOR_BASE = 7_190_000_000
LOBBY_WIDTH = 48
SHOP_WIDTH = 40
EXTRA_ENEMIES = {
    "CombatLevel1": ["Believer 3", "Believer 6", "Believer 7", "Dog 5", "Dog 8"],
    "CombatLevel3": ["Executive 7"],
    "CombatLevel4": ["Believer 1"],
}
EXPECTED_ENEMIES = {
    "Tutorial": (1, 0, 0),
    "CombatLevel1": (1, 2, 0),
    "CombatLevel2": (4, 2, 2),
    "CombatLevel3": (4, 1, 1),
    "CombatLevel4": (2, 2, 1),
    "CombatLevel5": (4, 1, 1),
    "CombatLevel6": (3, 0, 3),
}
EXPECTED_ENEMY_X = {
    "Tutorial": {"Believer": [27.22], "Executive": [], "Dog": []},
    "CombatLevel1": {"Believer": [27.22], "Executive": [18.34, 55.34], "Dog": []},
    "CombatLevel2": {"Believer": [10.94, 13.90, 31.66, 77.54], "Executive": [16.86, 64.22], "Dog": [37.58, 55.34]},
    "CombatLevel3": {"Believer": [15.38, 31.66, 56.82, 62.74], "Executive": [47.94], "Dog": [21.30]},
    "CombatLevel4": {"Believer": [13.90, 59.78], "Executive": [46.46, 76.06], "Dog": [37.58]},
    "CombatLevel5": {"Believer": [16.86, 30.18, 59.78, 71.62], "Executive": [47.94], "Dog": [34.62]},
    "CombatLevel6": {"Believer": [19.82, 27.22, 39.06], "Executive": [], "Dog": [22.78, 43.50, 47.94]},
}
ANCHORS = {
    "IntroPlayer": (-5.34, -1.98),
    "BelieverEncounter": (19.82, -1.98),
    "PlayerTalk": (19.82, -1.98),
    "BelieverStart": (27.22, -1.98),
    "BelieverNear": (24.26, -1.98),
    "BelieverThrow": (31.66, -1.98),
}


def scene_path(name):
    parent = ROOT / "Assets" / "Scenes" / "Gameplay"
    return parent / "Designed" / f"{name}.unity" if name in DESIGNED else parent / f"{name}.unity"


def game_object_body(name, component_ids):
    return (
        f"""--- !u!1 &{name}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
"""
        + "".join(f"  - component: {{fileID: {identifier}}}\n" for identifier in component_ids)
        + """  m_Layer: 0
  m_Name: PLACEHOLDER
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    )


def transform_body(identifier, owner, parent, x, y, children=()):
    child_lines = "  m_Children: []\n" if not children else (
        "  m_Children:\n" + "".join(f"  - {{fileID: {child}}}\n" for child in children)
    )
    return f"""--- !u!4 &{identifier}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {owner}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {layout.number(x)}, y: {layout.number(y)}, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
{child_lines}  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def add_roots(scene, transform_ids):
    identifier = SCENE_ROOT_ID
    body = scene.body(identifier)
    pattern = r"(^  m_Roots:\n)((?:  - \{fileID: -?\d+\}\n)*)"
    match = re.search(pattern, body, re.M)
    assert match, "SceneRoots list was not found"
    existing = re.findall(r"fileID: (-?\d+)", match[2])
    roots = list(dict.fromkeys(existing + [str(value) for value in transform_ids]))
    replacement = match[1] + "".join(f"  - {{fileID: {root}}}\n" for root in roots)
    scene.put(identifier, body[:match.start()] + replacement + body[match.end():])


def set_object_name(scene, owner, name):
    body = re.sub(r"^  m_Name: .*", "  m_Name: " + name, scene.body(owner), flags=re.M)
    scene.put(owner, body)
    scene.names[owner] = name


def set_transform_position(scene, owner, x=None, y=None):
    transform = scene.transforms[owner]
    values = {}
    if x is not None:
        values["x"] = x
    if y is not None:
        values["y"] = y
    scene.set_vector(transform, "m_LocalPosition", **values)


def named_objects(scene, name):
    return [owner for owner, label in scene.names.items() if label == name]


def prefab_instance(scene, guid, name=None):
    matches = []
    for identifier, (kind, _) in scene.docs.items():
        if kind != "1001":
            continue
        body = scene.body(identifier)
        if f"guid: {guid}," not in body:
            continue
        name_override = re.search(r"propertyPath: m_Name\n      value: ([^\n]+)", body)
        if name is None or (name_override and name_override[1] == name):
            matches.append(identifier)
    assert len(matches) == 1, (scene.path, guid, name, matches)
    return matches[0]


def set_prefab_axis(scene, instance, axis, value):
    body = scene.body(instance)
    prop = f"m_LocalPosition.{axis}"
    source_guid = re.search(r"m_SourcePrefab:.*guid: (\w+)", body)[1]
    existing = re.search(
        r"    - target: \{fileID: (-?\d+), guid: " + re.escape(source_guid)
        + r", type: 3\}\n      propertyPath: " + re.escape(prop) + r"\n",
        body,
    )
    if existing:
        body = layout.override(body, prop, layout.number(value), target=int(existing[1]))
    else:
        sibling = re.search(
            r"    - target: \{fileID: (-?\d+), guid: " + re.escape(source_guid)
            + r", type: 3\}\n      propertyPath: m_LocalPosition\.[xy]\n",
            body,
        )
        assert sibling, (scene.path, "missing prefab root position override", prop)
        entry = (
            "    - target: {fileID: 1001, guid: "
            + source_guid
            + ", type: 3}\n"
            + f"      propertyPath: {prop}\n"
            + f"      value: {layout.number(value)}\n"
            + "      objectReference: {fileID: 0}\n"
        )
        entry = entry.replace("fileID: 1001", "fileID: " + sibling[1], 1)
        assert "    m_RemovedComponents:" in body
        body = body.replace("    m_RemovedComponents:", entry + "    m_RemovedComponents:", 1)
    scene.put(instance, body)


def set_prefab_position(scene, instance, x=None, y=None):
    if x is not None:
        set_prefab_axis(scene, instance, "x", x)
    if y is not None:
        set_prefab_axis(scene, instance, "y", y)


def set_world_position_under_parent(scene, owner, x, y=None):
    transform = scene.transforms[owner]
    parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", scene.body(transform))[1]
    if parent == "0":
        return set_transform_position(scene, owner, x=x, y=y)
    parent_owner = next((go for go, tr in scene.transforms.items() if tr == parent), None)
    assert parent_owner is not None
    parent_pos = scene.vector(parent, "m_LocalPosition")
    parent_scale = scene.vector(parent, "m_LocalScale")
    local_x = (x - parent_pos["x"]) / parent_scale["x"]
    local_y = None if y is None else (y - parent_pos["y"]) / parent_scale["y"]
    set_transform_position(scene, owner, x=local_x, y=local_y)


def resize_floor(scene, name, target_width, left=None):
    terrain_name = "Platformer Floor"
    terrain = scene.transforms[scene.named(terrain_name)]
    tilemap = next(identifier for identifier, (kind, _) in scene.docs.items() if kind == "1839735485")
    current_width = int(scene.vector(tilemap, "m_Size")["x"])
    if left is not None:
        scene.set_vector(terrain, "m_LocalPosition", x=left)
    if current_width == target_width:
        return tilemap
    expected = {"Lobby": 156, "ShopTest": 52}[name]
    assert current_width == expected, (name, current_width, expected, target_width)
    tiles = layout.tile_entries(scene, tilemap)
    assert min(x for x, _ in tiles) == 0 and max(x for x, _ in tiles) == current_width - 1
    cropped = {(x, y): tile for (x, y), tile in tiles.items() if 0 <= x < target_width}
    layout.serialize_tiles(scene, tilemap, cropped, target_width)
    # Keep Unity's original serialized cell order to avoid rewriting every
    # retained tile just because the right side of the map became shorter.
    body = scene.body(tilemap)
    serialized = "".join(
        f"  - first: {{x: {x}, y: {y}, z: 0}}\n" + tile
        for (x, y), tile in cropped.items()
    )
    body, count = re.subn(
        r"^  m_Tiles:\n[\s\S]*?(?=^  m_AnimatedTiles:)",
        "  m_Tiles:\n" + serialized,
        body,
        flags=re.M,
    )
    assert count == 1
    scene.put(tilemap, body)
    return tilemap


def add_tutorial_anchors(scene):
    group_names = named_objects(scene, "TutorialCutsceneAnchors")
    if group_names:
        assert len(group_names) == 1
        group = group_names[0]
        group_transform = scene.transforms[group]
        children = [
            owner for owner, transform in scene.transforms.items()
            if re.search(r"m_Father: \{fileID: " + re.escape(group_transform) + r"\}", scene.body(transform))
        ]
        assert {scene.names[owner] for owner in children} == set(ANCHORS)
        for owner in children:
            x, y = ANCHORS[scene.names[owner]]
            set_transform_position(scene, owner, x=x, y=y)
        return

    ids = [ANCHOR_BASE + offset for offset in range(0, 70, 10)]
    assert not any(str(identifier) in scene.docs for identifier in ids)
    group_go, group_transform = ids[0], ids[0] + 1
    anchor_transforms = []
    group_body = game_object_body(group_go, [group_transform]).replace("m_Name: PLACEHOLDER", "m_Name: TutorialCutsceneAnchors")
    scene.extra.extend([group_body, transform_body(group_transform, group_go, 0, 0, 0, [ids[i] + 1 for i in range(1, 7)])])
    for index, (name, (x, y)) in enumerate(ANCHORS.items(), start=1):
        go = ids[index]
        transform = go + 1
        anchor_transforms.append(transform)
        go_body = game_object_body(go, [transform]).replace("m_Name: PLACEHOLDER", "m_Name: " + name)
        scene.extra.extend([go_body, transform_body(transform, go, group_transform, x, y)])
    add_roots(scene, [group_transform])


def set_believer_story_name(scene):
    existing = named_objects(scene, "TutorialStoryBeliever")
    if existing:
        assert len(existing) == 1
        return
    believers = named_objects(scene, "Believer 1")
    assert len(believers) == 1
    set_object_name(scene, believers[0], "TutorialStoryBeliever")


def apply_tutorial(scene):
    set_believer_story_name(scene)
    story = scene.named("TutorialStoryBeliever")
    set_transform_position(scene, story, x=27.22, y=-1.98)
    add_tutorial_anchors(scene)


def apply_enemy_sheet_counts(scene, name):
    for enemy_name in EXTRA_ENEMIES.get(name, []):
        matches = named_objects(scene, enemy_name)
        assert len(matches) == 1, (name, enemy_name, matches)
        scene.scalar(matches[0], "m_IsActive", 0)
    if name == "CombatLevel1":
        believers = named_objects(scene, "Believer 4")
        assert len(believers) == 1
        set_transform_position(scene, believers[0], x=27.22)


def apply_lobby(scene):
    terrain = scene.transforms[scene.named("Platformer Floor")]
    scale = scene.vector(terrain, "m_LocalScale")["x"]
    player_instance_id = prefab_instance(scene, PLAYER_GUID, "Player")
    player_x = layout.prefab_position(scene.body(player_instance_id), "x")
    left = player_x - 9 * scale  # P is four workbook columns after the L origin.
    tilemap = resize_floor(scene, "Lobby", LOBBY_WIDTH, left)
    width = LOBBY_WIDTH * scale
    right = left + width
    room_center = (left + right) * 0.5

    # Keep the lobby's existing room decoration centered on the shorter floor.
    room = scene.transforms[scene.named("Room")]
    scene.set_vector(room, "m_LocalPosition", x=room_center)
    background = scene.transforms[scene.named("Hub Background Decoration")]
    scene.set_vector(background, "m_LocalPosition", x=room_center)
    for owner in named_objects(scene, "Room boundary"):
        pos = scene.vector(scene.transforms[owner], "m_LocalPosition")
        world_x = left + 2 if pos["x"] < 0 else right - 2
        set_world_position_under_parent(scene, owner, world_x)

    # The workbook places the foundation at U and the three exits at Z, AC, AF.
    marker = lambda column: left + (2 * (column - 12) + 1) * scale
    set_world_position_under_parent(scene, scene.named("Foundation Anchor"), marker(21))
    three = [owner for owner in scene.names if scene.names[owner].startswith("Branch Slot ") and
             re.search(r"m_Father: \{fileID: " + re.escape(scene.transforms[scene.named("Three Candidate Layout")]) + r"\}",
                       scene.body(scene.transforms[owner]))]
    two = [owner for owner in scene.names if scene.names[owner].startswith("Branch Slot ") and
           re.search(r"m_Father: \{fileID: " + re.escape(scene.transforms[scene.named("Two Candidate Layout")]) + r"\}",
                     scene.body(scene.transforms[owner]))]
    assert len(three) == 3 and len(two) == 2
    three.sort(key=lambda owner: int(scene.names[owner].rsplit(" ", 1)[1]))
    two.sort(key=lambda owner: int(scene.names[owner].rsplit(" ", 1)[1]))
    slot_x = [marker(column) for column in (26, 29, 32)]
    for owner, x in zip(three, slot_x):
        set_world_position_under_parent(scene, owner, x)
    for owner, x in zip(two, (slot_x[0], slot_x[2])):
        set_world_position_under_parent(scene, owner, x)

    hall = scene.transforms[scene.named("Interior Scenery")]
    layout.rebuild_hall(scene, left, width)
    # Ensure the generated room pieces remain children of the existing scenery root.
    assert hall == scene.transforms[scene.named("Interior Scenery")]

    for identifier, (kind, _) in scene.docs.items():
        if kind != "114" or "LobbyCameraFollow" not in scene.body(identifier) and "LobbySceneController" not in scene.body(identifier):
            continue
        body = scene.body(identifier)
        if "LobbyCameraFollow" in body:
            scene.scalar(identifier, "MinX", left + 2)
            scene.scalar(identifier, "MaxX", right - 2)
        if "LobbySceneController" in body:
            scene.scalar(identifier, "lobbyMinX", left + 4)
            scene.scalar(identifier, "lobbyMaxX", right - 4)
            scene.set_vector(identifier, "lobbyTitlePosition", x=room_center)


def apply_shop(scene):
    terrain = scene.transforms[scene.named("Platformer Floor")]
    root = scene.vector(terrain, "m_LocalPosition")
    scale = scene.vector(terrain, "m_LocalScale")["x"]
    left = root["x"]
    width = SHOP_WIDTH * scale
    right = left + width
    resize_floor(scene, "ShopTest", SHOP_WIDTH)
    background = scene.transforms[scene.named("Hub Background Decoration")]
    scene.set_vector(background, "m_LocalPosition", x=(left + right) * 0.5)

    player_x = left + 9 * scale  # P marker center.
    player = prefab_instance(scene, PLAYER_GUID, "Player")
    set_prefab_position(scene, player, x=player_x)
    shop = prefab_instance(scene, SHOP_GUID, "Shop")
    set_prefab_position(scene, shop, x=left + 23 * scale)  # Center of the T:X region, W.
    camera = scene.transforms[scene.named("Main Camera")]
    scene.set_vector(camera, "m_LocalPosition", x=player_x + 2)

    layout.rebuild_hall(scene, left, width)
    for identifier, (kind, _) in scene.docs.items():
        if kind != "114":
            continue
        body = scene.body(identifier)
        if "LobbyCameraFollow" in body:
            scene.scalar(identifier, "MinX", left)
            scene.scalar(identifier, "MaxX", right)
        if "LobbySceneController" in body:
            scene.scalar(identifier, "stageMinX", left + 0.6)
            scene.scalar(identifier, "stageMaxX", right - 0.6)


def enabled_enemy_counts(scene):
    enemy_parent = scene.transforms[scene.named("Enemies")]
    counts = {"Believer": 0, "Executive": 0, "Dog": 0}
    for owner, transform in scene.transforms.items():
        parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", scene.body(transform))[1]
        if parent != enemy_parent or "m_IsActive: 0" in scene.body(owner):
            continue
        label = scene.names[owner]
        if label.startswith("Believer") or label == "TutorialStoryBeliever":
            counts["Believer"] += 1
        elif label.startswith("Executive"):
            counts["Executive"] += 1
        elif label.startswith("Dog"):
            counts["Dog"] += 1
    return counts


def enabled_enemy_positions(scene):
    enemy_parent = scene.transforms[scene.named("Enemies")]
    positions = {"Believer": [], "Executive": [], "Dog": []}
    for owner, transform in scene.transforms.items():
        parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", scene.body(transform))[1]
        if parent != enemy_parent or "m_IsActive: 0" in scene.body(owner):
            continue
        label = scene.names[owner]
        kind = "Believer" if label.startswith("Believer") or label == "TutorialStoryBeliever" else (
            "Executive" if label.startswith("Executive") else "Dog" if label.startswith("Dog") else None
        )
        if kind:
            positions[kind].append(scene.vector(transform, "m_LocalPosition")["x"])
    return {kind: sorted(xs) for kind, xs in positions.items()}


def component_value(scene, class_name, field):
    matches = [scene.body(identifier) for identifier, (kind, _) in scene.docs.items()
               if kind == "114" and class_name in scene.body(identifier)]
    assert len(matches) == 1, (scene.path, class_name, len(matches))
    match = re.search(r"^  " + re.escape(field) + r": ([^\n]+)", matches[0], re.M)
    assert match, (scene.path, class_name, field)
    return float(match[1])


def validate():
    assert set(SCENES) == {"Tutorial", *(f"CombatLevel{i}" for i in range(1, 7)), "Lobby", "ShopTest"}
    for name in DESIGNED:
        scene = layout.Scene(scene_path(name))
        expected_width = 106 if name == "Tutorial" else 114 if name == "CombatLevel6" else 138
        tilemap = next(identifier for identifier, (kind, _) in scene.docs.items() if kind == "1839735485")
        assert int(scene.vector(tilemap, "m_Size")["x"]) == expected_width, (name, "tile width")
        counts = enabled_enemy_counts(scene)
        expected = EXPECTED_ENEMIES[name]
        actual = (counts["Believer"], counts["Executive"], counts["Dog"])
        assert actual == expected, (name, "enemy counts", actual, expected)
        positions = enabled_enemy_positions(scene)
        for kind, expected_xs in EXPECTED_ENEMY_X[name].items():
            assert len(positions[kind]) == len(expected_xs) and all(
                math.isclose(actual_x, expected_x, abs_tol=1e-4)
                for actual_x, expected_x in zip(positions[kind], expected_xs)
            ), (name, kind, positions[kind], expected_xs)
        if name == "Tutorial":
            for anchor, (x, y) in ANCHORS.items():
                owner = scene.named(anchor)
                pos = scene.vector(scene.transforms[owner], "m_LocalPosition")
                assert math.isclose(pos["x"], x, abs_tol=1e-4) and math.isclose(pos["y"], y, abs_tol=1e-4)
            assert scene.named("TutorialStoryBeliever")

        # Verify that each designed ledge remains connected by a normal jump.
        terrain = scene.transforms[scene.named("Platformer Terrain")]
        grid = scene.vector(terrain, "m_LocalPosition")
        cell = scene.vector(terrain, "m_LocalScale")["x"]
        platforms = []
        for owner, transform in scene.transforms.items():
            if scene.names.get(owner) not in ("One-way ledge",) and not scene.names.get(owner, "").startswith("Jump Link "):
                continue
            box = scene.component(owner, "61")
            pos = scene.vector(transform, "m_LocalPosition")
            size = scene.vector(box, "m_Size")
            a = round((pos["x"] - size["x"] * 0.5 - grid["x"]) / cell)
            b = round((pos["x"] + size["x"] * 0.5 - grid["x"]) / cell)
            h = round((pos["y"] + size["y"] * 0.5 - grid["y"]) / cell)
            platforms.append((a, b, h))
        reached = layout.reachable(platforms, expected_width)
        assert len(reached) == len(platforms), (name, "unreachable ledges", len(reached), len(platforms))

    lobby = layout.Scene(scene_path("Lobby"))
    lobby_terrain = lobby.transforms[lobby.named("Platformer Floor")]
    lobby_tilemap = next(identifier for identifier, (kind, _) in lobby.docs.items() if kind == "1839735485")
    lobby_scale = lobby.vector(lobby_terrain, "m_LocalScale")["x"]
    lobby_left = lobby.vector(lobby_terrain, "m_LocalPosition")["x"]
    assert int(lobby.vector(lobby_tilemap, "m_Size")["x"]) == LOBBY_WIDTH
    assert math.isclose(lobby_left + 9 * lobby_scale, -28, abs_tol=1e-4)
    for owner, column in ((lobby.named("Foundation Anchor"), 21),):
        p = lobby.vector(lobby.transforms[owner], "m_LocalPosition")
        expected_x = lobby_left + (2 * (column - 12) + 1) * lobby_scale
        assert math.isclose(p["x"], expected_x, abs_tol=1e-4)
    three_parent = lobby.transforms[lobby.named("Three Candidate Layout")]
    two_parent = lobby.transforms[lobby.named("Two Candidate Layout")]
    three_slots = []
    two_slots = []
    for owner, label in lobby.names.items():
        if not label.startswith("Branch Slot "):
            continue
        transform = lobby.transforms[owner]
        parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", lobby.body(transform))[1]
        if parent == three_parent:
            three_slots.append(lobby.vector(transform, "m_LocalPosition")["x"])
        elif parent == two_parent:
            two_slots.append(lobby.vector(transform, "m_LocalPosition")["x"])
    marker = lambda column: lobby_left + (2 * (column - 12) + 1) * lobby_scale
    assert len(three_slots) == 3 and all(math.isclose(a, b, abs_tol=1e-4)
                                          for a, b in zip(sorted(three_slots), map(marker, (26, 29, 32))))
    assert len(two_slots) == 2 and all(math.isclose(a, b, abs_tol=1e-4)
                                       for a, b in zip(sorted(two_slots), (marker(26), marker(32))))
    lobby_right = lobby_left + LOBBY_WIDTH * lobby_scale
    assert math.isclose(component_value(lobby, "LobbyCameraFollow", "MinX"), lobby_left + 2, abs_tol=1e-4)
    assert math.isclose(component_value(lobby, "LobbyCameraFollow", "MaxX"), lobby_right - 2, abs_tol=1e-4)
    assert math.isclose(component_value(lobby, "LobbySceneController", "lobbyMinX"), lobby_left + 4, abs_tol=1e-4)
    assert math.isclose(component_value(lobby, "LobbySceneController", "lobbyMaxX"), lobby_right - 4, abs_tol=1e-4)
    lobby_controller = next(body for identifier, (kind, _) in lobby.docs.items()
                            if kind == "114" and "LobbySceneController" in (body := lobby.body(identifier)))
    title = re.search(r"^  lobbyTitlePosition: \{x: ([^,]+), y: ([^}]+)\}", lobby_controller, re.M)
    assert title and math.isclose(float(title[1]), (lobby_left + lobby_right) * 0.5, abs_tol=1e-4)
    shop = layout.Scene(scene_path("ShopTest"))
    shop_terrain = shop.transforms[shop.named("Platformer Floor")]
    shop_tilemap = next(identifier for identifier, (kind, _) in shop.docs.items() if kind == "1839735485")
    assert int(shop.vector(shop_tilemap, "m_Size")["x"]) == SHOP_WIDTH
    assert math.isclose(layout.prefab_position(shop.body(prefab_instance(shop, PLAYER_GUID, "Player")), "x"),
                        shop.vector(shop_terrain, "m_LocalPosition")["x"] + 9 * shop.vector(shop_terrain, "m_LocalScale")["x"],
                        abs_tol=1e-4)
    shop_left = shop.vector(shop_terrain, "m_LocalPosition")["x"]
    shop_scale = shop.vector(shop_terrain, "m_LocalScale")["x"]
    shop_right = shop_left + SHOP_WIDTH * shop_scale
    shop_instance = prefab_instance(shop, SHOP_GUID, "Shop")
    assert math.isclose(layout.prefab_position(shop.body(shop_instance), "x"), shop_left + 23 * shop_scale,
                        abs_tol=1e-4)
    assert math.isclose(component_value(shop, "LobbyCameraFollow", "MinX"), shop_left, abs_tol=1e-4)
    assert math.isclose(component_value(shop, "LobbyCameraFollow", "MaxX"), shop_right, abs_tol=1e-4)
    assert math.isclose(component_value(shop, "LobbySceneController", "stageMinX"), shop_left + 0.6, abs_tol=1e-4)
    assert math.isclose(component_value(shop, "LobbySceneController", "stageMaxX"), shop_right - 0.6, abs_tol=1e-4)
    print("배치 확인 통과: 제작 씬 9개, 적 수, 지형 폭, 튜토리얼 앵커, 기본 점프 연결")


def apply():
    for name in SCENES:
        path = scene_path(name)
        before = path.read_text()
        scene = layout.Scene(path)
        if name == "Tutorial":
            apply_tutorial(scene)
        elif name in EXTRA_ENEMIES or name == "CombatLevel1":
            apply_enemy_sheet_counts(scene, name)
        elif name == "Lobby":
            apply_lobby(scene)
        elif name == "ShopTest":
            apply_shop(scene)
        after = scene.text()
        if after != before:
            path.write_text(after)
            print("수정:", path.relative_to(ROOT))
        else:
            print("유지:", path.relative_to(ROOT))
    validate()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="저장소 소유 범위의 9개 씬에 배치 적용")
    parser.add_argument("--check", action="store_true", help="현재 9개 씬의 정적 배치 확인")
    args = parser.parse_args()
    if args.apply:
        apply()
    elif args.check:
        validate()
    else:
        print("대상 씬:", ", ".join(SCENES))
        print("변경 적용: python3 Tools/level_layout_rework.py --apply")
        print("정적 확인: python3 Tools/level_layout_rework.py --check")


if __name__ == "__main__":
    main()
