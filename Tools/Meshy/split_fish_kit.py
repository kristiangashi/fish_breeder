# Splits a multi-fish Meshy kit into one FBX per fish: centre, straighten, plump (Y = thickness), decimate.
# Run with Blender's Python (Blender 4+/5, or `pip install bpy`):
#   python3 split_fish_kit.py <kit name> <meshy_kit.fbx> <output folder> [<albedo> <normal> <roughness> [size]]
# Add a kit to KITS for a new collage: each fish is found by the centre of its loose part in the
# kit's X/Z layout (metres), then named, thickened and decimated as listed. Small loose pieces
# (a fin Meshy left detached) join the fish whose centre is nearest.
# Straightening: Meshy sometimes bends a fish's tail sideways. The fish's midline is traced from
# above, the front half is turned to point straight along X, and everything behind it is unbent
# along that midline, so a bent tail comes back in line with the body, fin and all.
# Plumping scales thickness (Y) by up to the factor given, weighted by how thick the fish
# already is around each point, so the body fills out while fins and the tail stay thin.
# An optional fourth number tips a fish Meshy made nose-down back level (degrees, + = nose up).
# Re-baking (give the kit's albedo, normal and roughness maps and a texture size after the output
# folder): decimating a Meshy atlas tears its texture where triangles end up spanning UV islands,
# so the decimated fish gets fresh UVs and its own maps baked from the full-detail mesh:
# <sid>_Albedo.png, <sid>_Normal.png and <sid>_Smoothness.png (inverted roughness) next to the FBX.
# Fish under 2400 triangles (the small ones) get maps half that size.
# Rounding (kits in ROUND_KITS, whose second number is the real fish's width as a share of its
# length): Meshy's later collages are as thick as real fish but slab-sided, flat cut-outs with a
# hard rim. Each cross-section is reshaped into a full oval of the real width, fins left alone.
# Front fill (also ROUND_KITS, when re-baking): Meshy paints these fish from the side and leaves
# grey filler where the surface turns away from the side view (the nose, forehead and belly), so
# the side pattern is carried round onto those surfaces (a clown loach's eye stripe meets over its head).
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix

# kit -> {island centre (x, z): (species id, thickness multiplier, target triangles[, pitch])}
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
    # Meshy_AI_Colorful_Freshwater_Fish_Collection (2026-11-01): six real medium fish, all made
    # about 7% as thick as long, so they are plumped to their real width (12-17% of the length).
    "colorful_freshwater": {
        (-0.47, 0.72): ("Frontosa", 2.6, 3000, 12.0),
        (0.47, 0.75): ("TexasCichlid", 2.3, 3000),
        (-0.47, -0.02): ("Pumpkinseed", 2.3, 3000),
        (0.49, -0.01): ("JackDempsey", 2.3, 3000),
        (-0.46, -0.75): ("PeacockBass", 2.5, 3000),
        (0.48, -0.75): ("ArcticGrayling", 1.9, 3000),
    },
    # Meshy_AI_Colorful_Aquarium_Fish_Collage and _Reef_Fish_Collage (2026-11-02): rounded to real
    # widths (see ROUND_KITS) and front-filled. Fish that grow big get more triangles.
    "aquarium_collage": {
        (-0.66, 0.20): ("BanggaiCardinal", 0.17, 1800),
        (-0.02, 0.19): ("CoralBeauty", 0.21, 1800),
        (0.64, 0.19): ("ClownLoach", 0.19, 2400),
        (-0.66, -0.22): ("ClownTrigger", 0.25, 3000),
        (0.00, -0.23): ("GiantTrevally", 0.19, 3000),
        (0.64, -0.23): ("WatchmanGoby", 0.18, 1800),
    },
    "reef_collage": {
        (-0.66, 0.20): ("NapoleonWrasse", 0.26, 3000),
        (0.02, 0.21): ("BlueTang", 0.19, 2400),
        (0.67, 0.21): ("PicassoTrigger", 0.25, 2400),
        (-0.66, -0.22): ("RoyalGramma", 0.21, 1800),
        (0.02, -0.22): ("YellowTang", 0.2, 2400),
        (0.67, -0.23): ("ZebraLoach", 0.19, 1800),
    },
}

