# Splits every Meshy fish into separate parts so fins and tails can be mixed between species:
#   Body, Dorsal, Ventral (anal + pelvic fins), Pectoral and Tail, plus Fill_<Part> patches that
#   close the openings each part leaves in the body (the game shows a patch only when that part
#   comes from another fish).
# Run with Blender's Python (`pip install bpy`, Blender 4+/5):
#   python3 split_fins.py <folder of unsplit fish FBXs> <output folder>
# It expects the original single-mesh models (as in Resources/Fish before commit "Split fish
# models into parts"); copy the results back over Resources/Fish, keeping the .meta files.
# How it works: each face's thickness is measured by casting rays inward (the "shape diameter");
# thin faces are fins. The caudal peduncle (where the tail starts) is the narrowest part of the
# thick body in its back half, or the hand-checked value in OVERRIDES; the mesh is cut flat there.
# Fins mostly behind the cut are the tail, sideways pieces in the front are pectoral fins, and the
# rest are dorsal (above the body's midline) or ventral (below). Barbels and lips stay on the head.
import bpy, bmesh, sys, os, math, json, glob
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PARTS = ["Body", "Dorsal", "Ventral", "Pectoral", "Tail"]

def load(path):
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
    bpy.ops.import_scene.fbx(filepath=path)
    o = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    # Bake the import transform so mesh coordinates are world coordinates (Z up).
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    return o

def frame(bm):
    """Length axis index (0 X or 1 Y), head sign (+1 head at max end), lateral axis index."""
    pts = [v.co for v in bm.verts]
    size = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
    la = 0 if size[0] >= size[1] else 1
    lat = 1 - la
    lo = min(p[la] for p in pts); L = size[la]
    def width(sel):
        w = [p[lat] for p in pts if sel((p[la] - lo) / L)]
        return max(w) - min(w) if w else 0
    head = 1 if width(lambda t: t > 0.8) > width(lambda t: t < 0.2) else -1
    return la, head, lat, L

