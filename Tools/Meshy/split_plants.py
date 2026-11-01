# Splits a sheet of four AI-made aquarium plants (Tripo, Oct 2026) into one decimated FBX per plant,
# plus a low-poly _Collider mesh each, and paints each plant its own albedo texture.
# Run with Blender (4.5+/5.x):
#   blender -b --factory-startup --python split_plants.py -- <sheet.fbx> <output folder>
# The sheet's base colour map (<name>.fbm/*basecolor*) is read from beside the FBX.
#
# The sheet's texture is a patchwork with big grey (untextured) patches, so it is not used as is.
# Each plant gets new UVs instead: u goes once around the plant's stem (blades and leaves radiate
# from it, so a streak runs along each leaf) and v up its height. Its texture is vertical streaks in
# shades of the plant's own green (taken from the coloured parts of the sheet), darker at the base and
# lighter at the tips, and wraps around in u. Roots and the soil clump at the bottom (brown on the
# sheet) use a brown band at the top of the texture.
import bpy, bmesh, sys, os, math, random, colorsys
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, outdir = args[0], args[1]
os.makedirs(outdir, exist_ok=True)

# Plants left to right on the sheet: name, target triangles, how far up (share of height) brown
# faces count as roots, and how far up everything counts as the soil clump.
PLAN = [
    ("Crypt", 3200, 0.3, 0.0),
    ("JungleVal", 3600, 0.12, 0.0),
    ("AmazonSword", 3000, 0.12, 0.0),
    ("Aponogeton", 3600, 0.12, 0.07),
]
COLLIDER_TRIS = 160
TEX_W, TEX_H = 1024, 512
LEAF_V = 0.86   # v from 0 to LEAF_V: leaves; v from ROOT_V up: roots
ROOT_V = 0.9

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sheet = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = sheet
sheet.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# The sheet's colours, for each plant's green and to find the roots.
fbm = os.path.splitext(src)[0] + ".fbm"
base_path = [os.path.join(fbm, f) for f in os.listdir(fbm) if "basecolor" in f.lower()][0]
img = bpy.data.images.load(base_path)
iw, ih = img.size
pixels = np.empty(iw * ih * 4, dtype=np.float32)
img.pixels.foreach_get(pixels)
pixels = pixels.reshape(ih, iw, 4)[:, :, :3]


def sample(uv):
    x = int((uv.x % 1.0) * (iw - 1))
    y = int((uv.y % 1.0) * (ih - 1))
    return pixels[y, x]


# Loose parts, grouped into the four plants by where their centre lies along x.
bm = bmesh.new()
bm.from_mesh(sheet.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.faces.ensure_lookup_table()
uv_layer = bm.loops.layers.uv.active
seen, parts = set(), []
for f in bm.faces:
    if f.index in seen:
        continue
    stack, part = [f], []
    seen.add(f.index)
    while stack:
        g = stack.pop()
        part.append(g.index)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index)
                    stack.append(h)
    xs = [v.co.x for i in part for v in bm.faces[i].verts]
    parts.append((min(xs), max(xs), part))
parts.sort(key=lambda p: -len(p[2]))
big = sorted(parts[:4], key=lambda p: p[0])
groups = [list(p[2]) for p in big]
for lo, hi, part in parts[4:]:
    c = (lo + hi) / 2
    best = min(range(4), key=lambda k: 0 if big[k][0] <= c <= big[k][1] else min(abs(c - big[k][0]), abs(c - big[k][1])))
    groups[best] += part


def is_brown(rgb):
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    return 0.02 <= h <= 0.13 and s > 0.35 and v > 0.08


def is_green(rgb):
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    return 0.13 < h < 0.42 and s > 0.35 and v > 0.15