# Kits whose second number is a real width (share of the length) to round the body out to.
ROUND_KITS = {"aquarium_collage", "reef_collage"}

def round_body(pts, width, power=2.2, cell=0.012):
    """Reshapes a slab-sided fish (lying along X, Y sideways, Z up) so every cross-section is an
    oval as wide as the real fish (width x length at the thickest), keeping its side outline and
    leaving thin fins as they are. Each vertex's sideways offset from the midline is scaled."""
    x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
    L = x1 - x0
    # The midline of each column (side view) is the median of its vertices' sideways positions;
    # each cell's half-width is the farthest any of its vertices sits from it.
    colys = {}
    for p in pts: colys.setdefault(round(p.x / cell), []).append(p.y)
    colmid = {kx: sorted(ys)[len(ys) // 2] for kx, ys in colys.items()}
    hw = {}
    for p in pts:
        kx = round(p.x / cell); k = (kx, round(p.z / cell))
        hw[k] = max(hw.get(k, 0.0), abs(p.y - colmid[kx]))
    cells = hw
    cols = {}
    for (kx, kz) in cells: cols.setdefault(kx, []).append(kz)
    info = {}
    for kx, kzs in cols.items():
        lo, hi = min(kzs), max(kzs)
        mid = (lo + hi) / 2; band = max(1.0, 0.25 * (hi - lo))
        # The middle of the column's height: its median, so a pectoral fin sticking out doesn't count.
        mids = sorted(hw[kx, kz] for kz in kzs if abs(kz - mid) <= band)
        tcol = mids[len(mids) // 2] if mids else 0.0
        body = [kz for kz in kzs if hw[kx, kz] >= 0.3 * tcol] if tcol > 0 else []
        if not body: continue
        info[kx] = dict(t=tcol, lo=min(body), hi=max(body), body=set(body), mid=colmid[kx])
    keys = sorted(info)
    near = lambda kx: [j for j in keys if abs(j - kx) <= 2]
    smooth = {kx: sum(info[j]["t"] for j in near(kx)) / len(near(kx)) for kx in keys}
    ts = sorted(t for kx, t in smooth.items() if 0.15 <= (kx * cell - x0) / L <= 0.75)
    tref = ts[int(len(ts) * 0.8)]
    s = width * L / 2 / max(1e-9, tref)
    k = {}
    for kx in keys:
        c = info[kx]
        zc = (c["lo"] + c["hi"]) / 2; hh = (c["hi"] - c["lo"]) / 2 + 0.5
        for kz in c["body"]:
            u = min(1.0, abs(kz - zc) / hh)
            e = s * smooth[kx] * (1 - u ** power) ** (1 / power)
            k[kx, kz] = max(0.55, min(3.0, e / max(1e-9, hw[kx, kz])))
    # Soften cell to cell so the surface stays smooth.
    soft = {}
    for (kx, kz) in k:
        ring = [k.get((kx + i, kz + j), 1.0) for i in (-1, 0, 1) for j in (-1, 0, 1) if (kx + i, kz + j) in cells]
        soft[kx, kz] = sum(ring) / len(ring)
    def at(x, z):
        fx, fz = x / cell, z / cell
        ix, iz = math.floor(fx), math.floor(fz)
        tx, tz = fx - ix, fz - iz
        out = 0.0
        for a, wa in ((ix, 1 - tx), (ix + 1, tx)):
            for b, wb in ((iz, 1 - tz), (iz + 1, tz)):
                out += wa * wb * soft.get((a, b), 1.0)
        return out
    def mid_at(x):
        fx = x / cell; ix = math.floor(fx); t = fx - ix
        a = info.get(ix, info.get(ix + 1)); b = info.get(ix + 1, a)
        if a is None: return 0.0
        return a["mid"] * (1 - t) + b["mid"] * t
    for p in pts:
        m = mid_at(p.x)
        p.y = m + (p.y - m) * at(p.x, p.z)
    return s

def side_pictures(high, sid, outdir, px=1024, margin=0.02, erode=0.025):
    """Pictures of the full-detail fish from both sides (its own texture, unlit), with the rim
    where the surface turns away cut off and filled again from the pattern inside it, so the
    side pattern runs right to the outline. Returns the two images and the framing."""
    sc = bpy.context.scene
    ws = [v.co for v in high.data.vertices]
    x0, x1 = min(p.x for p in ws), max(p.x for p in ws)
    z0, z1 = min(p.z for p in ws), max(p.z for p in ws)
    L = x1 - x0
    W = L * (1 + 2 * margin); H = (z1 - z0) + 2 * margin * L
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    hidden = []
    for o in sc.objects:
        if o is not high and not o.hide_render: o.hide_render = True; hidden.append(o)
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
    sc.view_settings.exposure = 0; sc.view_settings.gamma = 1
    sc.render.resolution_percentage = 100
    sc.render.resolution_x = px; sc.render.resolution_y = max(8, int(round(px * H / W)))
    cam = bpy.data.cameras.new("Side"); cam.type = 'ORTHO'; cam.ortho_scale = max(W, H)
    co = bpy.data.objects.new("Side", cam); sc.collection.objects.link(co); sc.camera = co
    old = list(high.data.materials)
    em = bpy.data.materials.new(sid + "_Flat"); em.use_nodes = True; nt = em.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    tex = [n for n in old[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image.colorspace_settings.name == 'sRGB'][0]
    img = nt.nodes.new("ShaderNodeTexImage"); img.image = tex.image
    e = nt.nodes.new("ShaderNodeEmission"); nt.links.new(img.outputs[0], e.inputs[0])
    nt.links.new(e.outputs[0], nt.nodes["Material Output"].inputs[0])
    high.data.materials.clear(); high.data.materials.append(em)
    pics = []
    r = max(1, int(erode * L / W * px))
    for s in (-1, 1):
        co.location = (cx, s * 10 * L, cz)
        co.rotation_euler = (math.pi / 2, 0, 0 if s < 0 else math.pi)
        cam.clip_start = 0.01; cam.clip_end = 20 * L
        path = os.path.join(outdir, f"_{sid}_side{'L' if s < 0 else 'R'}.png")
        sc.render.filepath = path; bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path); im.colorspace_settings.name = 'sRGB'
        w_, h_ = im.size
        a = np.array(im.pixels[:], dtype=np.float32).reshape(h_, w_, 4)
        rgb = a[..., :3].copy(); valid = a[..., 3] > 0.99
        for _ in range(r):
            v = valid.copy()
            v[1:] &= valid[:-1]; v[:-1] &= valid[1:]; v[:, 1:] &= valid[:, :-1]; v[:, :-1] &= valid[:, 1:]
            valid = v
        rgb *= valid[..., None]
        kept = valid.copy()
        for _ in range(4 * r + 80):
            if valid.all(): break
            acc = np.zeros_like(rgb); cnt = np.zeros(valid.shape, np.float32)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                sh = np.roll(valid, (dy, dx), (0, 1)); acc += np.roll(rgb, (dy, dx), (0, 1)) * sh[..., None]; cnt += sh
            grow = (~valid) & (cnt > 0)
            rgb[grow] = acc[grow] / cnt[grow][:, None]
            valid = valid | grow
        # Soften the filled rim so the fill's streaks don't show on the back and belly.
        for _ in range(2 * r):
            soft = rgb.copy()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)): soft += np.roll(rgb, (dy, dx), (0, 1))
            rgb = np.where(kept[..., None], rgb, soft / 5)
        a[..., :3] = rgb; a[..., 3] = 1
        im.pixels[:] = a.ravel()
        im.filepath_raw = path; im.file_format = 'PNG'; im.save()
        pics.append(im)
    high.data.materials.clear()
    for m in old: high.data.materials.append(m)
    bpy.data.objects.remove(co, do_unlink=True)
    for o in hidden: o.hide_render = False
    sc.render.film_transparent = False
    return pics, (cx, cz, W, H)

def add_front_fill(mat, pics, frame):
    """Mixes the side pictures into the fish's colour wherever its surface faces away from the
    sides (fully more than about 55 degrees off, fading out by 30), left picture on the left side, right on the right."""
    (left, right), (cx, cz, W, H) = pics, frame
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    orig = bsdf.inputs["Base Color"].links[0].from_socket
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    pos = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Position"], pos.inputs[0])
    nrm = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Normal"], nrm.inputs[0])
    def op(kind, a, b=None):
        n = nt.nodes.new("ShaderNodeMath"); n.operation = kind
        for i, val in enumerate((a, b)):
            if val is None: continue
            if isinstance(val, (int, float)): n.inputs[i].default_value = val
            else: nt.links.new(val, n.inputs[i])
        return n.outputs[0]
    du = op('DIVIDE', op('SUBTRACT', pos.outputs[0], cx), W)
    v = op('ADD', op('DIVIDE', op('SUBTRACT', pos.outputs[2], cz), H), 0.5)
    def sample(im, sign):
        uu = op('ADD', op('MULTIPLY', du, sign), 0.5)
        c = nt.nodes.new("ShaderNodeCombineXYZ"); nt.links.new(uu, c.inputs[0]); nt.links.new(v, c.inputs[1])
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; t.extension = 'EXTEND'
        nt.links.new(c.outputs[0], t.inputs[0])
        return t.outputs[0]
    pick = nt.nodes.new("ShaderNodeMix"); pick.data_type = 'RGBA'
    nt.links.new(op('GREATER_THAN', nrm.outputs[1], 0.0), pick.inputs[0])
    nt.links.new(sample(left, 1), pick.inputs[6]); nt.links.new(sample(right, -1), pick.inputs[7])
    w = nt.nodes.new("ShaderNodeMapRange"); w.clamp = True
    nt.links.new(op('ABSOLUTE', nrm.outputs[1]), w.inputs[0])
    w.inputs[1].default_value = 0.55; w.inputs[2].default_value = 0.85; w.inputs[3].default_value = 1.0; w.inputs[4].default_value = 0.0
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'
    nt.links.new(w.outputs[0], mix.inputs[0]); nt.links.new(orig, mix.inputs[6]); nt.links.new(pick.outputs[2], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])

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

