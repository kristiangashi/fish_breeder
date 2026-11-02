# Turns one dense Meshy decoration (a single model, hundreds of thousands of triangles) into a
# tank piece: base centred on the origin, a decimated render mesh with fresh UVs and its own maps
# baked from the full-detail model, and a coarse <id>_Collider mesh (not convex, so caves stay open).
# Run with Blender 4+/5:
#   blender -b --factory-startup --python bake_decoration.py -- <id> <meshy.fbx> <albedo> <normal> <roughness> <output folder> [triangles] [texture size]
# Writes <id>.fbx, <id>_Albedo.png, <id>_Normal.png and <id>_MetallicSmoothness.png (not metallic,
# smoothness = 1 - roughness in alpha), the files TankDecor.MaterialFor gives a piece of its own.
# Made for the Stone Spire Grotto (2026-11-02): 434k triangles cut to 6000, collider 600.
import bpy, sys, os, math
import numpy as np
from mathutils import Matrix, Vector

COLLIDER_TRIS = 600

def decimate(o, target):
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris <= target: return
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    mod = o.modifiers.new("Decimate", 'DECIMATE'); mod.ratio = target / tris; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)

def run(name, src, albedo, normal, rough, outdir, tris=6000, size=2048):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    high = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    high.data.transform(high.matrix_world); high.matrix_world = Matrix.Identity(4)
    # Base centre on the origin, Z up.
    ws = [v.co for v in high.data.vertices]
    base = Vector(((min(p.x for p in ws) + max(p.x for p in ws)) / 2, (min(p.y for p in ws) + max(p.y for p in ws)) / 2, min(p.z for p in ws)))
    high.data.transform(Matrix.Translation(-base))
    height = max(v.co.z for v in high.data.vertices)

    mat = bpy.data.materials.new(name + "_High"); mat.use_nodes = True
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
    for p in high.data.polygons: p.use_smooth = True

    low = high.copy(); low.data = high.data.copy(); low.name = name; low.data.name = name
    bpy.context.scene.collection.objects.link(low)
    bpy.ops.object.select_all(action='DESELECT'); low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')
    decimate(low, tris)
    col = low.copy(); col.data = low.data.copy(); col.name = name + "_Collider"; col.data.name = name + "_Collider"
    bpy.context.scene.collection.objects.link(col)
    decimate(col, COLLIDER_TRIS)
    col.data.materials.clear()

    # Fresh UVs on the decimated piece, then bake colour, normals and roughness onto them.
    bpy.ops.object.select_all(action='DESELECT'); low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    lmat = bpy.data.materials.new(name); lmat.use_nodes = True
    low.data.materials.clear(); low.data.materials.append(lmat)
    target = lmat.node_tree.nodes.new("ShaderNodeTexImage"); lmat.node_tree.nodes.active = target
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 4
    sc.render.bake.margin = 8
    reach = height * 0.01
    high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    baked = {}
    for key, kind, colour, px in (("Albedo", 'DIFFUSE', True, size), ("Normal", 'NORMAL', False, size), ("Rough", 'ROUGHNESS', False, size // 2)):
        img = bpy.data.images.new(f"{name}_{key}", px, px, alpha=False)
        img.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'
        target.image = img
        kw = dict(pass_filter={'COLOR'}) if kind == 'DIFFUSE' else {}
        bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=reach, max_ray_distance=reach * 3, **kw)
        baked[key] = img
    for key in ("Albedo", "Normal"):
        img = baked[key]; img.filepath_raw = os.path.join(outdir, f"{name}_{key}.png"); img.file_format = 'PNG'; img.save()
    r = baked["Rough"]; w, h = r.size
    px = np.array(r.pixels[:], dtype=np.float32).reshape(h, w, 4)
    gloss = bpy.data.images.new(name + "_MetallicSmoothness", w, h, alpha=True)
    out = np.zeros_like(px); out[..., 3] = 1.0 - px[..., 0]
    gloss.pixels.foreach_set(out.ravel())
    gloss.filepath_raw = os.path.join(outdir, name + "_MetallicSmoothness.png"); gloss.file_format = 'PNG'; gloss.save()
    lmat.node_tree.nodes.remove(target)
    bpy.data.objects.remove(high, do_unlink=True)

    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True); col.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print("exported", name, len(low.data.polygons), "tris,", len(col.data.polygons), "collider tris, height", round(height, 3))

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    run(args[0], args[1], args[2], args[3], args[4], args[5],
        int(args[6]) if len(args) > 6 else 6000, int(args[7]) if len(args) > 7 else 2048)
