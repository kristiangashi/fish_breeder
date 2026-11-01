# Gives a Meshy fish the pectoral fins Meshy left out (it paints the fish flat, like a picture,
# and leaves the paired side fins off). Run after split_fish_kit.py and before split_fins.py:
#   python3 add_pectorals.py <folder of fish FBXs> <output folder>
# Each fish in FINS gets a fin on each side, rooted just behind the gill cover: a thin closed
# fan that points back and a little down and stands out from the body at its resting angle.
# Its colour comes from one of the fish's own fins (src: a line from root to tip across that
# fin in the side view, and how far either side of it to sample), so it matches the other fins.
# The new faces use a second material named "Pectoral", which split_fins.py turns into the
# Pectoral part (and drops again), so the fins move like the medium fish's other pectorals.
# Positions are in the side view: u = fraction of the length from the nose, v = height / length
# from the middle of the fish's height (see the grid renders the fish checklist describes).
import bpy, bmesh, sys, os, math, glob, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, intersect_point_tri_2d

# sid -> root (u, v), length, root height, widest height (all / length), droop and flare
# (degrees), pointed tip, and the colour source (root (u, v), tip (u, v), half width).
FINS = {
    # Meshy_AI_Colorful_Freshwater_Fish_Collection (2026-11-01).
    "Frontosa": dict(root=(0.30, -0.08), length=0.15, h0=0.035, hmax=0.065, droop=25, flare=28, pointed=False,
                     src=((0.40, -0.14), (0.52, -0.20), 0.012)),
    "TexasCichlid": dict(root=(0.26, -0.06), length=0.14, h0=0.035, hmax=0.06, droop=22, flare=28, pointed=False,
                         src=((0.60, -0.12), (0.78, -0.155), 0.015)),
    "Pumpkinseed": dict(root=(0.30, -0.03), length=0.17, h0=0.04, hmax=0.065, droop=18, flare=26, pointed=True,
                        src=((0.36, -0.17), (0.385, -0.29), 0.025)),
    "JackDempsey": dict(root=(0.28, -0.06), length=0.13, h0=0.035, hmax=0.06, droop=22, flare=28, pointed=False,
                        src=((0.64, -0.15), (0.80, -0.205), 0.015)),
    "PeacockBass": dict(root=(0.28, -0.06), length=0.13, h0=0.035, hmax=0.055, droop=20, flare=26, pointed=False,
                        src=((0.37, -0.135), (0.46, -0.185), 0.015)),
    "ArcticGrayling": dict(root=(0.22, -0.05), length=0.14, h0=0.03, hmax=0.045, droop=25, flare=30, pointed=True,
                           src=((0.445, -0.09), (0.425, -0.165), 0.012)),
}

NA, NB = 8, 6          # grid steps along and across each fin
GAP = 0.004            # how far the fin stands off the body at the root (/ length)
THICK = 0.004          # fin thickness (/ length)

def load(path):
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
    bpy.ops.import_scene.fbx(filepath=path)
    o = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    return o

def half_height(a, f):
    h0, hm = f["h0"], f["hmax"]
    if f["pointed"]:
        # Widest a little before halfway, then tapering to a point.
        return 0.5 * (h0 + (hm - h0) * math.sin(min(a, 0.45) / 0.45 * math.pi / 2)) * (1 - max(0.0, (a - 0.45) / 0.55)) ** 0.8
    h = h0 + (hm - h0) * math.sin(min(a, 0.7) / 0.7 * math.pi / 2)
    if a > 0.7: h *= math.sqrt(max(0.0, 1 - ((a - 0.7) / 0.3) ** 2))
    return 0.5 * h

