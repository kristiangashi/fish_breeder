"""Step 2 of 3: finds each character's joints on split.blend, cuts the mesh to a triangle budget
(default 7000), builds a simple skeleton and skins it. Writes rigged.blend.
Usage: python3 crew_rig.py -- [triangles]"""
import bpy, bmesh, sys, math, statistics as st
import os
# Intermediate files go in CREW_WORK (default: the current folder).
WORK=os.environ.get('CREW_WORK', os.getcwd())
from mathutils import Vector, Matrix
argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
TARGET=int(argv[0]) if argv else 7000
bpy.ops.wm.open_mainfile(filepath=os.path.join(WORK, 'split.blend'))
chars=[o for o in bpy.data.objects if o.type=='MESH']
med=lambda a: st.median(a) if a else 0.0
report={}
for o in chars:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    V=[v.co.copy() for v in o.data.vertices]
    H=max(c.z for c in V)
    J={}
    for side,sg in (('L',1),('R',-1)):
        S=[c for c in V if c.x*sg>0]
        span=max(c.x*sg for c in S)
        arm=[c for c in S if 0.7*span<c.x*sg<0.9*span]
        armZ=med([c.z for c in arm])
        sl=[c.z for c in S if abs(c.x*sg-0.8*span)<0.01]
        t=(max(sl)-min(sl)) if sl else 0.05*H
        edge=0.15*H
        x=0.8*span
        while x>0.05*H:
            n=sum(1 for c in S if abs(c.x*sg-x)<0.004 and armZ-0.15*H<c.z<armZ-1.6*t)
            if n>3: edge=x; break
            x-=0.004
        sx=edge-0.5*t
        A=span-sx
        def at(xx):
            s=[c for c in S if abs(c.x*sg-xx)<0.01 and abs(c.z-armZ)<2*t]
            return Vector((xx*sg, med([c.y for c in s]), med([c.z for c in s]) if s else armZ))
        J['UpperArm.'+side]=at(sx); J['LowerArm.'+side]=at(sx+0.42*A); J['Hand.'+side]=at(sx+0.76*A); J['HandEnd.'+side]=at(span-0.01)
        J['_t'+side]=t; J['_armZ'+side]=armZ
    armZ=(J['_armZL']+J['_armZR'])/2
    from mathutils.bvhtree import BVHTree
    tree=BVHTree.FromPolygons(V,[p.vertices[:] for p in o.data.polygons])
    def solid(x,z): return tree.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))[0] is not None
    crotch=None
    z=0.55*H
    while z>0.12*H:
        if not (solid(0,z) or solid(0.006*H,z) or solid(-0.006*H,z)):
            crotch=z; break
        z-=0.003*H
    crotch=max(0.34*H,min(crotch or 0.42*H,0.5*H))
    hipZ=crotch+0.04*H
    def mid(z,w=0.1):
        s=[c for c in V if abs(c.z-z)<0.01*H and abs(c.x)<w*H]
        return Vector((0,med([c.y for c in s]),z))
    for side,sg in (('L',1),('R',-1)):
        legs=[c for c in V if c.x*sg>0.01*H and 0.1*H<c.z<0.2*H]
        lx=med([c.x for c in legs]); ly=med([c.y for c in legs])
        low=[c for c in V if c.x*sg>0 and c.z<0.04*H]
        toeY=min(c.y for c in low)
        J['UpperLeg.'+side]=Vector((lx,ly,hipZ-0.02*H)); J['LowerLeg.'+side]=Vector((lx,ly,0.05*H+(hipZ-0.07*H)*0.5)); J['Foot.'+side]=Vector((lx,ly+0.01*H,0.06*H)); J['FootEnd.'+side]=Vector((lx,toeY+0.02*H,0.015*H))
    chestZ=armZ-0.1*H; neckZ=armZ+0.025*H; headZ=neckZ+0.05*H
    J['Hips']=mid(hipZ); J['Spine']=mid(hipZ+0.1*H); J['Chest']=mid(chestZ); J['Neck']=mid(neckZ,0.05); J['Head']=mid(headZ,0.06); J['HeadEnd']=Vector((0,J['Head'].y,H))
    m=o.modifiers.new('dec','DECIMATE'); m.ratio=min(1.0,TARGET/len(o.data.polygons)); m.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    report[o.name]=dict(H=round(H,3),crotch=round(crotch/H,3),armZ=round(armZ/H,3),shoulder=round(J['UpperArm.L'].x,3),tris=len(o.data.polygons))
    # armature
    ad=bpy.data.armatures.new(o.name+'_Rig'); arm=bpy.data.objects.new(o.name+'_Rig',ad); bpy.context.collection.objects.link(arm)
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb=ad.edit_bones
    def bone(name,head,tail,parent=None,connect=False):
        b=eb.new(name); b.head=head; b.tail=tail
        if parent: b.parent=eb[parent]; b.use_connect=connect
        return b
    bone('Hips',J['Hips'],J['Spine'])
    bone('Spine',J['Spine'],J['Chest'],'Hips',True)
    bone('Chest',J['Chest'],J['Neck'],'Spine',True)
    bone('Neck',J['Neck'],J['Head'],'Chest',True)
    bone('Head',J['Head'],J['HeadEnd'],'Neck',True)
    for s in 'LR':
        bone('UpperArm.'+s,J['UpperArm.'+s],J['LowerArm.'+s],'Chest')
        bone('LowerArm.'+s,J['LowerArm.'+s],J['Hand.'+s],'UpperArm.'+s,True)
        bone('Hand.'+s,J['Hand.'+s],J['HandEnd.'+s],'LowerArm.'+s,True)
        bone('UpperLeg.'+s,J['UpperLeg.'+s],J['LowerLeg.'+s],'Hips')
        bone('LowerLeg.'+s,J['LowerLeg.'+s],J['Foot.'+s],'UpperLeg.'+s,True)
        bone('Foot.'+s,J['Foot.'+s],J['FootEnd.'+s],'LowerLeg.'+s,True)
    for b in eb: b.roll=0
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
    r=bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    empty=[g.name for g in o.vertex_groups if not any(g.index in [x.group for x in v.groups] for v in o.data.vertices[:])]
    unweighted=sum(1 for v in o.data.vertices if not v.groups or sum(x.weight for x in v.groups)<1e-4)
    report[o.name]['unweighted']=unweighted
print(report)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, 'rigged.blend'))
