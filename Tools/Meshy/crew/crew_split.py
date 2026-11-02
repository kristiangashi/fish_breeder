"""Step 1 of 3: splits the Meshy crew lineup FBX into one mesh per character, named, scaled
(OwnerMan = 1.75 m) and standing on its feet. Writes split.blend into the work folder.
Usage: python3 crew_split.py -- <lineup.fbx>"""
import bpy, bmesh, sys, mathutils
import os
# Intermediate files go in CREW_WORK (default: the current folder).
WORK=os.environ.get('CREW_WORK', os.getcwd())
F=sys.argv[sys.argv.index('--')+1]
OUT=os.path.join(WORK, 'split.blend')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=F)
src=[o for o in bpy.data.objects if o.type=='MESH'][0]
# apply transforms so vertex coords are world
bpy.context.view_layer.objects.active=src; src.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
parts=[o for o in bpy.data.objects if o.type=='MESH']
def bbox(o):
    xs=[v.co.x for v in o.data.vertices]; ys=[v.co.y for v in o.data.vertices]; zs=[v.co.z for v in o.data.vertices]
    return min(xs),max(xs),min(ys),max(ys),min(zs),max(zs)
# split fused part: the widest one
widest=max(parts,key=lambda o:(bbox(o)[1]-bbox(o)[0]))
b=bbox(widest); print('widest',widest.name,b)
V=widest.data.vertices
def crossings(x): return sum(1 for e in widest.data.edges if (V[e.vertices[0]].co.x-x)*(V[e.vertices[1]].co.x-x)<0)
cands=[b[0]+(b[1]-b[0])*(0.4+0.2*k/80) for k in range(81)]
cc=[(crossings(x),x) for x in cands]; i,split=min(cc); i=split
print('split at',split,min(cc)[0])
bm=bmesh.new(); bm.from_mesh(widest.data)
for f in bm.faces: f.select = f.calc_center_median().x > split
right=[f for f in bm.faces if f.select]
# move selected faces to new object by duplicating mesh then deleting complementary faces
me2=widest.data.copy(); o2=widest.copy(); o2.data=me2; bpy.context.collection.objects.link(o2)
bm2=bmesh.new(); bm2.from_mesh(me2)
bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if f.calc_center_median().x <= split], context='FACES'); bm2.to_mesh(me2)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().x > split], context='FACES'); bm.to_mesh(widest.data)
parts=[o for o in bpy.data.objects if o.type=='MESH']
# small parts join nearest big
big=[o for o in parts if len(o.data.vertices)>1000]; small=[o for o in parts if len(o.data.vertices)<=1000]
def ctr(o):
    b=bbox(o); return mathutils.Vector(((b[0]+b[1])/2,(b[2]+b[3])/2,(b[4]+b[5])/2))
for s in small:
    tgt=min(big,key=lambda o:max(0,abs(ctr(s).x-ctr(o).x)-(bbox(o)[1]-bbox(o)[0])/2)+max(0,abs(ctr(s).z-ctr(o).z)-(bbox(o)[5]-bbox(o)[4])/2))
    print('join',s.name,len(s.data.vertices),'->',tgt.name)
    bpy.ops.object.select_all(action='DESELECT'); s.select_set(True); tgt.select_set(True); bpy.context.view_layer.objects.active=tgt; bpy.ops.object.join()
big=[o for o in bpy.data.objects if o.type=='MESH']
top=sorted([o for o in big if ctr(o).z>0],key=lambda o:ctr(o).x); bot=sorted([o for o in big if ctr(o).z<=0],key=lambda o:ctr(o).x)
# The Meshy lineup's third figure became a customer and the Hawaiian-shirt tourist the fish seller.
names=['OwnerWoman','OwnerMan','CustomerTrader']+['CustomerSunhat','CustomerKid','FishSeller','CustomerRocker']
for o,n in zip(top+bot,names): o.name=n; o.data.name=n
man=[o for o in big if o.name=='OwnerMan'][0]; bm_=bbox(man); scale=1.75/(bm_[5]-bm_[4]); print('scale',scale)
for o in big:
    b=bbox(o)
    # feet centre: centre of vertices in the lowest 5% of height
    h=b[5]-b[4]; low=[v.co for v in o.data.vertices if v.co.z<b[4]+h*0.05]
    fx=sum(c.x for c in low)/len(low); fy=sum(c.y for c in low)/len(low)
    off=mathutils.Vector((-fx,-fy,-b[4]))
    for v in o.data.vertices: v.co=(v.co+off)*scale
    o.data.update()
    print(o.name,len(o.data.polygons),'height',round(h*scale,3))
bpy.ops.wm.save_as_mainfile(filepath=OUT)
