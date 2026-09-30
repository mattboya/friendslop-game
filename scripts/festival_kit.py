KIT_CONFIG={"tol":.004,"uv":.5,"res":(1280,720),"bg":(.42,.45,.50),"ground":(.30,.33,.30),
"pal":{"Dark":(.09,.10,.17),"Wood":(.39,.25,.18),"Mint":(.16,.78,.64),"Rose":(.93,.26,.47),"Gold":(.98,.68,.21),"Cream":(.91,.84,.65),"Blue":(.23,.42,.73),"Metal":(.37,.41,.47),"Glass":(.30,.73,.79),"White":(.86,.90,.82),"Rubber":(.10,.12,.16),"Leaf":(.22,.43,.28),"LeafWarm":(.38,.48,.27),"Bark":(.44,.31,.24),"Stone":(.43,.46,.42),"Needle":(.16,.31,.26),
"PaintRose":(.65,.21,.34),"PaintMint":(.21,.56,.51),"PaintGold":(.72,.48,.22),"PaintCream":(.82,.77,.62),"AutoGlass":(.18,.30,.38),"CanvasRose":(.93,.26,.47),"CanvasGold":(.98,.68,.21),"CanvasMint":(.16,.78,.64),"CanvasCream":(.91,.84,.65),"CanvasDark":(.13,.20,.24),
"StageGlowGold":(1,.64,.28),"StageGlowMint":(.28,.92,.75),"StageGlowRose":(1,.30,.55)}}
import bpy,bmesh,math,json,os
from mathutils import Vector as V,Matrix as M
from mathutils.bvhtree import BVHTree as BT
C=KIT_CONFIG;RZ=M.Rotation(math.pi,4,"Z");I=M.Identity(4);R0=os.getcwd();PARTS={};COL={};MAT={};KIT=[None]
def A(x,y,z):return V((x,z,y))
def U(v):return V((-v.x,v.z,-v.y))
def ad(p,d,t=1):return tuple(p[i]+d[i]*t for i in range(3))
def rot(deg,axis):return M.Rotation(math.radians(deg),4,{"x":"X","y":"Z","z":"Y"}[axis])
def setup(names):
    bpy.context.preferences.filepaths.save_version=0
    for o in [o for o in bpy.data.objects if o.get("kit") in names or o.get("temp")]:bpy.data.objects.remove(o,do_unlink=True)
    for c in [c for c in bpy.data.collections if c.name in names]:bpy.data.collections.remove(c)
    for d in (bpy.data.meshes,bpy.data.curves,bpy.data.cameras):
        for x in [x for x in d if x.users==0]:d.remove(x)
    for k,c in C["pal"].items():
        m=bpy.data.materials.get("FK_"+k) or bpy.data.materials.new("FK_"+k);m.diffuse_color=(*c,1);MAT[k]=m
    sc=bpy.context.scene;sc.render.engine="BLENDER_WORKBENCH";sc.render.resolution_x,sc.render.resolution_y=C["res"]
    sh=sc.display.shading;sh.light="STUDIO";sh.color_type="MATERIAL";sh.show_object_outline=True;sh.show_cavity=True
    if sc.world is None:sc.world=bpy.data.worlds.new("FK_World")
    sc.world.color=C["bg"]
def kit(k,shell=None):
    KIT[0]=k;PARTS[k]={};COL[k]=bpy.data.collections.new(k);bpy.context.scene.collection.children.link(COL[k])
    for n,m,c,s,cut in (shell if shell is not None else [("Ground","Stone",(0,-.05,0),(8,.1,8),0)]):box(n,m,c,s,tag="shell",cut=bool(cut))
def ob(n,m,bm,piv=None,smooth=False,tag="",cut=False):
    bmesh.ops.transform(bm,matrix=RZ,verts=bm.verts[:])
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    uv=bm.loops.layers.uv.new("UV")
    for f in bm.faces:
        a=max(range(3),key=lambda i:abs(f.normal[i]));u,v=[i for i in range(3) if i!=a]
        for l in f.loops:l[uv].uv=(l.vert.co[u]*C["uv"],l.vert.co[v]*C["uv"])
        f.smooth=smooth
    me=bpy.data.meshes.new(n);bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(n,me);me.materials.append(MAT[m]);o["kit"]=KIT[0];o["mat"]=m;o["tag"]=tag;o["cut"]=cut
    COL[KIT[0]].objects.link(o)
    if piv is not None:
        p=RZ@A(*piv);me.transform(M.Translation(-p));o.location=p
    PARTS[KIT[0]][n]=o;return o
