# Renders a side view and a top view of every fish model for the balance console's Swimming tab
# (Tools/BalanceConsole/fish/<Species>_side.webp and _top.webp). Each picture is cropped exactly to
# the fish's length, nose on the left, so a position across the picture is a fraction of the body
# from nose (0) to tail tip (1), the same measure the game's swim spine uses.
# Run with Blender's Python (`pip install bpy`):  python3 console_pictures.py <project root>
import bpy, sys, os, glob, math
from mathutils import Matrix
from PIL import Image

root = sys.argv[1]
out = os.path.join(root, "Tools", "BalanceConsole", "fish")
os.makedirs(out, exist_ok=True)
WIDTH = 480

def atlas_for(folder):
    pngs = [p for p in glob.glob(os.path.join(folder, "*_Albedo.png"))]
    return pngs[0] if pngs else None

for fbx in sorted(glob.glob(os.path.join(root, "Assets", "Resources", "Fish", "*", "*.fbx"))):
    sid = os.path.basename(fbx)[:-4]
    tex = atlas_for(os.path.dirname(fbx))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    img = bpy.data.images.load(tex) if tex else None
    mat = bpy.data.materials.new("fish"); mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.6
    if img:
        t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = img
        mat.node_tree.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    for o in meshes:
        o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
        if o.name.startswith("Fill_"): o.hide_render = True; continue
        o.data.materials.clear(); o.data.materials.append(mat)
    pts = [v.co for o in meshes if not o.name.startswith("Fill_") for v in o.data.vertices]
    lo = [min(p[i] for p in pts) for i in range(3)]; hi = [max(p[i] for p in pts) for i in range(3)]
    size = [hi[i] - lo[i] for i in range(3)]
    la = 0 if size[0] >= size[1] else 1; lat = 1 - la
    L = size[la]
    def width(sel):
        w = [p[lat] for p in pts if sel((p[la] - lo[la]) / L)]
        return max(w) - min(w) if w else 0
    head_at_max = width(lambda t: t > 0.8) > width(lambda t: t < 0.2)
    c = [(lo[i] + hi[i]) / 2 for i in range(3)]
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12
    sc.render.film_transparent = True
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.85, 0.9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.4
    cam = bpy.data.cameras.new("c"); cam.type = 'ORTHO'; cam.ortho_scale = L
    co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
    for view in ("side", "top"):
        h = size[2] if view == "side" else size[lat]
        sc.render.resolution_x = WIDTH
        sc.render.resolution_y = max(8, int(round(WIDTH * h / L)))
        # Camera looks at the fish with the length axis running across the picture.
        pos = list(c)
        if view == "side":
            pos[lat] -= 3
            # Looking along +lateral: build the rotation from the camera's axes.
            right = [0, 0, 0]; right[la] = 1
            upv = (0, 0, 1)
        else:
            pos[2] += 3
            right = [0, 0, 0]; right[la] = 1
            upv = [0, 0, 0]; upv[lat] = 1
        from mathutils import Vector
        r = Vector(right); u = Vector(upv); back = r.cross(u)  # camera +Z points away from the view
        if view == "side" and back[lat] > 0: back = -back
        if view == "top" and back[2] < 0: u = -u; back = r.cross(u)
        if view == "side": u = back.cross(r) if False else Vector((0, 0, 1)); back = r.cross(u)
        if (back[lat] > 0 and view == "side"): r = -r; back = r.cross(u)
        co.matrix_world = Matrix((
            (r[0], u[0], back[0], pos[0]),
            (r[1], u[1], back[1], pos[1]),
            (r[2], u[2], back[2], pos[2]),
            (0, 0, 0, 1)))
        path = f"/tmp/_cp_{view}.png"
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = Image.open(path).convert("RGBA")
        # Nose on the left: the head is at the max end of the length axis when r points along +axis.
        head_right = (r[la] > 0) == head_at_max
        if head_right: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        im.save(os.path.join(out, f"{sid}_{view}.webp"), quality=85)
    print(sid, "head at", "max" if head_at_max else "min", "L", round(L, 3))
