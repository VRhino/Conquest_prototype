"""Inspect the saved Unity spearman assembly and its source FBX in Blender."""
import bpy
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
PREFAB = ROOT / 'Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab'

def field(text, name, default=''):
    match = re.search(r'^  ' + re.escape(name) + r': (.*)$', text, re.M)
    return match.group(1).strip() if match else default

def ref(text, name):
    return int(re.search(r'fileID: (-?\d+)', field(text, name, '{fileID: 0}')).group(1))

def source_manifest():
    blocks = {}
    for match in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', PREFAB.read_text(), re.M | re.S):
        blocks[int(match[2])] = (int(match[1]), match[3])
    gos = {key: {'name': field(txt, 'm_Name'), 'active': field(txt, 'm_IsActive') == '1'}
           for key, (kind, txt) in blocks.items() if kind == 1}
    transforms = {key: {'go': ref(txt, 'm_GameObject'), 'parent': ref(txt, 'm_Father'), 'text': txt}
                  for key, (kind, txt) in blocks.items() if kind == 4 and field(txt, 'm_GameObject')}
    go_transform = {t['go']: key for key, t in transforms.items()}
    def active(go):
        if go not in gos or not gos[go]['active']:
            return False
        parent = transforms[go_transform[go]]['parent']
        return not parent or active(transforms[parent]['go'])
    meshes = []
    for key, (kind, txt) in blocks.items():
        if kind != 137:
            continue
        go = ref(txt, 'm_GameObject')
        if active(go) and field(txt, 'm_Enabled') == '1':
            meshes.append({'name': gos[go]['name'], 'mesh': field(txt, 'm_Mesh'),
                           'materials': re.findall(r'guid: ([a-f0-9]{32})', txt.split('m_Materials:')[1].split('m_StaticBatchInfo:')[0])})
    return {'visible_meshes': meshes, 'transform_count': len(transforms)}

manifest = source_manifest()
print('VISIBLE_MESHES', json.dumps(manifest))
bpy.ops.wm.read_factory_settings(use_empty=True)
fbx = ROOT / 'Assets/Synty/PolygonFantasyHeroCharacters/Models/ModularCharacters.fbx'
bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
names = {item['name'] for item in manifest['visible_meshes']}
for obj in list(bpy.data.objects):
    if obj.type == 'MESH' and obj.name not in names:
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.view_layer.update()
manifest['objects'] = [{'name': obj.name, 'type': obj.type, 'parent': obj.parent.name if obj.parent else None,
                        'location': list(obj.location), 'scale': list(obj.scale),
                        'dimensions': list(obj.dimensions)} for obj in bpy.data.objects if obj.type in {'MESH','ARMATURE'}]
rigs = [obj for obj in bpy.data.objects if obj.type == 'ARMATURE']
manifest['rigs'] = [{'name': rig.name, 'matrix': [list(row) for row in rig.matrix_world],
                     'bones': [{'name': b.name, 'head': list(b.head_local), 'tail': list(b.tail_local),
                                'parent': b.parent.name if b.parent else None} for b in rig.data.bones]} for rig in rigs]
(OUT / 'source_inspection.json').write_text(json.dumps(manifest, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'spearman_source.blend'))
print('INSPECTION_COMPLETE', OUT / 'source_inspection.json')
