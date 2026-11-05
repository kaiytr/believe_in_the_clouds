"""Apply modular stone interiors and matching terrain to the nine authored scenes.

Uses existing atlas pixels, preserves collision and actor placement, and can be
reapplied after Tools/resize_level_layouts.py without duplicating decoration.
"""
from pathlib import Path
import re
import uuid
import math

from resize_level_layouts import (ROOT, CELL, HALL, COUNTS, Scene, number, override,
                                 rebuild_hall, tile_entries, serialize_tiles)

ATLAS = 'e54146265ed9b7b4b9e28267312f2508'
TILES = 'c897c0fb0e5f7dd4e89de0e2f5059833'
MATERIAL_PATH = 'Assets/Art/LevelTiles/InteriorTerrain.mat'
MATERIAL = uuid.uuid5(uuid.NAMESPACE_URL, 'believe-in-the-clouds/interior/terrain-material').hex
WINDOW_PARTS = [1040, 1140, 1150, 1160, 1170, 1180]
GLASS_TINTS = dict(Tutorial=(.32,.28,.18), Lobby=(.46,.34,.18), ShopTest=(.48,.34,.18),
                   CombatLevel1=(.30,.29,.22), CombatLevel2=(.16,.31,.38),
                   CombatLevel3=(.40,.29,.16), CombatLevel4=(.30,.23,.39),
                   CombatLevel5=(.16,.34,.30), CombatLevel6=(.40,.18,.16))


def add_sprites_and_tiles():
    path = ROOT/'Assets/Art/LevelTiles/PlatformerTerrain.png.meta'
    meta = path.read_text()
    template = re.search(r'^    - serializedVersion: 2\n      name: InteriorWall\n[\s\S]*?(?=^    - serializedVersion:|^    outline:)', meta, re.M)[0]
    sprites = [(f'InteriorGround{x}_{y}', 188700100+y*4+x, (1168+x*16, 96+y*16, 16, 16))
               for y in range(4) for x in range(4)]
    sprites += [('InteriorLedgeLeft',188700200,(208,592,48,32)),
                ('InteriorLedgeRight',188700201,(352,592,48,32)),
                ('InteriorWindowArch',188700210,(1552,112,32,16)),
                ('InteriorWindowJamb',188700211,(1552,128,32,16)),
                ('InteriorWindowSill',188700212,(1552,144,32,16))]
    for name, identifier, (x,y,w,h) in sprites:
        if '      name: '+name+'\n' in meta:
            continue
        body = template.replace('name: InteriorWall','name: '+name).replace('internalID: 188700001','internalID: '+str(identifier))
        for key,value in [('x',x),('y',1280-y-h),('width',w),('height',h)]:
            body = re.sub(r'^        '+key+r': \d+', '        '+key+': '+str(value),body,flags=re.M)
        body = re.sub(r'spriteID: \w+', 'spriteID: '+uuid.uuid5(uuid.NAMESPACE_URL,name).hex,body)
        meta = meta.replace('  externalObjects:',f'  - first:\n      213: {identifier}\n    second: {name}\n  externalObjects:',1)
        meta = meta.replace('    outline: []\n    customData:',body+'    outline: []\n    customData:',1)
        meta = meta.replace('  mipmapLimitGroupName:',f'      {name}: {identifier}\n  mipmapLimitGroupName:',1)
    path.write_text(meta)

    path = ROOT/'Assets/Art/LevelTiles/PlatformerTiles.asset'
    scene = Scene(path)
    template = next(scene.body(i) for i,(kind,_) in scene.docs.items() if kind == '114')
    original_id = re.search(r'^--- !u!114 &(-?\d+)',template)[1]
    additions = []
    for y in range(4):
        for x in range(4):
            identifier = 188720000+y*4+x
            if str(identifier) in scene.docs:
                continue
            body = template.replace('--- !u!114 &'+original_id,'--- !u!114 &'+str(identifier),1)
            body = re.sub(r'^  m_Name:.*',f'  m_Name: InteriorGround{x}_{y}',body,flags=re.M)
            body = re.sub(r'^  m_Sprite:.*',f'  m_Sprite: {{fileID: {188700100+y*4+x}, guid: {ATLAS}, type: 3}}',body,flags=re.M)
            additions.append(body)
    path.write_text(''.join(scene.parts)+''.join(additions))
    scenery_path = ROOT/'Assets/Art/LevelTiles/InteriorScenery.mat'
    material = scenery_path.read_text()
    material = re.sub(r'    - _Color:.*','    - _Color: {r: 1.05, g: 1.1, b: 1.2, a: 1}',material)
    scenery_path.write_text(material)
    material = material.replace('m_Name: InteriorScenery','m_Name: InteriorTerrain')
    material = re.sub(r'    - _Color:.*','    - _Color: {r: 1.65, g: 1.6, b: 1.5, a: 1}',material)
    (ROOT/MATERIAL_PATH).write_text(material)
    (ROOT/(MATERIAL_PATH+'.meta')).write_text(f'''fileFormatVersion: 2
guid: {MATERIAL}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
''')


