# Splits a Meshy aquarium equipment sheet into one decimated FBX per piece, plus a low-poly
# _Collider mesh each, and packs the textures for Unity at 1024. Two sheets so far:
#   equipment: three filter bases (for under the tank, not used yet), three heaters and three
#              bubblers in a 3x3 grid, cheapest on the left. Textures: Equipment_*.png
#   filters:   the three filters that hang on the back glass at the top of the tank, each on a
#              grey bar standing in for the glass, which is cut away. Textures: Filters_*.png
# Run with Blender's Python (Blender 4+/5, or `pip install bpy`):
#   python3 split_equipment.py -- <meshy_sheet.fbx> <output folder> [equipment|filters]
# The textures (<name>.png, _normal, _metallic, _roughness) are read from beside the FBX.
# The third heater comes fused to its outside controller by two cables: the tube and the box
# are cut out by the boxes in HEATER2_SPLIT and the cables are dropped (the game draws its own).
import bpy, bmesh, sys, os, json
from mathutils import Vector
from PIL import Image

args = sys.argv[sys.argv.index("--") + 1:]
src, outdir = args[0], args[1]
sheet_kind = args[2] if len(args) > 2 else "equipment"
os.makedirs(outdir, exist_ok=True)

# grid centre of each loose part (x, z in metres, as laid out in the sheet) -> name, target triangles, pivot
# pivot "floor": bottom centre (stands on the sand); "centre": middle of the bounding box;
# "clip": the top middle of the grey glass bar it hangs on (the bar itself is removed).
PLAN = {
    (-0.73, 0.30): ("FilterBase0", 1400, "floor"),
    (-0.12, 0.28): ("FilterBase1", 1600, "floor"),
    (0.60, 0.28): ("FilterBase2", 2000, "floor"),
    (-0.74, -0.17): ("Heater0", 700, "centre"),
    (-0.12, -0.17): ("Heater1", 1200, "centre"),
    (0.60, -0.09): ("Heater2", 0, "split"),
    (-0.74, -0.44): ("Bubbler0", 700, "floor"),
    (-0.07, -0.45): ("Bubbler1", 600, "floor"),
    (0.62, -0.40): ("Bubbler2", 1400, "floor"),
}
# name -> (min corner, max corner, target triangles); a face goes to the box holding its centre.
HEATER2_SPLIT = {
    "Heater2": ((0.60, -1.0, -1.0), (1.0, 1.0, -0.125), 800),
    "Heater2_Controller": ((0.29, -0.16, -0.15), (0.575, 0.01, 0.075), 800),
}
PLAN_FILTERS = {
    (-0.70, 0.0): ("Filter0", 1600, "clip"),
    (-0.03, 0.0): ("Filter1", 1600, "clip"),
    (0.68, 0.0): ("Filter2", 2000, "clip"),
}
# The glass bar: faces in this corner of each filter (y towards the back, z at the top) whose
# texture is light grey.
BAR_MIN_Y, BAR_MIN_Z, BAR_LIGHTNESS = 0.25, 0.38, 0.55
COLLIDER_TRIS = 80
if sheet_kind == "filters":
    PLAN = PLAN_FILTERS
TEXTURES = "Filters" if sheet_kind == "filters" else "Equipment"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sheet = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = sheet
sheet.select_set(True)
# Work in world space from here on (the importer's axis rotation is baked into the mesh).
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0001)
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')

material = bpy.data.materials.new(TEXTURES)
albedo_image = Image.open(os.path.splitext(src)[0] + ".png").convert("RGB")


def bounds(obj):
    xs = [v.co.x for v in obj.data.vertices]; ys = [v.co.y for v in obj.data.vertices]; zs = [v.co.z for v in obj.data.vertices]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def cut(obj, lo, hi, name):
    """A copy of obj keeping only the faces whose centre is inside the box lo..hi."""
    copy = obj.copy(); copy.data = obj.data.copy(); copy.name = name
    bpy.context.collection.objects.link(copy)
    bm = bmesh.new(); bm.from_mesh(copy.data)
    def inside(f):
        c = f.calc_center_median()
        return all(lo[i] <= c[i] <= hi[i] for i in range(3))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not inside(f)], context='FACES')
    bm.to_mesh(copy.data); bm.free()
    return copy


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


