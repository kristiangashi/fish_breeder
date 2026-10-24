# Splits a multi-fish Meshy kit into one FBX per fish: centre, plump (Y = thickness), decimate.
# Run with Blender's Python (Blender 4+/5, or `pip install bpy`):
#   blender -b -P split_fish_kit.py -- <meshy_kit.fbx> <output folder>
# Edit `plan` for a new kit: each fish is found by the centre of its loose part in the kit's
# X/Z layout (metres), then named, thickened and decimated as listed.
# Plumping scales thickness (Y) by up to the factor given, weighted by how thick the fish
# already is around each point, so the body fills out while fins and the tail stay thin.
import bpy, sys, bmesh, json
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
src, outdir = args[0], args[1]
# island order (by centre) -> (species id, thickness multiplier, target triangles)
plan = {
    (-0.64, 0.45): ("RedTailShark", 1.6, 2400),
    (0.00, 0.45): ("SiameseAlgaeEater", 1.7, 2000),
    (0.64, 0.45): ("SilverDollar", 1.3, 2200),
    (-0.64, 0.00): ("BlackMolly", 2.3, 2400),
    (0.00, 0.00): ("Platy", 2.3, 2200),
    (0.64, 0.00): ("Oscar", 2.0, 2600),
    (-0.64, -0.47): ("GreenTerror", 1.8, 2600),
    (0.00, -0.47): ("BlueAcara", 1.8, 2600),
    (0.64, -0.47): ("GoldenMbuna", 1.8, 2400),
}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0001)
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
report = {}
for part in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
    mw = part.matrix_world
    ws = [mw @ v.co for v in part.data.vertices]
    cx = (min(p.x for p in ws) + max(p.x for p in ws)) / 2
    cz = (min(p.z for p in ws) + max(p.z for p in ws)) / 2
    key = min(plan, key=lambda k: (k[0] - cx) ** 2 + (k[1] - cz) ** 2)
    sid, plump, tris = plan[key]
    # Work in world space: bake the import rotation into the mesh, then put it back so the
    # export uses the same axes as the original set.
    centre = Vector(((min(p.x for p in ws) + max(p.x for p in ws)) / 2,
                     (min(p.y for p in ws) + max(p.y for p in ws)) / 2, cz))
    rot = part.matrix_world.copy()
    pts = [rot @ v.co - centre for v in part.data.vertices]
    # Plump the body but not the fins: how thick the fish is around each point (in side view)
    # decides how much that point is pushed out, so thin fins and the tail stay thin.
    cell = 0.012
    thick = {}
    for p in pts:
        k = (round(p.x / cell), round(p.z / cell))
        thick[k] = max(thick.get(k, 0.0), abs(p.y))
    tmax = max(thick.values())
    def local(p):
        kx, kz = round(p.x / cell), round(p.z / cell)
        return max(thick.get((kx + i, kz + j), 0.0) for i in (-1, 0, 1) for j in (-1, 0, 1))
    for v, p in zip(part.data.vertices, pts):
        t = (local(p) / tmax - 0.25) / 0.45
        t = max(0.0, min(1.0, t)); t = t * t * (3 - 2 * t)
        p.y *= 1 + (plump - 1) * t
        v.co = rot.inverted() @ p
    part.location = (0, 0, 0)
    part.name = sid
    part.data.name = sid
    if part.data.materials:
        part.data.materials[0].name = sid
    bpy.ops.object.select_all(action='DESELECT')
    part.select_set(True)
    bpy.context.view_layer.objects.active = part
    before = sum(len(p.vertices) - 2 for p in part.data.polygons)
    mod = part.modifiers.new("Decimate", 'DECIMATE')
    mod.ratio = min(1.0, tris / before)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.shade_smooth()
    after = sum(len(p.vertices) - 2 for p in part.data.polygons)
    bpy.ops.export_scene.fbx(filepath=f"{outdir}/{sid}.fbx", use_selection=True, mesh_smooth_type='FACE',
                             path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
    ws = [part.matrix_world @ v.co for v in part.data.vertices]
    dims = [round(max(p[i] for p in ws) - min(p[i] for p in ws), 3) for i in range(3)]
    report[sid] = {"tris_before": before, "tris_after": after, "dims_xyz": dims}
print(json.dumps(report, indent=1))