def sprite(scene, identifier, sprite_id):
    body = re.sub(r'^  m_Sprite:.*',f'  m_Sprite: {{fileID: {sprite_id}, guid: {ATLAS}, type: 3}}',scene.body(identifier),flags=re.M)
    scene.put(identifier,body)


def clone_window(scene, go, name, x, y, sprite_id, tiled=False, size_y=1):
    if str(go) not in scene.docs:
        for old, new, kind in [(1040,go,'1'),(1041,go+1,'4'),(1042,go+2,'212')]:
            body = scene.body(str(old))
            body = re.sub(r'^--- !u!'+kind+r' &\d+',f'--- !u!{kind} &{new}',body,flags=re.M)
            for a,b in [(1040,go),(1041,go+1),(1042,go+2)]:
                body = body.replace(f'fileID: {a}}}',f'fileID: {b}}}')
            if kind == '1': body = re.sub(r'^  m_Name:.*','  m_Name: '+name,body,flags=re.M)
            if kind == '4': body = re.sub(r'^  m_LocalPosition:.*',f'  m_LocalPosition: {{x: {number(x)}, y: {number(y)}, z: 0}}',body,flags=re.M)
            if kind == '212':
                body = re.sub(r'^  m_Sprite:.*',f'  m_Sprite: {{fileID: {sprite_id}, guid: {ATLAS}, type: 3}}',body,flags=re.M)
                body = re.sub(r'^  m_DrawMode:.*','  m_DrawMode: '+('2' if tiled else '0'),body,flags=re.M)
                body = re.sub(r'^  m_Size:.*',f'  m_Size: {{x: 2, y: {number(size_y)}}}',body,flags=re.M)
            scene.extra.append(body)
    else:
        scene.scalar(str(go),'m_Name',name)
        scene.set_vector(str(go+1),'m_LocalPosition',x=x,y=y)
        sprite(scene,str(go+2),sprite_id)
        scene.scalar(str(go+2),'m_DrawMode',2 if tiled else 0)
        scene.set_vector(str(go+2),'m_Size',x=2,y=size_y)