def box(n,m,c,s,ch=0,R=None,T=I,**k):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1);bmesh.ops.scale(bm,vec=A(*s),verts=bm.verts[:])
    if ch:bmesh.ops.bevel(bm,geom=bm.verts[:]+bm.edges[:],offset=ch,segments=1,profile=.5,affect="EDGES",clamp_overlap=True)
    if R is not None:bmesh.ops.transform(bm,matrix=R,verts=bm.verts[:])
    bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c)),verts=bm.verts[:]);return ob(n,m,bm,**k)
def slab(n,m,a,b,w,t,ch=0,T=I,**k):
    d=A(*b)-A(*a);L=d.length;z=d/L;x=V((1,0,0))-z*z.x
    if x.length<.1:x=V((0,1,0))-z*z.y
    x.normalize();y=z.cross(x)
    return box(n,m,[(p+q)/2 for p,q in zip(a,b)],(w,L,t),ch,M((x,y,z)).transposed().to_4x4(),T,**k)
def cyl(n,m,a,b,r,k=8,r2=None,T=I,**kw):
    pa,pb=T@A(*a),T@A(*b);d=pb-pa;bm=bmesh.new()
    bmesh.ops.create_cone(bm,cap_ends=True,segments=k,radius1=r,radius2=r if r2 is None else r2,depth=d.length)
    bmesh.ops.transform(bm,matrix=M.Translation((pa+pb)/2)@d.to_track_quat("Z","Y").to_matrix().to_4x4(),verts=bm.verts[:]);return ob(n,m,bm,**kw)
def ico(n,m,c,r,sub=1,s=(1,1,1),T=I,**kw):
    bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=sub,radius=r);bmesh.ops.scale(bm,vec=A(*s),verts=bm.verts[:])
    bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c)),verts=bm.verts[:]);return ob(n,m,bm,**kw)
def tube(n,m,pts,r,k=6,closed=False,T=I,**kw):
    P=[T@A(*p) for p in pts];N=len(P);bm=bmesh.new();rs=[];u=None
    for i,p in enumerate(P):
        t=(P[(i+1)%N]-P[i-1]) if closed else (P[min(i+1,N-1)]-P[max(i-1,0)]);t.normalize()
        u=(t.orthogonal() if u is None else u-t*u.dot(t)).normalized();w=t.cross(u)
        rs.append([bm.verts.new(p+(u*math.cos(6.2832*j/k)+w*math.sin(6.2832*j/k))*r) for j in range(k)])
    for i in range(N if closed else N-1):
        a,b=rs[i],rs[(i+1)%N]
        for j in range(k):bm.faces.new((a[j],a[(j+1)%k],b[(j+1)%k],b[j]))
    if not closed:bm.faces.new(rs[0][::-1]);bm.faces.new(rs[-1])
    return ob(n,m,bm,**kw)
def loft(n,m,rings,cap=True,closed=True,T=I,**kw):
    bm=bmesh.new();R=[[bm.verts.new(T@A(*p)) for p in r] for r in rings];k=len(R[0])
    for a,b in zip(R,R[1:]):
        for j in range(k if closed else k-1):bm.faces.new((a[j],a[(j+1)%k],b[(j+1)%k],b[j]))
    if cap:bm.faces.new(R[0][::-1]);bm.faces.new(R[-1])
    return ob(n,m,bm,**kw)
def lathe(n,m,prof,c=(0,0,0),k=12,T=I,**kw):
    return loft(n,m,[[(c[0]+r*math.cos(6.2832*j/k),c[1]+y,c[2]+r*math.sin(6.2832*j/k)) for j in range(k)] for r,y in prof],T=T,**kw)
def prism(n,m,prof,x0,x1,T=I,**kw):return loft(n,m,[[(x,y,z) for z,y in prof] for x in (x0,x1)],T=T,**kw)
def puff(n,m,c,s,f=1.35,T=I,**kw):
    bm=bmesh.new();bmesh.ops.create_cube(bm,size=1);bmesh.ops.subdivide_edges(bm,edges=bm.edges[:],cuts=1,use_grid_fill=True)
    for v in bm.verts:v.co.z*=f*(1-.9*abs(v.co.x))*(1-.9*abs(v.co.y))
    bmesh.ops.scale(bm,vec=A(*s),verts=bm.verts[:]);bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c)),verts=bm.verts[:]);return ob(n,m,bm,smooth=True,**kw)
