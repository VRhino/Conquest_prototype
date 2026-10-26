"""Pose the assembled source and build a six-soldier review scene."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector, Euler

OUT = Path(__file__).resolve().parent
CFG = json.loads((OUT / 'preview_settings.json').read_text())
REPORT = json.loads((OUT / 'assembly_report.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'spearman_assembled.blend'))
rig = bpy.data.objects['Spearman_Reference']
body = [o for o in bpy.data.objects if o.type=='MESH' and o.name not in {'Spear','Shield'}]
gear = {'spear':bpy.data.objects['Spear'], 'shield':bpy.data.objects['Shield']}

def update():
    bpy.context.view_layer.update()

def aim_bone(rig, name, child, target):
    bone = rig.pose.bones[name]
    origin = bone.head.copy()
    current = rig.pose.bones[child].head - origin
    wanted = Vector(target) - origin
    delta = current.rotation_difference(wanted).to_matrix().to_4x4()
    bone.matrix = Matrix.Translation(origin) @ delta @ Matrix.Translation(-origin) @ bone.matrix
    update()

def two_bone(rig, upper, lower, end, target, pole):
    a = rig.pose.bones[upper].head.copy()
    b = rig.pose.bones[lower].head.copy()
    c = rig.pose.bones[end].head.copy()
    l1,l2 = (b-a).length,(c-b).length
    target=Vector(target); direction=target-a
    distance=min(direction.length,(l1+l2)*0.998)
    axis=direction.normalized()
    bend=Vector(pole)-a; bend-=axis*bend.dot(axis); bend.normalize()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    height=math.sqrt(max(0,l1*l1-along*along))
    elbow=a+axis*along+bend*height
    aim_bone(rig,upper,lower,elbow)
    aim_bone(rig,lower,end,a+axis*distance)
    return (rig.pose.bones[end].head-target).length

def pose(rig, row, gear_objects):
    cfg=CFG[row]
    original={b.name:b.matrix.copy() for b in rig.pose.bones}
    hips=rig.pose.bones['Hips']
    hips.matrix=Matrix.Translation((0,cfg['hip_back'],-cfg['hip_drop']))@hips.matrix
    update()
    spine=rig.pose.bones['Spine_01']; origin=spine.head.copy()
    lean=Matrix.Rotation(math.radians(cfg['torso_lean_degrees']),4,'X')
    spine.matrix=Matrix.Translation(origin)@lean@Matrix.Translation(-origin)@spine.matrix
    update()
    for side,word in [('L','left'),('R','right')]:
        two_bone(rig,'UpperLeg_'+side,'LowerLeg_'+side,'Ankle_'+side,cfg[word+'_ankle'],cfg[word+'_knee_pole'])
        foot=rig.pose.bones['Ankle_'+side]
        m=original[foot.name].copy(); m.translation=foot.head.copy(); foot.matrix=m
        update()
    errors={}
    for kind,side,hand in [('shield','L','Hand_L'),('spear','R','Hand_R')]:
        offset=Matrix(REPORT['equipment'][kind]['offset'])
        if kind=='shield':
            # Fit the palm to the modeled handle rather than the prefab wrist origin.
            offset=offset@Matrix.Translation(-Vector(CFG['shield_grip_shift_local']))
        rotation=Euler([math.radians(a) for a in cfg[kind+'_rotation_degrees']]).to_matrix().to_4x4()
        scale=Matrix(REPORT['equipment'][kind]['matrix']).to_scale()
        desired=Matrix.LocRotScale(Vector(cfg[kind+'_location']),rotation.to_quaternion(),scale)
        hand_target=desired@offset.inverted()
        # The spear's model origin is below its grip; target the grip directly.
        if kind=='spear':
            hand_target.translation=Vector(cfg[kind+'_location'])
        target=hand_target.translation
        shoulder=rig.pose.bones['Shoulder_'+side].head
        pole=Vector((0.65 if side=='L' else -0.65,0.0,shoulder.z-0.4))
        if row=='rear':
            pole.z=shoulder.z+0.05
        errors[kind]=two_bone(rig,'Shoulder_'+side,'Elbow_'+side,hand,target,pole)
        h=rig.pose.bones[hand]; m=hand_target.copy(); m.translation=h.head.copy(); h.matrix=m
        update()
        obj=gear_objects[kind]
        obj.matrix_world=rig.matrix_world@h.matrix@offset
        update()
    # Curl in the hand's reference frame, not around imported bone-local axes.
    # Synty's mirrored hands have different bone bases despite matching joint names.
    for side,suffix in [('L',''),('R','.001')]:
        hand_matrix=rig.pose.bones['Hand_'+side].matrix.copy()
        axis=hand_matrix.to_3x3() @ Vector((0,0,1))
        for chain in ['IndexFinger','Finger']:
            for joint,degrees in enumerate(CFG['finger_joint_curl_degrees'],start=1):
                bone=rig.pose.bones[chain+'_'+str(joint).zfill(2)+suffix]
                origin=bone.head.copy()
                rotation=Matrix.Rotation(math.radians(degrees),4,axis)
                bone.matrix=Matrix.Translation(origin)@rotation@Matrix.Translation(-origin)@bone.matrix
                update()
        thumb=rig.pose.bones['Thumb_01'+suffix]
        origin=thumb.head.copy()
        axis=hand_matrix.to_3x3() @ Vector((0,1,0))
        rotation=Matrix.Rotation(math.radians(CFG['thumb_curl_degrees']),4,axis)
        thumb.matrix=Matrix.Translation(origin)@rotation@Matrix.Translation(-origin)@thumb.matrix
    update()
    return errors

def copy_soldier(source_rig, objects, name):
    newrig=source_rig.copy(); newrig.data=source_rig.data.copy(); newrig.name=name
    bpy.context.scene.collection.objects.link(newrig)
    children=[]
    for obj in objects:
        clone=obj.copy(); clone.data=obj.data; clone.name=name+'_'+obj.name
        bpy.context.scene.collection.objects.link(clone)
        clone.parent=newrig
        for mod in clone.modifiers:
            if mod.type=='ARMATURE': mod.object=newrig
        children.append(clone)
    return newrig,children

all_objects=body+list(gear.values())
rear,rear_objects=copy_soldier(rig,all_objects,'Line_Rear_02')
rear_gear={kind:next(o for o in rear_objects if o.name.endswith(obj.name)) for kind,obj in gear.items()}
errors={'front':pose(rig,'front',gear),'rear':pose(rear,'rear',rear_gear)}
rig.name='Line_Front_02'
rear.location.y=CFG['row_spacing']
update()
soldiers=[rig,rear]
for source,objects,row in [(rig,all_objects,'Front'),(rear,rear_objects,'Rear')]:
    for col,x in [(1,-CFG['column_spacing']),(3,CFG['column_spacing'])]:
        clone,children=copy_soldier(source,objects,'Line_'+row+'_'+str(col).zfill(2))
        clone.location.x=x
        soldiers.append(clone)

# Pose actions are static review assets. Animation transitions come after visual approval.
for actor in soldiers:
    actor.animation_data_clear()
    for bone in actor.pose.bones:
        bone.keyframe_insert(data_path='location',frame=1)
        bone.keyframe_insert(data_path='rotation_quaternion' if bone.rotation_mode=='QUATERNION' else 'rotation_euler',frame=1)
    actor.animation_data.action.name=actor.name+'_PoseReview'

def mat(name,color):
    material=bpy.data.materials.new(name); material.diffuse_color=(*color,1)
    material.use_nodes=True
    material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=0.8
    return material

bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-0.015))
ground=bpy.context.object; ground.name='Review_Ground'; ground.data.materials.append(mat('Ground',(0.12,0.14,0.17)))
def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,position,power,size in CFG['lights']:
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); bpy.context.scene.collection.objects.link(obj); obj.location=position; aim(obj,(0,0,0.8))
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=CFG['render_samples']
scene.cycles.use_denoising=True
scene.render.resolution_x,scene.render.resolution_y=CFG['render_resolution']; scene.render.resolution_percentage=100
if scene.world is None:
    scene.world=bpy.data.worlds.new('Review_World')
scene.world.color=(0.2,0.2,0.2)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
cameras={}
for name,values in CFG['cameras'].items():
    data=bpy.data.cameras.new(name); data.type='ORTHO'; data.ortho_scale=values['scale']
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj); obj.location=values['position']; aim(obj,values['target']); cameras[name]=obj
scene.camera=cameras['Perspective']
for obj in bpy.context.selected_objects: obj.select_set(False)
rig.select_set(True); bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=CFG['viewport_distance']
            area.spaces.active.region_3d.view_location=Vector((0,0,0.9))
            area.spaces.active.region_3d.view_rotation=scene.camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
# Keep the file self-contained; the original Unity materials remain the source of truth.
for image in list(bpy.data.images):
    if image.users and image.source=='FILE' and Path(bpy.path.abspath(image.filepath)).exists():
        image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spearmen_line_pose_review.blend'))
update()
depsgraph=bpy.context.evaluated_depsgraph_get()
shields=[o for o in scene.objects if o.type=='MESH' and 'Shield' in o.name]
spears=[o for o in scene.objects if o.type=='MESH' and 'Spear' in o.name]
crossings=[]
for spear in spears:
    points=[v.co.z for v in spear.data.vertices]
    start=spear.matrix_world@Vector((0,0,min(points)))
    finish=spear.matrix_world@Vector((0,0,max(points)))
    for shield in shields:
        inverse=shield.matrix_world.inverted()
        local_start=inverse@start; local_end=inverse@finish
        direction=local_end-local_start
        hit,_,_,_=shield.evaluated_get(depsgraph).ray_cast(local_start,direction.normalized(),distance=direction.length)
        if hit: crossings.append([spear.name,shield.name])
report={'hand_target_error_m':errors,'spear_axis_shield_intersections':crossings,
        'soldiers':len(soldiers),'column_spacing':CFG['column_spacing'],'row_spacing':CFG['row_spacing'],
        'scope':'Static Line pose study; axis intersections only, not full mesh collision validation.'}
(OUT/'pose_review_report.json').write_text(json.dumps(report,indent=2))
for name in CFG['render_views']:
    scene.camera=cameras[name]; scene.render.filepath=str(OUT/('preview_'+name.lower()+'.png'))
    bpy.ops.render.render(write_still=True)
print('POSE_PREVIEW_COMPLETE',flush=True)

# Export rotation deltas against the imported bind pose. Unity applies them to
# its own prefab skeleton, then converts that pose through the existing Avatar.
conversion=Matrix(REPORT['conversion'])
def export_delta(matrix):
    return [value for row in (conversion.inverted()@matrix@conversion) for value in row]
unity_poses=[]
for actor,objects,label in [(rig,all_objects,'Front'),(rear,rear_objects,'Rear')]:
    bones=[]
    for bone in actor.pose.bones:
        bones.append({'name':bone.name.replace('.001',' 1'),
                      'delta':export_delta(bone.matrix@bone.bone.matrix_local.inverted())})
    equipment=[]
    for kind in ['spear','shield']:
        obj=next(o for o in objects if o.name.endswith(kind.capitalize()))
        matrix=actor.matrix_world.inverted()@obj.matrix_world
        equipment.append({'kind':kind,'delta':export_delta(matrix@Matrix(REPORT['equipment'][kind]['matrix']).inverted())})
    unity_poses.append({'name':label,'bones':bones,'equipment':equipment})
(OUT/'unity_pose_source.json').write_text(json.dumps({'poses':unity_poses},indent=2))
