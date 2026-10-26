"""Build a reviewable Blender assembly from saved Unity prefab data. No Unity assets are edited."""
import bpy
import json
import math
import re
from pathlib import Path
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
SOURCE = ROOT / 'Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab'
CONFIG = json.loads((OUT / 'preview_settings.json').read_text())

def field(text, name, default=''):
    match = re.search(r'^  ' + re.escape(name) + r': (.*)$', text, re.M)
    return match.group(1).strip() if match else default

def ref(text, name):
    return int(re.search(r'fileID: (-?\d+)', field(text, name, '{fileID: 0}')).group(1))

def vec(text, name, keys='xyz'):
    value = field(text, name)
    return [float(re.search(key + r': ([^,}]+)', value)[1]) for key in keys]

def unity_transforms():
    blocks = {int(m[2]): (int(m[1]), m[3]) for m in re.finditer(
        r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', SOURCE.read_text(), re.M | re.S)}
    names = {key: field(txt, 'm_Name') for key, (kind, txt) in blocks.items() if kind == 1}
    transforms = {key: txt for key, (kind, txt) in blocks.items() if kind == 4 and field(txt, 'm_GameObject')}
    name_to_id = {names[ref(txt, 'm_GameObject')]: key for key, txt in transforms.items()}
    cache = {}
    def world(key):
        if key in cache:
            return cache[key]
        txt = transforms[key]
        parent = ref(txt, 'm_Father')
        pos = vec(txt, 'm_LocalPosition') if parent else [0, 0, 0]
        rot = Quaternion(vec(txt, 'm_LocalRotation', 'wxyz'))
        local = Matrix.LocRotScale(Vector(pos), rot, Vector(vec(txt, 'm_LocalScale')))
        cache[key] = world(parent) @ local if parent else local
        return cache[key]
    return {name: world(key) for name, key in name_to_id.items()}

def material_flat(name, color, roughness=0.65, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color[:3], 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color[:3], 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat

def hero_material():
    """Rebuild the saved material's color-mask composition for pose review."""
    mat_path = ROOT / 'Assets/Synty/PolygonFantasyHeroCharacters/Materials/FantasyHero.mat'
    text = mat_path.read_text()
    colors = {m[1]: [float(m[i]) for i in range(2,5)] for m in re.finditer(
        r'- (_Color_\w+): \{r: ([^,]+), g: ([^,]+), b: ([^,]+)', text)}
    textures = {m[1]: m[2] for m in re.finditer(r'- (_\w+):\s+m_Texture: \{fileID: \d+, guid: ([a-f0-9]+)', text)}
    mask_root = ROOT / 'Assets/Synty/PolygonFantasyHeroCharacters/Textures'
    guid_paths = {}
    for meta in mask_root.rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
        if match:
            guid_paths[match[1]] = meta.with_suffix('')
    mat = material_flat('FantasyHero_review_color_masks', colors['_Color_Primary'])
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    # Match the channel selection in POLYGON_CustomCharacters.shader. The masks
    # are white outside their region, so adding their RGB values washes out the model.
    channels = {}
    def texture_node(tex_name, mask=False):
        path = guid_paths[textures[tex_name]]
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = 'Non-Color' if mask else 'sRGB'
        node = nodes.new('ShaderNodeTexImage'); node.image = image
        node.interpolation = 'Closest'
        return node
    for name in ['_Mask_01','_Mask_02','_Mask_03','_Mask_04','_Mask_05']:
        separate = nodes.new('ShaderNodeSeparateColor'); separate.mode = 'RGB'
        links.new(texture_node(name,True).outputs['Color'], separate.inputs['Color'])
        channels[name] = separate.outputs
    acc = texture_node('_Texture').outputs['Color']
    layers = [('_Mask_01',0,'Primary'),('_Mask_01',1,'Secondary'),
              ('_Mask_04',0,'Leather_Primary'),('_Mask_04',1,'Leather_Secondary'),
              ('_Mask_02',0,'Metal_Primary'),('_Mask_02',1,'Metal_Secondary'),
              ('_Mask_02',2,'Metal_Dark'),('_Mask_04',2,'Hair'),
              ('_Mask_03',0,'Skin'),('_Mask_03',2,'Stubble'),('_Mask_03',1,'Scar')]
    for mask, channel, color in layers:
        select = nodes.new('ShaderNodeMath'); select.operation = 'LESS_THAN'
        select.inputs[1].default_value = 0.5  # Threshold defined by the source shader.
        links.new(channels[mask][channel],select.inputs[0])
        mix = nodes.new('ShaderNodeMixRGB')
        links.new(select.outputs[0],mix.inputs[0]); links.new(acc,mix.inputs[1])
        mix.inputs[2].default_value = (*colors['_Color_'+color],1)
        acc = mix.outputs[0]
    eyes = nodes.new('ShaderNodeMixRGB')
    links.new(channels['_Mask_05'][0],eyes.inputs[0]); links.new(acc,eyes.inputs[2])
    eyes.inputs[1].default_value = (*colors['_Color_Eyes'],1)
    links.new(eyes.outputs[0], nodes.get('Principled BSDF').inputs['Base Color'])
    return mat

def texture_material(name, path):
    mat = material_flat(name, (0.35,0.3,0.22))
    node = mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = bpy.data.images.load(str(path), check_existing=True)
    mat.node_tree.links.new(node.outputs['Color'], mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    return mat

def set_mesh_world_baked(obj, matrix):
    obj.data.transform(matrix)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)

def point_camera(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.wm.open_mainfile(filepath=str(OUT / 'spearman_source.blend'))
rig = bpy.data.objects['Armature']
body = [obj for obj in bpy.data.objects if obj.type == 'MESH']
body_world = {obj.name: obj.matrix_world.copy() for obj in body}
unit_scale = CONFIG['prefab_scale']
world_axis = rig.matrix_world.copy()
rig.data.transform(Matrix.Scale(CONFIG['fbx_bone_unit_scale'] * unit_scale, 4) @ world_axis)
rig.matrix_world = Matrix.Identity(4)
for bone in rig.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
for obj in body:
    # Cached source transforms are saved before changing the armature parent's matrix.
    obj_matrix = body_world[obj.name]
    set_mesh_world_baked(obj, Matrix.Scale(unit_scale,4) @ obj_matrix)
    obj.parent = rig
bpy.context.view_layer.update()

unity = unity_transforms()
# Determine the handedness conversion from the actual reference skeleton.
comparisons = []
for sx in [-1,1]:
    for sy in [-1,1]:
        c = Matrix(((sx,0,0,0),(0,0,sy,0),(0,1,0,0),(0,0,0,1)))
        error = sum(((c @ unity[name]).translation - rig.pose.bones[name].head).length for name in ['Hips','Head','Hand_L','Hand_R'])
        comparisons.append((error,c))
error, conversion = min(comparisons,key=lambda pair:pair[0])
print('SKELETON_ALIGNMENT_ERROR', error, flush=True)
if error > CONFIG['max_skeleton_alignment_error']:
    raise RuntimeError('Source skeleton conversion needs inspection before posing')
hero_mat = hero_material()
for obj in body:
    obj.data.materials.clear(); obj.data.materials.append(hero_mat)

equipment = []
equipment_offsets = {}
equipment_specs = [
    ('spear','SM_Wep_Spear_06','Hand_R','Assets/Synty/PolygonFantasyKingdom/Models/SM_Wep_Spear_06.fbx'),
    ('shield','SM_Wep_Shield_Long_14','Hand_L','Assets/Synty/PolygonFantasyHeroCharacters/Models/Weapons/SM_Wep_Shield_Long_14.fbx')]
for kind, name, hand, file in equipment_specs:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / file), use_anim=False)
    obj = next(o for o in set(bpy.data.objects)-before if o.type=='MESH')
    imported = obj.matrix_world.copy()
    # FBX geometry is normalized to metres first. The prefab's ancestor scale is then applied.
    obj.data.transform(imported)
    if kind == 'spear':
        unity_equip = unity[name]
    else:
        # Shield is a nested prefab; read its explicit transform overrides.
        prefab_text = SOURCE.read_text()
        segment = prefab_text[prefab_text.index('--- !u!1001 &6272614488000239299'):]
        def prop(key):
            return float(re.search(r'propertyPath: '+re.escape(key)+r'\s+value: ([^\n]+)',segment)[1])
        loc = Vector([prop('m_LocalPosition.'+axis) for axis in 'xyz'])
        rot = Quaternion([prop('m_LocalRotation.'+axis) for axis in 'wxyz'])
        scale = Vector([prop('m_LocalScale.'+axis) for axis in 'xyz'])
        unity_equip = unity['Off_Hand_Weapon_Carrier'] @ Matrix.LocRotScale(loc,rot,scale)
    desired = conversion @ unity_equip @ conversion.inverted()
    # Unity FBX mesh data and imported Blender world mesh use the same up axis after conversion.
    obj.parent = None; obj.matrix_world = desired
    equipment_offsets[kind] = rig.pose.bones[hand].matrix.inverted() @ desired
    obj.name = kind.capitalize()
    equipment.append((kind,obj,hand))
    obj.data.materials.clear()
    obj.data.materials.append(hero_mat if kind=='shield' else texture_material('Spear_palette', ROOT / 'Assets/Synty/PolygonFantasyKingdom/Textures/PolygonFantasyKingdom_Texture_01_A.png'))

# Persist the complete rest assembly before the review poses are created.
bpy.context.view_layer.update()
report = {'alignment_error_m': error, 'conversion': [list(row) for row in conversion],
          'equipment': {kind:{'matrix':[list(row) for row in obj.matrix_world], 'dimensions':list(obj.dimensions),
                             'offset':[list(row) for row in equipment_offsets[kind]]} for kind,obj,hand in equipment},
          'bones': {b.name:list(b.head) for b in rig.pose.bones},
          'body_bounds': {o.name:[list(o.matrix_world @ Vector(v)) for v in o.bound_box] for o in body}}
(OUT / 'assembly_report.json').write_text(json.dumps(report,indent=2))
for kind,obj,hand in equipment:
    world = obj.matrix_world.copy(); obj.parent=rig; obj.parent_type='BONE'; obj.parent_bone=hand
    bpy.context.view_layer.update(); obj.matrix_world=world

rig.name='Spearman_Reference'
rig.show_in_front=True
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'spearman_assembled.blend'))
print('ASSEMBLY_COMPLETE',flush=True)