def refine_prefab():
    path = ROOT/'Assets/Prefabs/Environment/InteriorHallBay.prefab'
    scene = Scene(path)
    # A 16-tile room is the default; higher routes get only the extra headroom they need.
    scene.set_vector('1011','m_LocalPosition',y=4.07)
    scene.set_vector('1012','m_Size',x=10,y=23)
    scene.set_vector('1051','m_LocalPosition',y=5.92)
    scene.set_vector('1052','m_Size',x=1,y=12)
    scene.set_vector('1121','m_LocalPosition',y=11.1)
    scene.set_vector('1071','m_LocalPosition',y=12.21)
    scene.scalar('1040','m_Name','Left Window Arch')
    scene.set_vector('1041','m_LocalPosition',x=-1.85,y=6.29)
    sprite(scene,'1042',188700210)
    scene.set_vector('1042','m_Size',x=2,y=1)
    for transform,x in [('1021',-1.85),('1031',1.85)]:
        scene.set_vector(transform,'m_LocalPosition',x=x,y=4.44)
        scene.set_vector(transform,'m_LocalScale',x=.92,y=3.7)
    clone_window(scene,1140,'Right Window Arch',1.85,6.29,188700210)
    clone_window(scene,1150,'Left Window Jamb',-1.85,4.44,188700211,True,4)
    clone_window(scene,1160,'Right Window Jamb',1.85,4.44,188700211,True,4)
    clone_window(scene,1170,'Left Window Sill',-1.85,2.59,188700212)
    clone_window(scene,1180,'Right Window Sill',1.85,2.59,188700212)
    scene.scalar('1080','m_Name','Wall Banner')
    scene.set_vector('1081','m_LocalPosition',y=4.44)
    decor_guid = re.search(r'^guid: (\w+)',(ROOT/'Assets/Art/LevelTiles/InteriorDecor.png.meta').read_text(),re.M)[1]
    scene.put('1082',re.sub(r'^  m_Sprite:.*',f'  m_Sprite: {{fileID: 188710003, guid: {decor_guid}, type: 3}}',scene.body('1082'),flags=re.M))
    children = re.findall(r'^  - \{fileID: (\d+)\}',scene.body('1001'),re.M)
    scene.children('1001',list(dict.fromkeys(children+[str(i+1) for i in WINDOW_PARTS if i != 1040])))
    # Prefab assets have no SceneRoots document.
    path.write_text(''.join(scene.parts)+''.join(scene.extra))


def set_override(body, target, prop, value):
    pattern = (r'(    - target: \{fileID: '+str(target)+r', guid: '+HALL+
               r', type: 3\}\n      propertyPath: '+re.escape(prop)+r'\n      value: )([^\n]*)')
    if re.search(pattern,body):
        return re.sub(pattern,lambda m:m[1]+str(value),body)
    addition = f'''    - target: {{fileID: {target}, guid: {HALL}, type: 3}}
      propertyPath: {prop}
      value: {value}
      objectReference: {{fileID: 0}}
'''
    return body.replace('    m_RemovedComponents:',addition+'    m_RemovedComponents:',1)


def terrain_palette(scene, tilemap):
    assets = Scene(ROOT/'Assets/Art/LevelTiles/PlatformerTiles.asset')
    by_id = {i:re.search(r'^  m_Name: (.*)',assets.body(i),re.M)[1] for i,(k,_) in assets.docs.items() if k == '114'}
    by_name = {name:i for i,name in by_id.items()}
    tile_sprites = {i:re.search(r'^  m_Sprite:.*fileID: (-?\d+)',assets.body(i),re.M)[1] for i in by_id}
    body = scene.body(tilemap)
    old_array = re.search(r'^  m_TileAssetArray:\n([\s\S]*?)(?=^  \w)',body,re.M)[1]
    current_ids = re.findall(r'm_Data: \{fileID: (-?\d+),',old_array)
    palette = []
    for i in current_ids[:11]:
        name = by_id[i]
        # The original asset's main Tile is the first green floor variant.
        if name == 'PlatformerTiles': name = 'Floor0_0'
        for a,b in [('Floor0_','Floor1_'),('Stone0_','Stone1_'),('Platform0_','Platform1_')]:
            name = name.replace(a,b)
        palette.append(by_name[name])
    if len(palette) == 8:
        palette.extend(by_name[f'Platform1_{i}'] for i in range(3))
    assert len(palette) == 11
    palette.extend(str(188720000+i) for i in range(16))
    for array,guid,ids in [('m_TileAssetArray',TILES,palette),('m_TileSpriteArray',ATLAS,[tile_sprites[i] for i in palette])]:
        entries = ''.join(f'  - m_RefCount: 0\n    m_Data: {{fileID: {i}, guid: {guid}, type: {2 if array == "m_TileAssetArray" else 3}}}\n' for i in ids)
        body = re.sub(r'^  '+array+r':\n[\s\S]*?(?=^  \w)','  '+array+':\n'+entries,body,flags=re.M)
    scene.put(tilemap,body)
    entries = tile_entries(scene,tilemap)
    for (x,y),data in entries.items():
        if y < -1:
            index = 11 + ((-y-2)%4)*4+x%4
            data = re.sub(r'(m_Tile(?:Sprite)?Index: )\d+',lambda m:m[1]+str(index),data)
            entries[x,y] = data
    serialize_tiles(scene,tilemap,entries,int(scene.vector(tilemap,'m_Size')['x']))
    renderer = next(i for i,(kind,_) in scene.docs.items() if kind == '483693784')
    body = scene.body(renderer)
    body = re.sub(r'(  m_Materials:\n  - \{fileID: 2100000, guid: )\w+',lambda m:m[1]+MATERIAL,body)
    scene.put(renderer,body)