def label(n,m,text,c,size=.1,depth=.004,R=None,T=I,**kw):
    cu=bpy.data.curves.new(n,"FONT");cu.body=text;cu.size=size;cu.extrude=depth/2;cu.align_x="CENTER";cu.align_y="CENTER"
    tmp=bpy.data.objects.new(n,cu);bpy.context.scene.collection.objects.link(tmp);bpy.context.view_layer.update()
    me=bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()));bpy.data.objects.remove(tmp);bpy.data.curves.remove(cu)
    bm=bmesh.new();bm.from_mesh(me);bpy.data.meshes.remove(me);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-5)
    bmesh.ops.transform(bm,matrix=T@M.Translation(A(*c))@(R if R is not None else M.Rotation(math.pi/2,4,"X")),verts=bm.verts[:]);return ob(n,m,bm,**kw)
def parent(ch,pa):
    bpy.context.view_layer.update();mw=ch.matrix_world.copy();ch.parent=pa;ch.matrix_world=mw
def bb(o):
    p=[U(o.matrix_world@v.co) for v in o.data.vertices];return V([min(q[i] for q in p) for i in range(3)]),V([max(q[i] for q in p) for i in range(3)])
def kbb(k):
    b=[bb(o) for o in PARTS[k].values() if o["tag"]!="shell"];return V([min(x[0][i] for x in b) for i in range(3)]),V([max(x[1][i] for x in b) for i in range(3)])
def audit(k,clear=()):
    bpy.context.view_layer.update();P=PARTS[k];B={n:bb(o) for n,o in P.items()};D={}
    for n,o in P.items():
        vs=[o.matrix_world@v.co for v in o.data.vertices];D[n]=(BT.FromPolygons(vs,[p.vertices[:] for p in o.data.polygons]),vs)
    def near(a,b):
        if any(B[a][0][i]>B[b][1][i]+C["tol"] or B[b][0][i]>B[a][1][i]+C["tol"] for i in range(3)):return False
        (ta,va),(tb,vb)=D[a],D[b]
        return bool(ta.overlap(tb)) or min(tb.find_nearest(v)[3] for v in va)<=C["tol"] or min(ta.find_nearest(v)[3] for v in vb)<=C["tol"]
    seen={n for n,o in P.items() if o["tag"]=="shell"};todo=list(seen)
    while todo:
        a=todo.pop()
        for b in P:
            if b not in seen and near(a,b):seen.add(b);todo.append(b)
    hit=[]
    for lo,hi in clear:
        lo,hi=V(lo),V(hi);dt=BT.FromPolygons([RZ@A(x,y,z) for x in (lo.x,hi.x) for y in (lo.y,hi.y) for z in (lo.z,hi.z)],((0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)))
        hit+=[n for n in P if P[n]["tag"]!="shell" and (dt.overlap(D[n][0]) or any(all(lo[i]<U(v)[i]<hi[i] for i in range(3)) for v in D[n][1]))]
    return sorted(set(P)-seen),sorted(set(hit))
def camera(eye,at,lens=35,ortho=None):
    cam=bpy.data.objects.new("ReviewCam",bpy.data.cameras.new("ReviewCam"));cam["temp"]=1;bpy.context.scene.collection.objects.link(cam)
    e,t=RZ@A(*eye),RZ@A(*at);cam.location=e;cam.rotation_euler=(t-e).to_track_quat("-Z","Y").to_euler();cam.data.lens=lens;cam.data.clip_end=500
    if ortho:cam.data.type="ORTHO";cam.data.ortho_scale=ortho
    return cam
def render(path,cam,show):
    for o in bpy.data.objects:
        if o.type in("MESH","FONT"):o.hide_render=not show(o)
    sc=bpy.context.scene;sc.camera=cam;os.makedirs(os.path.dirname(os.path.join(R0,path)),exist_ok=True)
    sc.render.filepath=os.path.join(R0,path);bpy.ops.render.render(write_still=True);bpy.data.objects.remove(cam,do_unlink=True)
def shot(k,path,eye,at,lens=35,cut=False,ortho=None):
    render(path,camera(eye,at,lens,ortho),lambda o:o.get("kit")==k and not (cut and o.get("cut")))
