"""Step 3 of 3: exports each rigged character from rigged.blend to out/<Name>.fbx, ready to copy
into Assets/Resources/Crew. Usage: python3 crew_export.py"""
import bpy
import os
# Intermediate files go in CREW_WORK (default: the current folder).
WORK=os.environ.get('CREW_WORK', os.getcwd())
os.makedirs(os.path.join(WORK, 'out'), exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(WORK, 'rigged.blend'))
for a in [o for o in bpy.data.objects if o.type=='ARMATURE']:
    mesh=[o for o in bpy.data.objects if o.type=='MESH' and o.parent==a][0]
    name=mesh.name
    mesh.data.materials.clear()
    mat=bpy.data.materials.get('Crew') or bpy.data.materials.new('Crew'); mesh.data.materials.append(mat)
    a.name=name+'_Rig'
    bpy.ops.object.select_all(action='DESELECT'); a.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=a
    bpy.ops.export_scene.fbx(filepath=os.path.join(WORK, 'out', f'{name}.fbx'), use_selection=True, object_types={'ARMATURE','MESH'},
        add_leaf_bones=False, bake_anim=False, use_armature_deform_only=True, apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