def sdf(bm, tree):
    out = []
    for f in bm.faces:
        c = f.calc_center_median(); n = f.normal
        if n.length < 1e-9: out.append(0.0); continue
        d0 = -n
        t1 = n.orthogonal().normalized(); t2 = n.cross(t1)
        ds = [d0] + [(d0 + 0.45 * (a * t1 + b * t2)).normalized() for a, b in ((1,0),(-1,0),(0,1),(0,-1))]
        hits = []
        for d in ds:
            loc, hn, idx, dist = tree.ray_cast(c + d * 1e-5, d)
            if loc is not None and idx != f.index and hn.dot(d) > 0: hits.append(dist)
            else: hits.append(0.0)
        hits.sort(); out.append(hits[len(hits) // 2])
    return out

def smooth_labels(bm, labels, keep, iters=3):
    for _ in range(iters):
        new = list(labels)
        for f in bm.faces:
            if not keep(labels[f.index]): continue
            votes = {}
            for e in f.edges:
                for g in e.link_faces:
                    if g is f: continue
                    votes[labels[g.index]] = votes.get(labels[g.index], 0) + g.calc_area()
            votes[labels[f.index]] = votes.get(labels[f.index], 0) + f.calc_area() * 1.2
            best = max(votes, key=votes.get)
            if keep(best): new[f.index] = best
        labels = new
    return labels

def components(bm, labels, label):
    seen = set(); comps = []
    for f in bm.faces:
        if labels[f.index] != label or f.index in seen: continue
        stack = [f]; seen.add(f.index); comp = []
        while stack:
            g = stack.pop(); comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen and labels[h.index] == label:
                        seen.add(h.index); stack.append(h)
        comps.append(comp)
    return comps

# Hand-checked caudal peduncle positions (fraction of the length from the nose) where the
# automatic guess cut too early, and a lower thinness ratio for the very flat silver dollar.
OVERRIDES = {
    "Angelfish": {"peduncle": 0.70}, "Betta": {"rays": 0.03}, "NeonTetra": {"rays": 0.03}, "BlackMolly": {"peduncle": 0.70}, "Clownfish": {"peduncle": 0.70},
    "Corydoras": {"peduncle": 0.77}, "FancyGoldfish": {"peduncle": 0.62}, "GreenTerror": {"peduncle": 0.75},
    "Guppy": {"peduncle": 0.64, "rays": 0.03}, "Platy": {"peduncle": 0.67}, "RedTailShark": {"peduncle": 0.77},
    "SiameseAlgaeEater": {"peduncle": 0.76}, "SilverDollar": {"peduncle": 0.76, "thin": 0.1}, "TigerBarb": {"peduncle": 0.74},
    # Wild tetras (2026-10-27).
    "RedeyeTetra": {"peduncle": 0.70}, "PenguinTetra": {"peduncle": 0.72}, "SilvertipTetra": {"peduncle": 0.75},
    "BleedingHeartTetra": {"peduncle": 0.76}, "SerpaeTetra": {"peduncle": 0.75}, "XRayTetra": {"peduncle": 0.73},
    "LemonTetra": {"peduncle": 0.70}, "BloodfinTetra": {"peduncle": 0.68},
}

SID = ""

def classify(o, thin=0.2):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    la, head, lat, L = frame(bm)
    s = sdf(bm, tree)
    ref = sorted(s)[int(len(s) * 0.97)]
    thin = OVERRIDES.get(SID, {}).get("thin", thin)
    labels = ["Fin" if v < thin * ref else "Body" for v in s]
    labels = smooth_labels(bm, labels, lambda l: True, 4)
    # Keep the biggest body piece; small thick islands are fin roots, small thin islands inside the body are body.
    comps = components(bm, labels, "Body")
    comps.sort(key=lambda c: -sum(f.calc_area() for f in c))
    for comp in comps[1:]:
        for f in comp: labels[f.index] = "Fin"
    # Small thin patches lying on the body surface are body; thin slivers sticking out are fin.
    total = sum(f.calc_area() for f in bm.faces)
    body_faces = [f for f in bm.faces if labels[f.index] == "Body"]
    body_tree = BVHTree.FromPolygons([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in body_faces])
    for comp in components(bm, labels, "Fin"):
        area = sum(f.calc_area() for f in comp)
        border = set(g.index for f in comp for e in f.edges for g in e.link_faces if labels[g.index] == "Body")
        if area < total * 0.004 and border:
            reach = max(body_tree.find_nearest(v.co)[3] for f in comp for v in f.verts)
            if reach < 0.012 * L:
                for f in comp: labels[f.index] = "Body"

    # Body outline per slice along the length (body faces only).
    al = lambda p: p[la] * head  # along, larger = nearer the head
    allv = [v.co for v in bm.verts]
    hmax = max(al(p) for p in allv); hmin = min(al(p) for p in allv)
    bins = 48
    def bi(a): return max(0, min(bins - 1, int((hmax - a) / (hmax - hmin) * bins)))  # 0 = nose
    body = [f for f in bm.faces if labels[f.index] == "Body"]
    bverts = set(v for f in body for v in f.verts)
    zc = sum(v.co.z for v in bverts) / len(bverts); lc = sum(v.co[lat] for v in bverts) / len(bverts)
    top = [None] * bins; bot = [None] * bins; wid = [0.0] * bins
    for v in bverts:
        b = bi(al(v.co))
        top[b] = v.co.z if top[b] is None else max(top[b], v.co.z)
        bot[b] = v.co.z if bot[b] is None else min(bot[b], v.co.z)
        wid[b] = max(wid[b], abs(v.co[lat] - lc))
    # The caudal peduncle: the narrowest point of the thick core of the body in its back half.
    core = set(v for f in bm.faces if s[f.index] >= 0.5 * ref for v in f.verts)
    ct = [None] * bins; cb = [None] * bins
    for v in core:
        b = bi(al(v.co))
        ct[b] = v.co.z if ct[b] is None else max(ct[b], v.co.z)
        cb[b] = v.co.z if cb[b] is None else min(cb[b], v.co.z)
    last = max(b for b in range(bins) if ct[b] is not None)
    cand = [b for b in range(int(bins * 0.45), last + 1) if ct[b] is not None]
    ped = min(cand, key=lambda b: (ct[b] - cb[b], -b))
    ped = OVERRIDES.get(SID, {}).get("peduncle", ped / bins) * bins
    ped_along = hmax - (ped + 0.5) / bins * (hmax - hmin)
    for b in range(bins):
        if top[b] is None:
            j = min((k for k in range(bins) if top[k] is not None), key=lambda k: abs(k - b))
            top[b], bot[b], wid[b] = top[j], bot[j], wid[j]
    mid = [(top[b] + bot[b]) / 2 for b in range(bins)]
    zs = {}
    for v in bverts: zs.setdefault(bi(al(v.co)), []).append(v.co.z)
    hi = [None] * bins; lo = [None] * bins
    for b, z in zs.items():
        z.sort(); hi[b] = z[int(len(z) * 0.93)]; lo[b] = z[int(len(z) * 0.07)]
    for f in bm.faces:
        if labels[f.index] != "Body": continue
        c = f.calc_center_median(); b = bi(al(c))
        if hi[b] is None: continue
        if c.z > hi[b] + 0.035 * L or c.z < lo[b] - 0.035 * L: labels[f.index] = "Fin"
    # Loose fin rays (betta, guppy, neon): thin body faces far from the thick core go with the fins.
    reach = OVERRIDES.get(SID, {}).get("rays")
    outline = OVERRIDES.get(SID, {}).get("outline", reach)
    core_faces = [f for f in bm.faces if s[f.index] >= 0.4 * ref]
    if reach:
        core_tree = BVHTree.FromPolygons([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in core_faces])
        for f in bm.faces:
            if labels[f.index] == "Body" and s[f.index] < 0.25 * ref and core_tree.find_nearest(f.calc_center_median())[3] > reach * L:
                labels[f.index] = "Fin"
    if outline:
        # ...and anything rising well above the thick core's outline.
        czs = {}
        for f in core_faces:
            for v in f.verts: czs.setdefault(bi(al(v.co)), []).append(v.co.z)
        for f in bm.faces:
            if labels[f.index] != "Body": continue
            c = f.calc_center_median(); z = czs.get(bi(al(c)))
            if z and c.z > max(z) + outline * L: labels[f.index] = "Fin"
    front = hmax - 0.12 * (hmax - hmin)
    # Whole fin pieces that stick out sideways in the front part are pectoral fins.
    for comp in components(bm, labels, "Fin"):
        cs = [f.calc_center_median() for f in comp]
        a = sum(al(c) for c in cs) / len(cs)
        out = max(abs(c[lat] - lc) / max(wid[bi(al(c))], 1e-6) for c in cs)
        zc_ = sum(c.z for c in cs) / len(cs); bb = bi(a)
        if a > ped_along and bb < bins * 0.62 and out > 1.25 and bot[bb] + 0.08 * (top[bb] - bot[bb]) < zc_ < mid[bb] + 0.25 * (top[bb] - bot[bb]):
            for f in comp: labels[f.index] = "Pectoral"
    # Fins mostly in front of the peduncle stay dorsal/ventral even where they reach past it;
    # fins mostly behind it are the tail; fins merged with the tail are cut at the peduncle.
    whole = {}
    for comp in components(bm, labels, "Fin"):
        area = sum(f.calc_area() for f in comp)
        behind = sum(f.calc_area() for f in comp if al(f.calc_center_median()) < ped_along) / area
        for f in comp: whole[f.index] = "Tail" if behind > 0.65 else "Front" if behind < 0.35 else "Cut"
    for f in bm.faces:
        c = f.calc_center_median(); a = al(c)
        kind = whole.get(f.index, "Cut")
        if kind == "Tail" or (kind == "Cut" and a < ped_along): labels[f.index] = "Tail"; continue
        if labels[f.index] != "Fin": continue
        b = bi(a)
        if a > front: labels[f.index] = "Body"  # barbels and lips stay with the head
        elif c.z > mid[b]: labels[f.index] = "Dorsal"
        else: labels[f.index] = "Ventral"
    labels = smooth_labels(bm, labels, lambda l: l != "Body", 3)
    info = dict(pedAlong=ped_along, hmax=hmax, hmin=hmin, la=la, head=head, lengthAxis="XY"[la], L=round(L, 3), peduncle=round(ped / bins, 3))
    return bm, labels, info

COLORS = {"Body": (0.75, 0.75, 0.72), "Dorsal": (0.95, 0.3, 0.25), "Ventral": (0.25, 0.5, 0.95), "Pectoral": (0.95, 0.85, 0.2), "Tail": (0.3, 0.85, 0.4), "Fin": (1, 0, 1)}

def split(o, bm, labels):
    """One object per part, all with the same (identity) transform."""
    objs = {}
    for part in PARTS:
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if labels[f.index] != part], context='FACES')
        if not b2.faces: b2.free(); continue
        me = bpy.data.meshes.new(part); b2.to_mesh(me); b2.free()
        for m in o.data.materials: me.materials.append(m)
        ob = bpy.data.objects.new(part, me); bpy.context.scene.collection.objects.link(ob)
        objs[part] = ob
    return objs