def bake(high, low, maps, size, outdir, sid, fill=False):
    """Bakes colour, normals and roughness from the full-detail fish onto the decimated one, through fresh UVs."""
    albedo, normal, rough = maps
    high.data.transform(high.matrix_world); high.matrix_world = Matrix.Identity(4)
    mat = bpy.data.materials.new(sid + "_High"); mat.use_nodes = True
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    def tex(path, colour):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(path, check_existing=True)
        n.image.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'
        return n
    nt.links.new(tex(albedo, True).outputs[0], bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tex(normal, False).outputs[0], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    nt.links.new(tex(rough, False).outputs[0], bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0
    high.data.materials.clear(); high.data.materials.append(mat)
    if fill:
        add_front_fill(mat, *side_pictures(high, sid, outdir))
    # Fresh UVs on the decimated fish.
    bpy.ops.object.select_all(action='DESELECT'); low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    lmat = bpy.data.materials.new(sid); lmat.use_nodes = True
    low.data.materials.clear(); low.data.materials.append(lmat)
    target = lmat.node_tree.nodes.new("ShaderNodeTexImage"); lmat.node_tree.nodes.active = target
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 4
    sc.render.bake.margin = 8
    dims = [max(v.co[i] for v in high.data.vertices) - min(v.co[i] for v in high.data.vertices) for i in range(3)]
    reach = max(dims) * 0.01
    high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    for name, kind, colour, px in (("Albedo", 'DIFFUSE', True, size), ("Normal", 'NORMAL', False, size // 2), ("Rough", 'ROUGHNESS', False, size // 2)):
        img = bpy.data.images.new(f"{sid}_{name}", px, px, alpha=False)
        img.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'
        target.image = img
        kw = dict(pass_filter={'COLOR'}) if kind == 'DIFFUSE' else {}
        bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=reach, max_ray_distance=reach * 3, **kw)
        if name == "Rough":
            # Unity's smoothness is the inverse of roughness.
            px_ = list(img.pixels)
            for i in range(0, len(px_), 4):
                for c in range(3): px_[i + c] = 1.0 - px_[i + c]
            img.pixels[:] = px_
            name = "Smoothness"
        img.filepath_raw = os.path.join(outdir, f"{sid}_{name}.png"); img.file_format = 'PNG'; img.save()
    lmat.node_tree.nodes.remove(target)
    bpy.data.objects.remove(high, do_unlink=True)

def run(kit, src, outdir, maps=None, size=2048):
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
        sid, plump, tris = plan[key][:3]
        pitch = plan[key][3] if len(plan[key]) > 3 else 0.0
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
        if pitch:
            # Level a nose-down fish: turn it in the side view about its middle, nose up.
            ws_ = lambda sel: (lambda ys: max(ys) - min(ys) if ys else 0)([p.y for p in pts if sel(p.x)])
            x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
            head = 1 if ws_(lambda x: x > x1 - 0.2 * (x1 - x0)) > ws_(lambda x: x < x0 + 0.2 * (x1 - x0)) else -1
            t = math.radians(pitch) * head
            pts = [Vector((p.x * math.cos(t) - p.z * math.sin(t), p.y, p.x * math.sin(t) + p.z * math.cos(t))) for p in pts]
            c2 = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2, 0, (min(p.z for p in pts) + max(p.z for p in pts)) / 2))
            pts = [p - c2 for p in pts]
        # Plump the body but not the fins: how thick the fish is around each point (in side view)
        # decides how much that point is pushed out, so thin fins and the tail stay thin.
        if kit in ROUND_KITS:
            plump = round(round_body(pts, plump), 2)
            for v, p in zip(part.data.vertices, pts): v.co = rot.inverted() @ p
            cell = None
        else:
            cell = 0.012
        thick = {}
        for p in (pts if cell else []):
            k = (round(p.x / cell), round(p.z / cell))
            thick[k] = max(thick.get(k, 0.0), abs(p.y))
        tmax = max(thick.values()) if thick else 1.0
        def local(p):
            kx, kz = round(p.x / cell), round(p.z / cell)
            return max(thick.get((kx + i, kz + j), 0.0) for i in (-1, 0, 1) for j in (-1, 0, 1))
        for v, p in zip(part.data.vertices, pts if cell else []):
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
        high = None
        if maps:
            high = part.copy(); high.data = part.data.copy(); bpy.context.scene.collection.objects.link(high)
            high.select_set(False)
        mod = part.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = min(1.0, tris / before)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.shade_smooth()
        after = sum(len(p.vertices) - 2 for p in part.data.polygons)
        if high:
            bpy.context.view_layer.objects.active = high; high.select_set(True)
            bpy.ops.object.shade_smooth(); high.select_set(False)
            bake(high, part, maps, size if tris >= 2400 else size // 2, outdir, sid, kit in ROUND_KITS)
            bpy.ops.object.select_all(action='DESELECT'); part.select_set(True)
            bpy.context.view_layer.objects.active = part
        bpy.ops.export_scene.fbx(filepath=f"{outdir}/{sid}.fbx", use_selection=True, mesh_smooth_type='FACE',
                                 path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
        ws = [part.matrix_world @ v.co for v in part.data.vertices]
        dims = [round(max(p[i] for p in ws) - min(p[i] for p in ws), 3) for i in range(3)]
        report[sid] = {"pieces": len(parts), "tail_bend": round(stray, 3), "plump": plump, "verts": len(part.data.vertices),
                       "tris_before": before, "tris_after": after, "dims_xyz": dims}
    print(json.dumps(report, indent=1))

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    run(args[0], args[1], args[2], args[3:6] if len(args) >= 6 else None, int(args[6]) if len(args) > 6 else 2048)