def add(o, f):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    tree = BVHTree.FromBMesh(bm)
    pts = [v.co for v in bm.verts]
    x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
    z0, z1 = min(p.z for p in pts), max(p.z for p in pts)
    L = x1 - x0; cz = (z0 + z1) / 2
    def width(lo, hi):
        ys = [p.y for p in pts if lo <= (p.x - x0) / L <= hi]
        return max(ys) - min(ys) if ys else 0
    head = 1 if width(0.8, 1.0) > width(0.0, 0.2) else -1
    nose = x1 if head > 0 else x0
    X = lambda u: nose - head * u * L
    Z = lambda v: cz + v * L
    far = 4 * L

    def sample_uv(u, v):
        """The texture coordinate of the fish's own skin at (u, v), seen from the -Y side."""
        o_ = Vector((X(u), -far, Z(v)))
        loc, n, idx, d = tree.ray_cast(o_, Vector((0, 1, 0)))
        if loc is None:
            loc, n, idx, d = tree.find_nearest(Vector((X(u), 0, Z(v))))
        bm.faces.ensure_lookup_table()  # the tree indexes the original faces, which keep their places
        face = bm.faces[idx]
        loops = list(face.loops)
        p2 = loc.xz
        for i in range(1, len(loops) - 1):
            tri = (loops[0], loops[i], loops[i + 1])
            if intersect_point_tri_2d(p2, *[l.vert.co.xz for l in tri]) or i == len(loops) - 2:
                a3 = [l.vert.co for l in tri]
                b3 = [tri[k][uv].uv.to_3d() for k in range(3)]
                return barycentric_transform(loc, *a3, *b3).to_2d()

    def surface_y(x, z, s):
        loc, n, idx, d = tree.ray_cast(Vector((x, s * far, z)), Vector((0, -s, 0)))
        return loc.y if loc is not None else 0.0

    (sr, st, sh) = f["src"]
    sdir = Vector((st[0] - sr[0], st[1] - sr[1]))
    sperp = Vector((-sdir.y, sdir.x)).normalized()
    droop = math.radians(f["droop"]); flare = math.radians(f["flare"])
    ur, vr = f["root"]
    rx, rz = X(ur), Z(vr)
    d = Vector((-head * math.cos(droop), 0, -math.sin(droop)))
    e = Vector((-d.z, 0, d.x))
    if e.z < 0: e = -e
    made = []
    for s in (-1, 1):
        root = Vector((rx, surface_y(rx, rz, s), rz))
        out = Vector((0, s, 0))
        grid = {}
        for i in range(NA + 1):
            a = i / NA
            for j in range(NB + 1):
                b = -1 + 2 * j / NB
                reach = a * f["length"] * L
                p = root + e * (b * half_height(a, f) * L) + (d * math.cos(flare) + out * math.sin(flare)) * reach + out * GAP * L
                sy = surface_y(p.x, p.z, s)
                if s * p.y < s * sy + GAP * L: p.y = sy + s * GAP * L
                v = bm.verts.new(p)
                c = Vector(sr) + sdir * a + sperp * (b * sh)
                grid[i, j] = (v, sample_uv(c.x, c.y))
        faces = []
        for i in range(NA):
            for j in range(NB):
                q = [grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]]
                fc = bm.faces.new([x[0] for x in q])
                for l, (_, t) in zip(fc.loops, q): l[uv].uv = t
                fc.material_index = 1; fc.smooth = True
                faces.append(fc)
        bm.normal_update()
        if sum(fc.normal.y for fc in faces) * s < 0:
            bmesh.ops.reverse_faces(bm, faces=faces)
        res = bmesh.ops.solidify(bm, geom=faces, thickness=THICK * L)
        for g in res["geom"]:
            if isinstance(g, bmesh.types.BMFace): g.material_index = 1; g.smooth = True
        made.append(len(faces))
    bm.normal_update()
    bm.to_mesh(o.data); bm.free()
    m = bpy.data.materials.new("Pectoral")
    while len(o.data.materials) < 1: o.data.materials.append(bpy.data.materials.new("Body"))
    o.data.materials.append(m)
    return made

if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    src, out = args[0], args[1]
    os.makedirs(out, exist_ok=True)
    report = {}
    for path in sorted(glob.glob(os.path.join(src, "*.fbx"))):
        sid = os.path.basename(path)[:-4]
        if sid not in FINS: continue
        o = load(path)
        report[sid] = add(o, FINS[sid])
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(out, sid + ".fbx"), use_selection=True, mesh_smooth_type='FACE',
                                 path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
    print(json.dumps(report))
