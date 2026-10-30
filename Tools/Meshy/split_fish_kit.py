# Splits a multi-fish Meshy kit into one FBX per fish: centre, straighten, plump (Y = thickness), decimate.
# Run with Blender's Python (Blender 4+/5, or `pip install bpy`):
#   python3 split_fish_kit.py <kit name> <meshy_kit.fbx> <output folder>
# Add a kit to KITS for a new collage: each fish is found by the centre of its loose part in the
# kit's X/Z layout (metres), then named, thickened and decimated as listed. Small loose pieces
# (a fin Meshy left detached) join the fish whose centre is nearest.
# Straightening: Meshy sometimes bends a fish's tail sideways. The fish's midline is traced from
# above, the front half is turned to point straight along X, and everything behind it is unbent
# along that midline, so a bent tail comes back in line with the body, fin and all.
# Plumping scales thickness (Y) by up to the factor given, weighted by how thick the fish
# already is around each point, so the body fills out while fins and the tail stay thin.
import bpy, sys, json, math
from mathutils import Vector, Matrix

# kit -> {island centre (x, z): (species id, thickness multiplier, target triangles)}
KITS = {
    "freshwater": {
        (-0.64, 0.45): ("RedTailShark", 1.6, 2400),
        (0.00, 0.45): ("SiameseAlgaeEater", 1.7, 2000),
        (0.64, 0.45): ("SilverDollar", 1.3, 2200),
        (-0.64, 0.00): ("BlackMolly", 2.3, 2400),
        (0.00, 0.00): ("Platy", 2.3, 2200),
        (0.64, 0.00): ("Oscar", 2.0, 2600),
        (-0.64, -0.47): ("GreenTerror", 1.8, 2600),
        (0.00, -0.47): ("BlueAcara", 1.8, 2600),
        (0.64, -0.47): ("GoldenMbuna", 1.8, 2400),
    },
    # Meshy_AI_Tetra_Fish_Collage (2026-10-27): the 8 tetras from the right half of the chart.
    "tetras_a": {
        (-0.56, 0.82): ("BloodfinTetra", 1.15, 1800),
        (0.34, 0.80): ("SerpaeTetra", 1.0, 1800),
        (-0.33, 0.30): ("XRayTetra", 1.0, 1800),
        (0.49, 0.28): ("BlackSkirtTetra", 1.0, 1800),
        (-0.23, -0.25): ("RedeyeTetra", 1.0, 1800),
        (0.53, -0.29): ("LemonTetra", 1.0, 1800),
        (-0.33, -0.81): ("BleedingHeartTetra", 1.0, 1800),
        (0.46, -0.80): ("BuenosAiresTetra", 1.0, 1800),
    },
    # Meshy_AI_Colorful_Aquarium_Fish_Collage (2026-10-27): 9 more tetras.
    "tetras_b": {
        (-0.67, 0.34): ("EmperorTetra", 1.1, 1800),
        (-0.07, 0.29): ("FlameTetra", 1.1, 1800),
        (0.58, 0.31): ("CongoTetra", 1.1, 1800),
        (-0.60, -0.06): ("PenguinTetra", 1.1, 1800),
        (0.01, -0.08): ("GlowlightTetra", 1.1, 1800),
        (0.63, -0.04): ("BucktoothTetra", 1.1, 1800),
        (-0.69, -0.40): ("RubyTetra", 1.1, 1800),
        (-0.09, -0.41): ("SilvertipTetra", 1.1, 1800),
        (0.62, -0.37): ("RummyNoseTetra", 1.1, 1800),
    },
    # Single-fish Meshy models (2026-10-30): the big wall-aquarium fish. One fish per file, so
    # every loose piece belongs to it. More triangles than the small fish since they're seen close up.
    "crimson_jewel": {(0.0, 0.0): ("CrimsonJewelCichlid", 1.0, 3000)},
    "silver_flamefin": {(0.0, 0.0): ("SilverFlamefin", 1.0, 3000)},
}