def drop_crumbs(ob, keep=0.03):
    """Deletes tiny loose pieces (tips of fin rays cut off from their fin) from a part."""
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    seen = set(); pieces = []
    for f in bm.faces:
        if f in seen: continue
        stack = [f]; seen.add(f); piece = []
        while stack:
            g = stack.pop(); piece.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen: seen.add(h); stack.append(h)
        pieces.append(piece)
    total = len(bm.faces)
    crumbs = [f for p in pieces if len(p) < total * keep for f in p]
    bmesh.ops.delete(bm, geom=crumbs, context='FACES')
    bm.to_mesh(ob.data); bm.free()
    return len(crumbs)

def vertex_parts(bm, labels):
    """For each vertex position, the parts whose faces use it (to tell which opening is which)."""
    out = {}
    for f in bm.faces:
        for v in f.verts:
            out.setdefault(tuple(round(c, 6) for c in v.co), set()).add(labels[f.index])
    return out

def fill_body_holes(ob, vparts=None, on_cut=None):
    """Closes the openings left where the fins and tail were cut off, so a body can wear other
    fins: each opening is walked round and fanned from its middle, wound like the faces round
    it, and coloured from one rim corner so the texture isn't smeared across the atlas."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uv = bm.loops.layers.uv.active
    tag = bm.faces.layers.int.new("fill")
    edges = [e for e in bm.edges if e.is_boundary]
    # The one old corner on each open edge gives its direction: old face goes a -> b.
    direction = {}
    for e in edges:
        l = e.link_loops[0]
        direction[e] = (l.vert, l.link_loop_next.vert, l)
    by_vert = {}
    for e in edges:
        for v in e.verts: by_vert.setdefault(v, []).append(e)
    used = set(); made = 0
    for e0 in edges:
        if e0 in used: continue
        loop = []; e = e0; v = direction[e0][1]
        while e is not None and e not in used:
            used.add(e); loop.append(e)
            nxt = [f for f in by_vert[v] if f not in used]
            if not nxt: break
            e = nxt[0]; v = e.other_vert(v)
        if len(loop) < 3: continue
        # The walk as a vertex sequence, split into simple cycles wherever it touches itself.
        seq = [direction[loop[0]][0]]
        v = seq[0]
        for x in loop:
            v = x.other_vert(v); seq.append(v)
        cycles = []; stack = []
        for v in seq[:-1] + [seq[-1]]:
            if v in stack:
                i = stack.index(v); cyc = stack[i:]; del stack[i + 1:]
                if len(cyc) >= 3: cycles.append(cyc)
            else:
                stack.append(v)
        if len(stack) >= 3: cycles.append(stack)
        for cyc in cycles:
            rim = [l for v in cyc for l in v.link_loops]
            centre = sum((v.co for v in cyc), Vector()) / len(cyc)
            pick_uv = min(rim, key=lambda l: (l.vert.co - centre).length)[uv].uv.copy() if uv and rim else None
            try:
                face = bm.faces.new(list(reversed(cyc)))
            except ValueError:
                continue
            tris = bmesh.ops.triangulate(bm, faces=[face], quad_method='BEAUTY', ngon_method='BEAUTY')["faces"]
            votes = {}
            for v in cyc:
                for part in (vparts or {}).get(tuple(round(c, 6) for c in v.co), ()):
                    if part != "Body": votes[part] = votes.get(part, 0) + 1
            code = PARTS.index(max(votes, key=votes.get)) if votes else 4
            if on_cut and sum(1 for v in cyc if on_cut(v.co)) >= max(6, 0.3 * len(cyc)): code = 4
            for f in tris:
                f.smooth = True; made += 1
                f[tag] = code
                if pick_uv is not None:
                    for l in f.loops: l[uv].uv = pick_uv
    bm.normal_update()
    bm.to_mesh(ob.data); bm.free()
    return made

def bisect_at(o, info):
    """Cuts the mesh cleanly across the caudal peduncle so the tail comes off along a flat seam."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    co = Vector((0, 0, 0)); no = Vector((0, 0, 0))
    co[info["la"]] = info["pedAlong"] * info["head"]; no[info["la"]] = 1
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    bm.to_mesh(o.data); bm.free()