def paint(base_hsv, seed):
    """Vertical streaks in shades of the plant's colour; brown root band on top."""
    rnd = random.Random(seed)
    h0, s0, v0 = base_hsv
    tex = np.zeros((TEX_H, TEX_W, 3), dtype=np.float32)
    # Streak columns of random width, each its own shade; wraps around in u.
    x = 0
    cols = []
    while x < TEX_W:
        w = rnd.randint(3, 18)
        cols.append((x, min(TEX_W, x + w), rnd.uniform(-0.025, 0.025), rnd.uniform(-0.12, 0.1), rnd.uniform(-0.38, 0.22)))
        x += w
    leaf_rows = int(TEX_H * LEAF_V)
    root_row = int(TEX_H * ROOT_V)
    for x0, x1, dh, ds, dv in cols:
        for row in range(TEX_H):
            t = row / max(1, leaf_rows - 1)
            if row < leaf_rows:
                # Darker towards the base, a little lighter and yellower at the tips.
                hue = h0 + dh - 0.015 * max(0.0, t - 0.6)
                sat = s0 + ds - 0.05 * t
                val = (v0 + dv * v0) * (0.55 + 0.55 * min(1.0, t * 1.3))
            elif row < root_row:
                hue, sat, val = h0 + dh, s0 + ds, (v0 + dv * v0) * 1.05
            else:
                hue, sat, val = 0.075 + dh, 0.55 + ds, 0.36 + dv * 0.35
            rgb = colorsys.hsv_to_rgb(hue % 1.0, min(1.0, max(0.0, sat)), min(1.0, max(0.0, val)))
            tex[row, x0:x1] = rgb
    # A soft blur across u so streak edges aren't razor sharp, and fine grain along v.
    k = np.array([0.25, 0.5, 0.25], dtype=np.float32)
    tex = k[0] * np.roll(tex, 1, axis=1) + k[1] * tex + k[2] * np.roll(tex, -1, axis=1)
    grain = np.array([rnd.uniform(0.975, 1.025) for _ in range(TEX_H)], dtype=np.float32)
    grain = (np.roll(grain, 1) + grain + np.roll(grain, -1)) / 3.0
    tex *= grain[:, None, None]
    return np.clip(tex, 0.0, 1.0)


def save_png(tex, path):
    out = bpy.data.images.new(os.path.basename(path), TEX_W, TEX_H, alpha=False)
    rgba = np.ones((TEX_H, TEX_W, 4), dtype=np.float32)
    rgba[:, :, :3] = tex
    out.pixels.foreach_set(rgba.ravel())
    out.filepath_raw = path
    out.file_format = 'PNG'
    out.save()