def platform_stonework(scene):
    # Rebuild only this tool's decoration, preserving every gameplay collider.
    old_ids = [i for i in scene.docs if 6400000000 <= int(i) < 6400010000]
    for i in old_ids: scene.put(i,'')
    roots = '9223372036854775807'
    scene.put(roots,scene.body(roots).replace('  - {fileID: 6400000001}\n',''))
    template = next(scene.body(i) for i,(k,_) in scene.docs.items() if k == '212' and 'm_Sprite: {fileID:' in scene.body(i))
    nodes = []
    for go,tr in scene.transforms.items():
        if scene.names[go] != 'One-way ledge': continue
        p = scene.vector(tr,'m_LocalPosition')
        size = scene.vector(scene.component(go,'61'),'m_Size')
        if size['x'] < CELL*6-.001: continue
        top = p['y']+size['y']*.5
        for direction,identifier in [(-1,188700200),(1,188700201)]:
            x = p['x']+direction*(size['x']*.5-CELL*1.5)
            nodes.append((x,top-CELL,identifier))
    if not nodes: return
    def go_doc(go,name,components):
        return f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
'''+''.join(f'  - component: {{fileID: {i}}}\n' for i in components)+f'''  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
'''
    def transform_doc(go,parent,x,y,scale,children=()):
        return f'''--- !u!4 &{go+1}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {number(x)}, y: {number(y)}, z: 0}}
  m_LocalScale: {{x: {number(scale)}, y: {number(scale)}, z: 1}}
  m_ConstrainProportionsScale: 0
'''+('  m_Children:\n'+''.join(f'  - {{fileID: {i}}}\n' for i in children) if children else '  m_Children: []\n')+f'''  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''
    scene.extra.extend([go_doc(6400000000,'Platform Stonework',[6400000001]),
                        transform_doc(6400000000,0,0,0,1,[6400000011+i*10 for i in range(len(nodes))])])
    scene.put(roots,scene.body(roots)+'  - {fileID: 6400000001}\n')
    for i,(x,y,sprite_id) in enumerate(nodes):
        go = 6400000010+i*10
        scene.extra.extend([go_doc(go,'Stone Corbel',[go+1,go+2]),transform_doc(go,6400000001,x,y,CELL)])
        body = re.sub(r'^--- !u!212 &\d+',f'--- !u!212 &{go+2}',template,flags=re.M)
        for field,value in [('m_GameObject',f'{{fileID: {go}}}'),('m_Sprite',f'{{fileID: {sprite_id}, guid: {ATLAS}, type: 3}}'),
                            ('m_Color','{r: 1, g: 1, b: 1, a: 1}'),('m_SortingOrder','-11'),('m_DrawMode','0'),('m_Size','{x: 3, y: 2}')]:
            body = re.sub(r'^  '+field+r':.*','  '+field+': '+value,body,flags=re.M)
        body = re.sub(r'(  m_Materials:\n  - \{fileID: 2100000, guid: )\w+',lambda m:m[1]+MATERIAL,body)
        scene.extra.append(body)