def straighten(pts):
    """Unbends a fish lying along X (Y sideways, Z up) in place; returns how far the tail had strayed."""
    xs = [p.x for p in pts]
    x0, x1 = min(xs), max(xs)
    L = x1 - x0
    bins = 40
    def b_of(x): return max(0, min(bins - 1, int((x - x0) / L * bins)))
    cols = [[] for _ in range(bins)]
    for p in pts: cols[b_of(p.x)].append(p.y)
    centre = []
    for c in cols:
        if not c: centre.append(None); continue
        c.sort()
        centre.append((c[int(len(c) * 0.05)] + c[int(len(c) * 0.95)]) / 2)
    for i in range(bins):
        if centre[i] is None:
            j = min((k for k in range(bins) if centre[k] is not None), key=lambda k: abs(k - i))
            centre[i] = centre[j]
    centre = [sum(centre[max(0, i - 2):i + 3]) / len(centre[max(0, i - 2):i + 3]) for i in range(bins)]
    # The head is the wider end (seen from above).
    def width(lo, hi):
        ys = [p.y for p in pts if lo <= (p.x - x0) / L <= hi]
        return max(ys) - min(ys) if ys else 0
    head_at_max = width(0.8, 1.0) > width(0.0, 0.2)
    # Straight line through the front half (head to mid-body).
    front = [i for i in range(bins) if (i >= bins * 0.55 if head_at_max else i < bins * 0.45)]
    mx = [x0 + (i + 0.5) / bins * L for i in front]
    my = [centre[i] for i in front]
    ax = sum(mx) / len(mx); ay = sum(my) / len(my)
    slope = sum((a - ax) * (b - ay) for a, b in zip(mx, my)) / max(1e-9, sum((a - ax) ** 2 for a in mx))
    def line(x): return ay + slope * (x - ax)
    # The midline as offsets from that line: zero over the front half.
    dev = [centre[i] - line(x0 + (i + 0.5) / bins * L) for i in range(bins)]
    for i in front: dev[i] = 0.0
    stray = max(abs(d) for d in dev)
    def spine(x):
        f = min(bins - 1.0, max(0.0, (x - x0) / L * bins - 0.5))
        i = min(bins - 2, int(f))
        return line(x) + dev[i] + (dev[i + 1] - dev[i]) * (f - i)
    h = L / bins
    def tangent(x): return Vector((1.0, (spine(x + h) - spine(x - h)) / (2 * h))).normalized()
    # Arc length along the spine, measured from the head end.
    n = 400
    head_x = x1 if head_at_max else x0
    step = (x0 - x1) / n if head_at_max else (x1 - x0) / n
    arc = [0.0]
    for k in range(n + 40):
        xa = head_x + step * k; xb = xa + step
        arc.append(arc[-1] + math.hypot(xb - xa, spine(xb) - spine(xa)))
    def arc_at(u):
        f = (u - head_x) / step
        if f < 0: return f * abs(step)  # ahead of the head end
        k = min(len(arc) - 2, int(f))
        return arc[k] + (arc[k + 1] - arc[k]) * (f - k)
    sign = -1 if head_at_max else 1
    head_y = line(head_x)
    for p in pts:
        # Nearest point on the midline (a few Newton steps from the vertex's own x).
        u = p.x
        for _ in range(8):
            t = tangent(u)
            u += Vector((p.x - u, p.y - spine(u))).dot(t) / max(0.3, t.x)
        t = tangent(u)
        side = Vector((p.x - u, p.y - spine(u))).dot(Vector((-t.y, t.x)))
        # Rebuild on a straight midline along X, the head where it was.
        p.x = head_x + sign * arc_at(u)
        p.y = head_y + side
    return stray

def run(kit, src, outdir):
    plan = KITS[kit]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    if len(plan) == 1:
        # A single-fish model may lie along Y; turn it to lie along X like the fish in the kits.
        ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
        if max(p.y for p in ws) - min(p.y for p in ws) > max(p.x for p in ws) - min(p.x for p in ws):
            m = obj.matrix_world
            obj.data.transform(m.inverted() @ Matrix.Rotation(math.radians(90), 4, 'Z') @ m)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    # Group the loose parts by fish: the nearest planned centre.
    groups = {}
    for part in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
        ws = [part.matrix_world @ v.co for v in part.data.vertices]
        cx = (min(p.x for p in ws) + max(p.x for p in ws)) / 2
        cz = (min(p.z for p in ws) + max(p.z for p in ws)) / 2
        key = min(plan, key=lambda k: (k[0] - cx) ** 2 + (k[1] - cz) ** 2)
        groups.setdefault(key, []).append(part)
    report = {}
    for key, parts in groups.items():
        sid, plump, tris = plan[key]
        # A detached fin floats beside the body: move it onto the fish's midline (seen from above).
        main = max(parts, key=lambda p: len(p.data.vertices))
        mws = [main.matrix_world @ v.co for v in main.data.vertices]
        for piece in parts:
            if piece is main: continue
            ws = [piece.matrix_world @ v.co for v in piece.data.vertices]
            lo, hi = min(p.x for p in ws), max(p.x for p in ws)
            near = [p.y for p in mws if lo <= p.x <= hi] or [p.y for p in mws]
            dy = (min(near) + max(near)) / 2 - (min(p.y for p in ws) + max(p.y for p in ws)) / 2
            inv = piece.matrix_world.inverted()
            for v, w in zip(piece.data.vertices, ws): v.co = inv @ (w + Vector((0, dy, 0)))
        bpy.ops.object.select_all(action='DESELECT')
        for p in parts: p.select_set(True)
        bpy.context.view_layer.objects.active = main
        if len(parts) > 1: bpy.ops.object.join()
        part = bpy.context.view_layer.objects.active
        # Work in world space: bake the import rotation into the mesh, then put it back so the
        # export uses the same axes as the original set.
        rot = part.matrix_world.copy()
        pts = [rot @ v.co for v in part.data.vertices]
        stray = straighten(pts) if kit != "freshwater" else 0.0
        centre = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2,
                         (min(p.y for p in pts) + max(p.y for p in pts)) / 2,
                         (min(p.z for p in pts) + max(p.z for p in pts)) / 2))
        pts = [p - centre for p in pts]
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
        report[sid] = {"pieces": len(parts), "tail_bend": round(stray, 3), "verts": len(part.data.vertices),
                       "tris_before": before, "tris_after": after, "dims_xyz": dims}
    print(json.dumps(report, indent=1))

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    run(args[0], args[1], args[2])