def split_fills(body):
    """Moves each patch that closes an opening into its own object, Fill_<Part>, so the game can
    show it only when that part comes from another fish (with its own fin on, it would z-fight)."""
    objs = {}
    for code, part in enumerate(PARTS):
        if code == 0: continue
        bm = bmesh.new(); bm.from_mesh(body.data)
        tag = bm.faces.layers.int.get("fill")
        keep = [f for f in bm.faces if f[tag] == code]
        if keep:
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[tag] != code], context='FACES')
            me = bpy.data.meshes.new("Fill_" + part); bm.to_mesh(me)
            for m in body.data.materials: me.materials.append(m)
            ob = bpy.data.objects.new("Fill_" + part, me); bpy.context.scene.collection.objects.link(ob)
            objs["Fill_" + part] = ob
        bm.free()
    bm = bmesh.new(); bm.from_mesh(body.data)
    tag = bm.faces.layers.int.get("fill")
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[tag] != 0], context='FACES')
    bm.faces.layers.int.remove(tag)
    bm.to_mesh(body.data); bm.free()
    return objs


if __name__ == "__main__":
    global_ns = globals()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    root = os.path.join(args[0], '')
    out = args[1]
    report = {}
    for path in sorted(glob.glob(root + '*/*.fbx')):
        sid = os.path.basename(path)[:-4]
        o = load(path)
        global_ns["SID"] = sid
        bm, labels, info = classify(o)
        bisect_at(o, info)
        OVERRIDES.setdefault(sid, {})["peduncle"] = info["peduncle"]
        bm, labels, info = classify(o)
        objs = split(o, bm, labels)
        vparts = vertex_parts(bm, labels)
        bpy.data.objects.remove(o, do_unlink=True)
        drop_crumbs(objs["Body"])
        cut = info["pedAlong"] * info["head"]; la = info["la"]; tol = 0.004 * info["L"]
        filled = fill_body_holes(objs["Body"], vparts, lambda co: abs(co[la] - cut) < tol)
        objs.update(split_fills(objs["Body"]))
        for ob in objs.values(): ob.data.name = f"{sid}_{ob.name}"
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objs.values(): ob.select_set(True)
        dest = os.path.join(out, os.path.relpath(path, root))
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=dest, use_selection=True, mesh_smooth_type='FACE', path_mode='STRIP',
                                 embed_textures=False, add_leaf_bones=False, bake_anim=False)
        report[sid] = {"peduncle": info["peduncle"], "tris": {p: len(ob.data.polygons) for p, ob in objs.items()}}
        for ob in list(objs.values()): bpy.data.objects.remove(ob, do_unlink=True)
    print(json.dumps(report, indent=1))
