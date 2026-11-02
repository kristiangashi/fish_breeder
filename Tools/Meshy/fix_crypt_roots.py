# Takes the roots and bulb off the Cryptocoryne made by split_plants.py and cuts its base off flat, so
# the leaves come straight out of the gravel, then rebuilds its convex-hull collider. Run with Blender:
#   blender -b --factory-startup --python fix_crypt_roots.py -- <Crypt.fbx> <out Crypt.fbx> [cut share]
# Root faces are the ones split_plants.py textured from the brown band at the top of the texture (v >= 0.885).
# The cut share (default 0.1) is how much of the remaining height goes under the sand.
import bpy, bmesh, sys
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]
cut = float(args[2]) if len(args) > 2 else 0.1
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
objs={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
for o in objs.values():
    bpy.context.view_layer.objects.active=o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
obj=objs["Crypt"]; old_col=objs["Crypt_Collider"]
bm=bmesh.new(); bm.from_mesh(obj.data)
uv=bm.loops.layers.uv.active
roots=[f for f in bm.faces if min(l[uv].uv.y for l in f.loops)>=0.885]
bmesh.ops.delete(bm, geom=roots, context='FACES')
# Bits left floating once the roots are gone (and anything tiny) go too.
seen=set(); parts=[]
for f in bm.faces:
    if f in seen: continue
    st=[f]; part=[]; seen.add(f)
    while st:
        g=st.pop(); part.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h not in seen: seen.add(h); st.append(h)
    parts.append(part)
print("parts",sorted(len(p) for p in parts)[-10:], len(parts))
drop=[f for p in parts if len(p)<12 for f in p]
bmesh.ops.delete(bm, geom=drop, context='FACES')
zmin=min(v.co.z for v in bm.verts); zmax=max(v.co.z for v in bm.verts)
# Seat it in the substrate: cut the base off flat, so the leaves come straight out of the gravel.
z=zmin+cut*(zmax-zmin)
geom=bm.verts[:]+bm.edges[:]+bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0,0,z), plane_no=(0,0,1), clear_inner=True)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
zmin=min(v.co.z for v in bm.verts)
lo=[v.co for v in bm.verts if v.co.z<zmin+0.01]
cx=sum(c.x for c in lo)/len(lo); cy=sum(c.y for c in lo)/len(lo)
for v in bm.verts: v.co-=Vector((cx,cy,zmin))
bm.to_mesh(obj.data); bm.free()
for p in obj.data.polygons: p.use_smooth=True
print("Crypt faces",len(obj.data.polygons),"height",max(v.co.z for v in obj.data.vertices))
# New collider: convex hull, cut down.
bpy.data.objects.remove(old_col)
hbm=bmesh.new(); hbm.from_mesh(obj.data)
hull=bmesh.ops.convex_hull(hbm, input=hbm.verts)
bmesh.ops.delete(hbm, geom=list(set(hull["geom_interior"])|set(hull["geom_unused"])), context='VERTS')
bmesh.ops.triangulate(hbm, faces=hbm.faces)
cme=bpy.data.meshes.new("Crypt_Collider"); hbm.to_mesh(cme); hbm.free()
while cme.uv_layers: cme.uv_layers.remove(cme.uv_layers[0])
col=bpy.data.objects.new("Crypt_Collider",cme); bpy.context.scene.collection.objects.link(col)
for _ in range(4):
    if len(col.data.polygons)<=160*1.05: break
    m=col.modifiers.new("d",'DECIMATE'); m.ratio=160/len(col.data.polygons); m.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=col; bpy.ops.object.modifier_apply(modifier=m.name)
print("collider",len(col.data.polygons))
for c in (obj,col): c.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); col.select_set(True); bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                         mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
