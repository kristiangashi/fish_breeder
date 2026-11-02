# Splits a Blender file of hang-on-back aquarium filters (three of them side by side along X,
# cheapest on the left, each one piece: the intake tube that hangs inside the tank, the hook over
# the glass, and the box that sits outside) into one decimated FBX per filter with a low-poly
# _Collider hull, and writes the Unity texture set.
# Run with Blender's Python (Blender 4+/5, or `pip install bpy`):
#   python3 split_hangon_filters.py -- <filters.blend> <output folder>
# Each filter's origin is put where it hangs: on the glass, at the top edge, in the middle of the
# intake tube, with +Y towards the outside of the tank, so the game only has to drop it on the rim.
import bpy, bmesh, sys, os, json
from mathutils import Vector
from PIL import Image

args = sys.argv[sys.argv.index("--") + 1:]
src, outdir = args[0], args[1]
os.makedirs(outdir, exist_ok=True)

# Where one filter ends and the next begins along X, and what to cut each one down to.
PLAN = [
    (-9.0, -0.25, "Filter0", 1800),
    (-0.25, 0.095, "Filter1", 2000),
    (0.095, 9.0, "Filter2", 2400),
]
COLLIDER_TRIS = 60
# The intake tube is everything below this height; the glass hangs just behind the tube.
TUBE_TOP_Z, GLASS_BEHIND_TUBE = 0.15, 0.03
# How far below the top of the hook the glass rim sits.
HOOK_DEPTH = 0.015
TEXTURE_SIZE = (1024, 1024)

bpy.ops.wm.open_mainfile(filepath=src)
for o in list(bpy.data.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o)
sheet = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = sheet
sheet.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
material = sheet.data.materials[0] if sheet.data.materials else None


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def decimate(obj, target):
    before = tris(obj)
    if target and before > target:
        select_only(obj)
        mod = obj.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = target / before
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return before


def cut(obj, x0, x1, name):
    """A copy of obj keeping only the faces between x0 and x1."""
    copy = obj.copy(); copy.data = obj.data.copy(); copy.name = name; copy.data.name = name
    bpy.context.collection.objects.link(copy)
    bm = bmesh.new(); bm.from_mesh(copy.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not (x0 <= f.calc_center_median().x < x1)], context='FACES')
    bm.to_mesh(copy.data); bm.free()
    return copy


def hang_point(obj):
    """Where the filter hangs: the middle of the intake tube, just behind it, at the glass rim."""
    co = [v.co for v in obj.data.vertices]
    tube = [v for v in co if v.z < TUBE_TOP_Z] or co
    x = (min(v.x for v in tube) + max(v.x for v in tube)) / 2
    y = max(v.y for v in tube) + GLASS_BEHIND_TUBE
    return Vector((x, y, max(v.z for v in co) - HOOK_DEPTH))


report = {}
for x0, x1, name, target in PLAN:
    piece = cut(sheet, x0, x1, name)
    origin = hang_point(piece)
    for v in piece.data.vertices:
        v.co -= origin
    before = decimate(piece, target)
    select_only(piece)
    bpy.ops.object.shade_smooth()
    piece.data.materials.clear()
    if material is not None:
        piece.data.materials.append(material)

    hull = piece.copy(); hull.data = piece.data.copy(); hull.name = name + "_Collider"; hull.data.name = hull.name
    bpy.context.collection.objects.link(hull)
    bm = bmesh.new(); bm.from_mesh(hull.data)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.delete(bm, geom=[g for g in result["geom_interior"] + result["geom_unused"] if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bm.to_mesh(hull.data); bm.free()
    hull.data.materials.clear()
    decimate(hull, COLLIDER_TRIS)

    bpy.ops.object.select_all(action='DESELECT')
    piece.select_set(True); hull.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True, mesh_smooth_type='FACE',
                             path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
    co = [v.co for v in piece.data.vertices]
    report[name] = {
        "tris_before": before, "tris_after": tris(piece), "collider_tris": tris(hull),
        "size_xyz": [round(max(v[i] for v in co) - min(v[i] for v in co), 3) for i in range(3)],
        "below_hook": round(-min(v.z for v in co), 3),
        "outside_glass": round(max(v.y for v in co), 3),
    }


def packed(name_part):
    for image in bpy.data.images:
        if name_part in image.name:
            path = os.path.join(outdir, "_" + image.name)
            image.filepath_raw = path
            image.save()
            return path
    return None


# Albedo and normal at 1024; metallic (B) and smoothness = 1 - roughness (G) packed together.
albedo, normal, rm = packed("basecolor"), packed("normal"), packed("rm")
Image.open(albedo).convert("RGB").resize(TEXTURE_SIZE, Image.LANCZOS).save(os.path.join(outdir, "Filters_Albedo.png"))
Image.open(normal).convert("RGB").resize(TEXTURE_SIZE, Image.LANCZOS).save(os.path.join(outdir, "Filters_Normal.png"))
orm = Image.open(rm).convert("RGB").resize(TEXTURE_SIZE, Image.LANCZOS).split()
black = Image.new("L", TEXTURE_SIZE, 0)
Image.merge("RGBA", (orm[2], black, black, orm[1].point(lambda v: 255 - v))).save(os.path.join(outdir, "Filters_MetallicSmoothness.png"))
for path in (albedo, normal, rm):
    if path and os.path.exists(path):
        os.remove(path)
print(json.dumps(report, indent=1))
