# Folds the stray knob poking out of the top of the Arctic grayling's mouth (a Meshy mesh error) back
# into its snout: the knob's vertices merge into one point level with the snout around it, the patch
# takes the median grey of its rim and smooth normals. Run with Blender:
#   blender -b --factory-startup --python fix_grayling_snout.py -- <ArcticGrayling.fbx> <out .fbx> <ArcticGrayling_Albedo.png>
# Only the Body part changes; the fins are exported as they came.
import bpy, sys, bmesh
import numpy as np
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]
src, dst, albedo = args[0], args[1], args[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o=bpy.data.objects["Body"]; M=o.matrix_world; Mi=M.inverted()
bm=bmesh.new(); bm.from_mesh(o.data)
knob=[v for v in bm.verts if (M@v.co).x>0.387 and (M@v.co).z>0.029]
ks=set(knob)
ring=set(n for v in knob for e in v.link_edges for n in [e.other_vert(v)] if n not in ks)
print("knob",len(knob),"ring",len(ring))
cy=sum((M@v.co).y for v in knob)/len(knob); cz=sum((M@v.co).z for v in knob)/len(knob)
cx=sum((M@v.co).x for v in ring)/len(ring)
target=Mi@Vector((cx,cy,cz))
print("merge to world",round(cx,4),round(cy,4),round(cz,4))
bmesh.ops.pointmerge(bm, verts=knob, merge_co=target)
bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
mvert=[v for v in bm.verts if (v.co-target).length<1e-6][0]
mv=mvert.index
uvl=bm.loops.layers.uv.active
fan=set(mvert.link_faces)
outer={}
for f in bm.faces:
    if f in fan: continue
    for l in f.loops:
        if any(g in fan for g in l.vert.link_faces): outer.setdefault(l.vert,l[uvl].uv.copy())
# Rim UVs sit on different texture islands, so the whole patch takes one rim colour: the median grey.
img=bpy.data.images.load(albedo); iw,ih=img.size
px=np.empty(iw*ih*4,dtype=np.float32); img.pixels.foreach_get(px); px=px.reshape(ih,iw,4)
def col(uv): return px[int((uv.y%1)*(ih-1)),int((uv.x%1)*(iw-1)),:3]
ranked=sorted(outer.values(), key=lambda uv: float(col(uv).mean()))
pick=ranked[len(ranked)//2]
print("rim greys",[round(float(col(u).mean()),3) for u in ranked],"pick",round(float(col(pick).mean()),3))
for f in fan:
    for l in f.loops: l[uvl].uv=pick.copy()
bm.to_mesh(o.data); bm.free()
m=o.data
cn=[c.vector.copy() for c in m.corner_normals]
fan=[p for p in m.polygons if mv in p.vertices]
avg=sum((p.normal for p in fan),Vector()).normalized()
print("fan",len(fan),"avg normal",avg)
for p in fan:
    for li in p.loop_indices:
        if m.loops[li].vertex_index==mv: cn[li]=avg
        else: cn[li]=(cn[li]+p.normal*0.5).normalized()
m.normals_split_custom_set(cn)
bpy.ops.object.select_all(action='DESELECT')
for ob in bpy.context.scene.objects:
    if ob.type=='MESH': ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, mesh_smooth_type='FACE', path_mode='STRIP',
                         embed_textures=False, add_leaf_bones=False, bake_anim=False)