def gallery(ks,path,cols=4,gap=1.0,view=(-.55,.62,-1.0),names=None):
    bpy.context.view_layer.update();bs=[kbb(k) for k in ks];cell=max(max(hi.x-lo.x,hi.z-lo.z) for lo,hi in bs)+gap;tall=max(hi.y-min(lo.y,0) for lo,hi in bs);moved=[];temp=[]
    rows=(len(ks)+cols-1)//cols;nc=min(cols,len(ks))
    for i,k in enumerate(ks):
        lo,hi=bs[i];cx,cz=(i%cols)*cell,-(i//cols)*cell;off=RZ@A(cx-(lo.x+hi.x)/2,max(0,-lo.y),cz-(lo.z+hi.z)/2)
        for o in COL[k].objects:
            if o.parent is None:o.location+=off;moved.append((o,off))
        KIT[0]=k;t=label("Label"+str(i),"White",(names or {}).get(k,k.replace("Festival","")),(cx,.02,cz-cell*.47),size=min(.32,cell*.075),R=M.Identity(4));t["temp"]=1;temp.append(t)
        del PARTS[k]["Label"+str(i)]
    W,D=nc*cell,rows*cell;ctr=((nc-1)*cell/2,0,-(rows-1)*cell/2)
    g=box("GalleryGround","Stone",(ctr[0],-.03,ctr[2]),(W+cell,.06,D+cell));g["temp"]=1;g.data.materials[0]=bpy.data.materials.get("FK_Ground") or bpy.data.materials.new("FK_Ground");g.data.materials[0].diffuse_color=(*C["ground"],1);del PARTS[KIT[0]]["GalleryGround"];temp.append(g)
    v=V(view).normalized();S=max((W+D*.45)*1.1,(D*.5+tall*.9)*1.9);at=ad(ctr,(0,1,0),tall*.3)
    render(path,camera(ad(at,v,S*3),at,ortho=S),lambda o:o.get("temp") or (o.get("kit") in ks and o.get("tag")!="shell"))
    for o,off in moved:o.location-=off
    for o in temp:bpy.data.objects.remove(o,do_unlink=True)
def merge(k):
    G={}
    for n,o in list(PARTS[k].items()):
        if o["tag"]=="shell":bpy.data.objects.remove(o,do_unlink=True)
        elif o["tag"]!="keep":G.setdefault(o["mat"],[]).append(o)
    for m,os_ in G.items():
        bm=bmesh.new();tri=0
        for o in os_:
            me=o.data.copy();me.transform(o.matrix_world);tri+=sum(len(p.vertices)-2 for p in me.polygons);bm.from_mesh(me);bpy.data.meshes.remove(me);bpy.data.objects.remove(o,do_unlink=True)
        nm=k.replace("Festival","")+" "+m+"__"+m;me=bpy.data.meshes.new(nm);bm.to_mesh(me);bm.free()
        assert sum(len(p.vertices)-2 for p in me.polygons)==tri,nm
        o=bpy.data.objects.new(nm,me);me.materials.append(MAT[m]);o["kit"]=k;o["tag"]="";COL[k].objects.link(o)
    bpy.context.view_layer.update()
def export(k,path):
    os_=list(COL[k].objects);os.makedirs(os.path.dirname(os.path.join(R0,path)),exist_ok=True)
    for o in bpy.context.view_layer.objects:o.select_set(o in os_)
    bpy.context.view_layer.objects.active=os_[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(R0,path),use_selection=True,object_types={"MESH"},bake_anim=False,axis_forward="-Z",axis_up="Y",apply_unit_scale=True,add_leaf_bones=False)
    return {"renderers":len(os_),"triangles":sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in os_),"separate":sorted(o.name for o in os_ if o.get("tag")=="keep")}
def finish(ks,fbx,src,man,clear=None,extra=None):
    res={k:dict(zip(("floating","blocking"),audit(k,(clear or {}).get(k,())))) for k in ks}
    bad={k:v for k,v in res.items() if v["floating"] or v["blocking"]}
    assert not bad,bad
    for k in ks:merge(k);res[k].update(export(k,os.path.join(fbx,k+".fbx")))
    for k,v in (extra or {}).items():res[k].update(v)
    os.makedirs(os.path.dirname(os.path.join(R0,src)),exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=os.path.join(R0,src))
    old=json.load(open(os.path.join(R0,man))) if os.path.exists(os.path.join(R0,man)) else {}
    models={**old.get("models",{}),**res}
    open(os.path.join(R0,man),"w").write(json.dumps({"source":"Original scripted Blender geometry; no external assets","runtimeDirectory":fbx,"models":models},indent=2)+"\n")
    return res