def refine_scene(name):
    designed = name not in ['Lobby','ShopTest']
    path = ROOT/'Assets/Scenes/Gameplay'/('Designed' if designed else '')/(name+'.unity')
    scene = Scene(path)
    terrain = scene.transforms[scene.named('Platformer Terrain' if designed else 'Platformer Floor')]
    tilemap = next(i for i,(k,_) in scene.docs.items() if k == '1839735485')
    left = scene.vector(terrain,'m_LocalPosition')['x']
    width = scene.vector(tilemap,'m_Size')['x']*scene.vector(terrain,'m_LocalScale')['x']
    rebuild_hall(scene,left,width)
    floor = scene.vector(terrain,'m_LocalPosition')['y']
    highest = max([scene.vector(tr,'m_LocalPosition')['y'] +
                   scene.vector(scene.component(go,'61'),'m_Size')['y']*.5-floor
                   for go,tr in scene.transforms.items()
                   if scene.names[go] == 'One-way ledge'] or [0])
    # 2-unit body + 4-tile jump + a small margin above the highest landing.
    ceiling = max(11.84, math.ceil((highest+2+2.961+.2)/CELL)*CELL) if designed else 7.4
    bottom,top = (2.22, 8.14 if highest > 8 else 6.66) if designed else (2.22,5.92)
    center = (bottom+top)*.5
    for i,body in enumerate(scene.extra):
        if not body.startswith('--- !u!1001'): continue
        column = int(re.search(r'Interior Bay 0-(\d+)',body)[1])
        has_windows = column%3 != 2
        for go in [1020,1030]+WINDOW_PARTS: body = set_override(body,go,'m_IsActive',1 if has_windows else 0)
        body = set_override(body,1080,'m_IsActive',0 if has_windows else 1)
        for target,prop,value in [(1011,'m_LocalPosition.y',(ceiling+CELL-4.44)*.5),
                                  (1012,'m_Size.y',(ceiling+CELL+4.44)/CELL),
                                  (1051,'m_LocalPosition.y',ceiling*.5),
                                  (1052,'m_Size.y',(ceiling-2.96)/CELL),
                                  (1121,'m_LocalPosition.y',ceiling-.74),
                                  (1071,'m_LocalPosition.y',ceiling+.37),
                                  (1081,'m_LocalPosition.y',center),
                                  (1091,'m_LocalPosition.x',-1.1 if column%2 else 1.1)]:
            body = set_override(body,target,prop,number(value))
        for transform in [1021,1031,1151,1161]: body = set_override(body,transform,'m_LocalPosition.y',number(center))
        for transform in [1021,1031]: body = set_override(body,transform,'m_LocalScale.y',number(top-bottom-CELL))
        for renderer in [1022,1032]:
            for channel,tint in zip('rgb',GLASS_TINTS[name]):
                body = set_override(body,renderer,'m_Color.'+channel,number(tint*.7))
        for renderer in [1152,1162]: body = set_override(body,renderer,'m_Size.y',number((top-bottom)/CELL-2))
        for transform in [1041,1141]: body = set_override(body,transform,'m_LocalPosition.y',number(top-CELL*.5))
        for transform in [1171,1181]: body = set_override(body,transform,'m_LocalPosition.y',number(bottom+CELL*.5))
        for renderer in [1142,1152,1162,1172,1182]:
            for channel in 'rgb':
                value = re.search(r'target: \{fileID: 1042,[\s\S]*?propertyPath: m_Color\.'+channel+r'\n      value: ([^\n]+)',body)[1]
                body = set_override(body,renderer,'m_Color.'+channel,value)
        scene.extra[i] = body
    for identifier,(kind,_) in scene.docs.items():
        if kind == '20':
            scene.set_vector(identifier,'m_BackGroundColor',r=.04,g=.045,b=.06,a=1)
        elif designed and kind == '114' and '::LobbyCameraFollow' in scene.body(identifier):
            camera = next(i for i,(k,_) in scene.docs.items() if k == '20')
            size = float(re.search(r'orthographic size: ([^\n]+)',scene.body(camera))[1])
            scene.scalar(identifier,'MaxY',max(0,floor+ceiling+CELL-size))
    terrain_palette(scene,tilemap)
    if designed: platform_stonework(scene)
    path.write_text(scene.text())
    print(name,'ceiling',number(ceiling),'window height',number(top-bottom))


if __name__ == '__main__':
    add_sprites_and_tiles()
    refine_prefab()
    for name in COUNTS: refine_scene(name)