for k, (name, tris, root_share, soil_share) in enumerate(PLAN):
    faces = set(groups[k])
    pbm = bm.copy()
    pbm.faces.ensure_lookup_table()
    bmesh.ops.delete(pbm, geom=[f for f in pbm.faces if f.index not in faces], context='FACES')
    zs = [v.co.z for v in pbm.verts]
    zmin, zmax = min(zs), max(zs)
    height = zmax - zmin
    # Stem axis: middle of the bottom tenth of the plant.
    low = [v.co for v in pbm.verts if v.co.z < zmin + 0.1 * height]
    cx = sum(c.x for c in low) / len(low)
    cy = sum(c.y for c in low) / len(low)

    # The plant's green, and which faces are roots or soil.
    puv = pbm.loops.layers.uv.active
    greens, root_faces = [], set()
    for f in pbm.faces:
        rgb = sum((Vector(sample(l[puv].uv)) for l in f.loops), Vector()) / len(f.loops)
        rel = (f.calc_center_median().z - zmin) / height
        if is_green(rgb):
            greens.append(colorsys.rgb_to_hsv(*rgb))
        if rel < soil_share or (rel < root_share and is_brown(rgb)):
            root_faces.add(f.index)
    greens = np.array(greens)
    base = tuple(np.median(greens, axis=0))
    # A touch greener than the sheet's yellow-green, which reads olive under the tank lights.
    base = (base[0] + 0.035, min(0.9, base[1] * 1.15), min(0.8, base[2]))
    print(name, "faces", len(pbm.faces), "base hsv", [round(c, 3) for c in base], "roots", len(root_faces))

    # Root/soil marker kept through decimation as a face attribute.
    root_layer = pbm.faces.layers.int.new("root")
    for f in pbm.faces:
        f[root_layer] = 1 if f.index in root_faces else 0

    # Near-duplicate vertices (sheet seams) stop the decimation early.
    bmesh.ops.remove_doubles(pbm, verts=pbm.verts, dist=0.002 * height)
    # So do edges shared by three or more faces (where leaves grow through each other): cut them apart.
    bmesh.ops.split_edges(pbm, edges=[e for e in pbm.edges if len(e.link_faces) > 2])
    # Pivot: stem axis at the bottom; Blender z up.
    for v in pbm.verts:
        v.co -= Vector((cx, cy, zmin))

    me = bpy.data.meshes.new(name)
    pbm.to_mesh(me)
    pbm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)

    def decimate(o, target):
        # One collapse pass can stop short on these thin open leaves; a few more get there.
        for _ in range(4):
            if len(o.data.polygons) <= target * 1.05:
                break
            mod = o.modifiers.new("dec", 'DECIMATE')
            mod.ratio = min(1.0, target / max(1, len(o.data.polygons)))
            mod.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=mod.name)

    decimate(obj, tris)
    # Collider: the plant's convex hull, cut down (plants are passable; it's for clicks and placing).
    hbm = bmesh.new()
    hbm.from_mesh(obj.data)
    hull = bmesh.ops.convex_hull(hbm, input=hbm.verts)
    bmesh.ops.delete(hbm, geom=list(set(hull["geom_interior"]) | set(hull["geom_unused"])), context='VERTS')
    bmesh.ops.triangulate(hbm, faces=hbm.faces)
    col_me = bpy.data.meshes.new(name + "_Collider")
    hbm.to_mesh(col_me)
    hbm.free()
    col = bpy.data.objects.new(name + "_Collider", col_me)
    bpy.context.scene.collection.objects.link(col)
    decimate(col, COLLIDER_TRIS)

    # New UVs: u around the stem, v up the height (roots in their own band).
    m = obj.data
    while m.uv_layers:
        m.uv_layers.remove(m.uv_layers[0])
    uvl = m.uv_layers.new(name="UVMap")
    roots = m.attributes["root"].data if "root" in m.attributes else None
    for poly in m.polygons:
        is_root = roots is not None and roots[poly.index].value == 1
        us = []
        for li in poly.loop_indices:
            co = m.vertices[m.loops[li].vertex_index].co
            us.append(math.atan2(co.y, co.x) / (2 * math.pi))
        # Keep each face's u continuous across the wrap (the texture repeats in u).
        ref = us[0]
        us = [u + round(ref - u) for u in us]
        for li, u in zip(poly.loop_indices, us):
            co = m.vertices[m.loops[li].vertex_index].co
            t = min(1.0, max(0.0, co.z / height))
            v = ROOT_V + 0.08 * t / max(root_share, soil_share, 0.05) if is_root else t * LEAF_V
            uvl.data[li].uv = (u, min(0.995, v))
    if "root" in m.attributes:
        m.attributes.remove(m.attributes["root"])
    for c in (obj, col):
        c.data.materials.clear()
    for p in m.polygons:
        p.use_smooth = True

    save_png(paint(base, k + 1), os.path.join(outdir, name + "_Albedo.png"))
    # Not metallic, a little wet sheen (metallic in R, smoothness in A).
    gloss = bpy.data.images.new(name + "_MetallicSmoothness", 8, 8, alpha=True)
    gloss.pixels.foreach_set(np.tile(np.array([0.0, 0.0, 0.0, 0.35], dtype=np.float32), 64))
    gloss.filepath_raw = os.path.join(outdir, name + "_MetallicSmoothness.png")
    gloss.file_format = 'PNG'
    gloss.save()

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    col.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print("exported", name, len(obj.data.polygons), "tris,", len(col.data.polygons), "collider tris, height", round(height, 3))