def cut_bar(obj):
    """Deletes the grey glass bar a filter hangs on; returns the bar's (min y, max y, max z)."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    w, h = albedo_image.size
    bar = []
    for f in bm.faces:
        c = f.calc_center_median()
        if c.y < BAR_MIN_Y or c.z < BAR_MIN_Z:
            continue
        u = sum(l[uv].uv.x for l in f.loops) / len(f.loops)
        v = sum(l[uv].uv.y for l in f.loops) / len(f.loops)
        r, g, b = albedo_image.getpixel((min(w - 1, max(0, int(u * w))), min(h - 1, max(0, int((1 - v) * h)))))
        if (r + g + b) / (3 * 255) > BAR_LIGHTNESS:
            bar.append(f)
    ys = [v.co.y for f in bar for v in f.verts]; zs = [v.co.z for f in bar for v in f.verts]
    found = (min(ys), max(ys), max(zs)) if bar else None
    bmesh.ops.delete(bm, geom=bar, context='FACES')
    bm.to_mesh(obj.data); bm.free()
    return found


def finish(obj, name, target, pivot):
    obj.name = name; obj.data.name = name
    bar = cut_bar(obj) if pivot == "clip" else None
    lo, hi = bounds(obj)
    centre = (lo + hi) / 2
    origin = Vector((centre.x, centre.y, lo.z)) if pivot == "floor" else centre
    if bar:
        origin = Vector((centre.x, (bar[0] + bar[1]) / 2, bar[2]))
    for v in obj.data.vertices:
        v.co -= origin
    obj.location = (0, 0, 0)
    before = decimate(obj, target)
    select_only(obj)
    bpy.ops.object.shade_smooth()
    obj.data.materials.clear(); obj.data.materials.append(material)

    # Collider: a decimated convex hull of the piece.
    hull = obj.copy(); hull.data = obj.data.copy(); hull.name = name + "_Collider"; hull.data.name = hull.name
    bpy.context.collection.objects.link(hull)
    bm = bmesh.new(); bm.from_mesh(hull.data)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.delete(bm, geom=[g for g in result["geom_interior"] + result["geom_unused"] if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bm.to_mesh(hull.data); bm.free()
    hull.data.materials.clear()
    decimate(hull, COLLIDER_TRIS)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); hull.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True, mesh_smooth_type='FACE',
                             path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
    lo, hi = bounds(obj)
    return {"tris_before": before, "tris_after": tris(obj), "collider_tris": tris(hull), "size_xyz": [round(x, 3) for x in hi - lo]}


report = {}
for part in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
    lo, hi = bounds(part)
    cx, cz = (lo.x + hi.x) / 2, (lo.z + hi.z) / 2
    key = min(PLAN, key=lambda k: (k[0] - cx) ** 2 + (k[1] - cz) ** 2)
    name, target, pivot = PLAN[key]
    if pivot == "split":
        for piece, (plo, phi, ptarget) in HEATER2_SPLIT.items():
            report[piece] = finish(cut(part, plo, phi, piece), piece, ptarget, "centre")
        bpy.data.objects.remove(part)
    else:
        report[name] = finish(part, name, target, pivot)

# Textures: albedo and normal at 1024; metallic (R) and smoothness = 1 - roughness (A) packed together.
base = os.path.splitext(src)[0]
size = (1024, 1024)
albedo_image.resize(size, Image.LANCZOS).save(os.path.join(outdir, TEXTURES + "_Albedo.png"))
Image.open(base + "_normal.png").convert("RGB").resize(size, Image.LANCZOS).save(os.path.join(outdir, TEXTURES + "_Normal.png"))
metal = Image.open(base + "_metallic.png").convert("L").resize(size, Image.LANCZOS)
rough = Image.open(base + "_roughness.png").convert("L").resize(size, Image.LANCZOS)
smooth = rough.point(lambda v: 255 - v)
black = Image.new("L", size, 0)
Image.merge("RGBA", (metal, black, black, smooth)).save(os.path.join(outdir, TEXTURES + "_MetallicSmoothness.png"))
print(json.dumps(report, indent=1))
