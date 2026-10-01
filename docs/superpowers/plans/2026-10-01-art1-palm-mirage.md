# ART-1 Palm Mirage Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Palm Mirage gets the full polo-field parody look and the waves' placeholders get real models. Play does not change, and Ember Playa looks the same as before.

**Architecture:** One new kit generator, `scripts/generate_polo_dressing.py`, builds every new model with `scripts/festival_kit.py`. It exports FBX into `Assets/Festival/Art/Resources`, saves `ArtSource/PoloDressing.blend` and the manifest `ArtSource/polo-dressing-manifest.json`, and writes review renders to `artifacts/polo-dressing/`. A new C# class, `FestivalPoloDressing`, places the palms, landmarks, canopy, lawn and horizon. `FestivalWorld` creates it and shows it only at Palm Mirage. The wheel, VIP board, Giggle Tank and creatures swap their primitives for the new models where they are built today. Phase A (Tasks 1–8) is Blender and Python only and runs now. Phase B (Tasks 9–14) is the C# work and starts only after wave 2026-10-01 has landed.

**Tech Stack:** Blender 5.2.2 (`/Applications/Blender.app/Contents/MacOS/Blender`, headless), Python bpy/bmesh through `festival_kit`, Unity 6000.3.24f1 (URP), NUnit EditMode tests, `node scripts/unity.mjs`.

**Spec:** `docs/superpowers/specs/2026-10-01-art1-palm-mirage-design.md`

## Global Constraints

- Work only in `/Users/mboyajian/Code/friendslop-game` on branch `art/festival-art`. Never read from, write to or run anything in `.worktrees/`.
- No gameplay change. Core and Network are untouched, `FestivalSession.ProtocolVersion` keeps its value, and no collider is added or moved. Every new model is visual only.
- Ember Playa looks exactly as it does today. All Palm Mirage dressing is shown only when `FestivalIndex == Festivals.PoloFestival`. The pre-level camp is out of scope.
- Copy: the tin reads `STAMPS`, the stall banner reads `GOOD TIMES SUPPLY`, and the stage sign reads `PALM MIRAGE`. No real brands, people or artworks. The astronaut is an original chunky design.
- Kit coordinates are Unity local coordinates: x right, y up, z forward, in metres. A model's pivot is its base centre at y = 0 unless the task says otherwise.
- Every exported model has **at least two materials** (two merged renderers). Unity collapses a single-mesh FBX onto its root, the renderer then loses its `__Colour` suffix, and `FestivalArtView` falls back to `Dark`.
- Triangle budgets (read from the manifest after each run):
  - each palm ≤ 2,000;
  - wheel (base + rotor + 8 gondolas) ≤ 6,000;
  - stage dressing ≤ 6,000;
  - astronaut ≤ 8,000;
  - tower ≤ 6,000;
  - canopy ≤ 4,000;
  - all ridges together ≤ 6,000;
  - each creature ≤ 1,200;
  - Giggle Tank ≤ 1,000;
  - balloon ≤ 300;
  - VIP board ≤ 500.
- New palette keys, with identical values in both places (`festival_kit.py` `KIT_CONFIG["pal"]` and `FestivalArtView.colors`): `Frond (.24,.55,.30)`, `PalmBark (.55,.42,.30)`, `Sand (.80,.62,.42)`.
- Blender runs headless and always passes `--python-exit-code 1`. Its output is redirected to a log and `echo "exit=$?"` follows immediately. Never pipe a gate into `tail`/`head` and trust `$?`.
- The generator never resets the scene. A clean start comes from the command line. Every task uses this command, with `<KITS>` replaced:

```bash
cd /Users/mboyajian/Code/friendslop-game && mkdir -p artifacts/polo-dressing
B=/Applications/Blender.app/Contents/MacOS/Blender
if [ -f ArtSource/PoloDressing.blend ]; then OPEN=(ArtSource/PoloDressing.blend); else OPEN=(--factory-startup --python-expr "import bpy;bpy.ops.wm.read_factory_settings(use_empty=True)"); fi
"$B" -b "${OPEN[@]}" --python-exit-code 1 --python scripts/generate_polo_dressing.py -- <KITS> > artifacts/polo-dressing/run.log 2>&1; echo "exit=$?"; grep -E "BUDGET|Error|Traceback|AssertionError" artifacts/polo-dressing/run.log
```

- Check one model with the blender-python skill's checker (sizes, scale 1, no `.001` duplicates, FBX re-import):

```bash
"$B" -b ArtSource/PoloDressing.blend --python-exit-code 1 --python ~/.claude/skills/blender-python/check.py -- <KIT> artifacts/polo-dressing/check Assets/Festival/Art/Resources/<KIT>.fbx > artifacts/polo-dressing/check.log 2>&1; echo "exit=$?"; grep AUDIT artifacts/polo-dressing/check.log
```

  `check.py` reports `dims` and `min` in Blender axes (x, depth, height), so a model's height is the third number.
- `artifacts/` is gitignored, so review renders stay local and are never committed.

## Review Focus

1. **A model loses its colours in Unity.** Either it was exported with one material and collapsed onto its root, or it uses a palette key that `FestivalArtView` doesn't know (`Frond`, `PalmBark`, `Sand`). The art shows up dark or white. A person expects every new model to show its palette colours. Pinned in Task 10: every renderer of every new resource ends in a known `__Key` and its material is not the white fallback.
2. **The festival switches back and forth** (Palm Mirage → Ember Playa → Palm Mirage, the way an encore or a new weekend moves through festivals). The trees stay hidden, or the dressing stays visible on the playa. Pinned in Task 10: show/hide in both directions, twice.
3. **A palm stands inside a landmark,** for example the outer-ring spot (48, 10.5) inside the astronaut's footprint. Pinned in Task 10: no palm within a landmark's clearance radius, and every landmark's bounds lie outside the ±40 m play square.
4. **The view stands at a corner of the map.** The horizon ridges fall outside the camera's 130 m far clip and vanish on one side. Pinned in Task 10: with the view at (±40, ±40), every ridge is within 125 m of the view.
5. **The canopy hangs into the crowd's headroom,** or a mast leaves the speaker stack. Pinned in Task 10: every non-mast canopy renderer is at y ≥ 6, and the masts stay inside the speaker proxies below 4 m.

---

## Phase A — models (runs now, alongside the wave)

### Task 1: Generator, palette keys and palms

**Files:**
- Create: `scripts/generate_polo_dressing.py`
- Modify: `scripts/festival_kit.py:2` (palette)
- Outputs: `Assets/Festival/Art/Resources/FestivalPalmTall.fbx`, `FestivalPalmLean.fbx`, `ArtSource/PoloDressing.blend`, `ArtSource/polo-dressing-manifest.json`

**Interfaces:**
- Produces: the generator's command line (`-- <kit names>`), the dicts `BUILD` (kit name → builder taking `k`), `CLEAR` (kit name → function returning a clear-zone list) and `REVIEW` (kit name → function that renders extra review shots). Later tasks add entries to these three dicts. Resource names: `FestivalPalmTall`, `FestivalPalmLean`.

- [ ] **Step 1: Add the palette keys to the kit**

In `scripts/festival_kit.py`, line 2 ends with `"Stone":(.43,.46,.42),"Needle":(.16,.31,.26),`. Insert the new keys right after `"Needle":(.16,.31,.26),` so that line ends:

```python
"Stone":(.43,.46,.42),"Needle":(.16,.31,.26),"Frond":(.24,.55,.30),"PalmBark":(.55,.42,.30),"Sand":(.80,.62,.42),
```

- [ ] **Step 2: Write the generator with the palm builder**

Create `scripts/generate_polo_dressing.py`:

```python
CONFIG={"out":"Assets/Festival/Art/Resources","src":"ArtSource/PoloDressing.blend","man":"ArtSource/polo-dressing-manifest.json","rev":"artifacts/polo-dressing",
"palm":{"tall":(10.2,0,.27,.17,11),"lean":(9.0,2.2,.26,.16,10),"fronds":9,"frond":((1.2,.36,.62),(1.3,-.15,.5),(1.1,-.85,.3)),"crown":.42,"nut":(.22,-.2,.15)}}
import sys,os;sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from festival_kit import *
G=CONFIG
def palm(k,p):
    H,lean,r0,r1,n=p;P=G["palm"];kit(k)
    pts=[(lean*(i/n)**2,H*i/n,0) for i in range(n+1)]
    for i in range(n):
        r=r0+(r1-r0)*i/n;cyl("Trunk"+str(i),"PalmBark",pts[i],pts[i+1],r,7,r2=r*.84)
    top=pts[-1];ico("Crown","Frond",top,P["crown"],2,s=(1,.7,1))
    for j in range(P["fronds"]):
        a=6.2832*j/P["fronds"]+.35*(j%2);d=(math.cos(a),0,math.sin(a));p0=ad(top,(0,.1,0))
        for s,(L,dy,w) in enumerate(P["frond"]):
            p1=(p0[0]+d[0]*L,p0[1]+dy,p0[2]+d[2]*L);slab("Frond"+str(j)+"_"+str(s),"Frond" if (j+s)%3 else "LeafWarm",p0,p1,w,.05);p0=p1
    nr,ny,rr=P["nut"]
    for j in range(3):
        a=6.2832*j/3+.5;ico("Nut"+str(j),"PalmBark",(top[0]+nr*math.cos(a),top[1]+ny,top[2]+nr*math.sin(a)),rr)
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"])}
CLEAR={}
REVIEW={}
want=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else list(BUILD)
setup(want)
for k in want:BUILD[k](k)
gallery(want,os.path.join(G["rev"],want[0]+".png"),cols=min(4,len(want)))
for k in want:
    if k in REVIEW:REVIEW[k](k)
for o in [o for o in bpy.data.objects if o.get("ctx")]:bpy.data.objects.remove(o,do_unlink=True)
for m in [m for m in bpy.data.materials if m.users==0 and not m.name.startswith("FK_")]:bpy.data.materials.remove(m)
res=finish(want,G["out"],G["src"],G["man"],clear={k:CLEAR[k]() for k in want if k in CLEAR})
print("BUDGET",{k:v["triangles"] for k,v in res.items()})
```

- [ ] **Step 3: Run it and expect success**

Run the Global Constraints command with `<KITS>` = `FestivalPalmTall FestivalPalmLean`.
Expected: `exit=0` and `BUDGET {'FestivalPalmTall': N, 'FestivalPalmLean': M}`, with each value ≤ 2000.
If `finish` raises `AssertionError` with `floating`, a part doesn't touch its neighbour. Make the overlap bigger (for example, move the nut centre inward with `"nut"`) and re-run.

- [ ] **Step 4: Check and look**

Run `check.py` for `FestivalPalmTall`, then for `FestivalPalmLean`.
Expected: `exit=0`, every object has `scale=(1.0, 1.0, 1.0)`, `dup_suffixes=[]`, height (the third `dims` value) ≈ 10.6 for Tall and ≈ 9.4 for Lean, and the third `min` value is 0.
Open `artifacts/polo-dressing/FestivalPalmTall.png` and the `artifacts/polo-dressing/check/FestivalPalmTall_*.png` renders with Read. Each palm should show a ringed, tapering trunk with a drooping crown of fronds, and Lean should bow toward +x. If a frond pokes into the ground or the crown reads as a ball, tune `"frond"` or `"crown"` in `CONFIG` and re-run Step 3.

- [ ] **Step 5: Commit**

```bash
git add scripts/festival_kit.py scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalPalmTall.fbx Assets/Festival/Art/Resources/FestivalPalmLean.fbx
git commit -m "ART-1: Palm Mirage gets tall and leaning palm models

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Ferris wheel models

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"wheel"` to `CONFIG`, three builders, and their `BUILD` entries)
- Outputs: `FestivalWheelBase.fbx`, `FestivalWheelRotor.fbx`, `FestivalWheelGondola.fbx`

**Interfaces:**
- Consumes: Task 1's generator.
- Produces the models Task 11 depends on:
  - **Base:** pivot at the wheel's rules base. Legs at x ±1.1 and z ±3.2 meet in bearings at (±1.1, 7.5, 0).
  - **Rotor:** pivot at the axle. The rim (radius 6) lies in the local y–z plane, with gondola anchor points at `(0, 6 sin a, 6 cos a)` for a = 0°, 45°, …. The axle spans x ±1.2.
  - **Gondola:** pivot at its body centre, with its hanger reaching up to y = +0.75. The body material is `Rose`; Task 11 recolours it per gondola.

- [ ] **Step 1: Add the wheel config and builders**

Add this key inside `CONFIG` after `"palm":{...}` (put a comma after the palm entry):

```python
"wheel":{"axle":7.5,"r":6,"inner":4.2,"legs":(1.1,3.2),"leg_r":.14,"plat":(3.2,.25,2.4),"rim":16,"spokes":8,"gond":(1.2,.9,1.0),"drop":.75}
```

Add these functions after `palm`:

```python
def wheel_base(k):
    W=G["wheel"];h=W["axle"];lx,lz=W["legs"];px,py,pz=W["plat"];kit(k,[("Ground","Stone",(0,-.05,0),(9,.1,9),0)])
    box("Platform","Wood",(0,py/2,0),(px,py,pz),.03);box("Step","Metal",(0,.06,-pz/2-.25),(1.4,.12,.5),.02)
    for x in (-lx,lx):
        for z in (-lz,lz):cyl("Leg"+str(x)+str(z),"Metal",(x,0,z),(x,h,0),W["leg_r"],6);box("Foot"+str(x)+str(z),"Dark",(x,.06,z),(.5,.12,.5),.02)
        cyl("Brace"+str(x),"Metal",(x,3,-lz*(1-3/h)),(x,3,lz*(1-3/h)),.07,6);box("Bearing"+str(x),"Gold",(x,h,0),(.36,.5,.5),.04)
def rim(r,deg):return (0,r*math.sin(math.radians(deg)),r*math.cos(math.radians(deg)))
def wheel_rotor(k):
    W=G["wheel"];R,Ri,n=W["r"],W["inner"],W["rim"];kit(k,[("Anchor","Stone",(0,0,0),(.05,.05,.05),0)])
    cyl("Axle","Metal",(-1.2,0,0),(1.2,0,0),.18,8);cyl("Hub","Gold",(-.3,0,0),(.3,0,0),.55,10)
    for s in range(n):
        a,b=360*s/n,360*(s+1)/n
        slab("Rim"+str(s),"StageGlowMint" if s%2==0 else "StageGlowRose",rim(R,a),rim(R,b),.18,.18)
        slab("Inner"+str(s),"Gold",rim(Ri,a),rim(Ri,b),.1,.1);ico("Bulb"+str(s),"StageGlowGold",rim(R+.12,a),.13)
    for s in range(W["spokes"]):
        a=360*s/W["spokes"];p=rim(R,a);cyl("Spoke"+str(s),"Metal",rim(.4,a),p,.06,5);cyl("Hanger"+str(s),"Metal",(-.3,p[1],p[2]),(.3,p[1],p[2]),.05,5)
def gondola(k):
    W=G["wheel"];gw,gh,gd=W["gond"];d=W["drop"];b=-gh/2;kit(k,[("Anchor","Stone",(0,d,0),(.06,.06,.06),0)])
    box("Floor","Rose",(0,b+.04,0),(gw,.08,gd),.02)
    for z in (-gd/2,gd/2):box("Wall"+str(z),"Rose",(0,b+.25,z),(gw,.42,.06),.02)
    for x in (-gw/2,gw/2):box("Side"+str(x),"Rose",(x,b+.25,0),(.06,.42,gd),.02)
    box("Seat","Cream",(0,b+.24,gd/2-.18),(gw-.16,.1,.3),.02)
    for x in (-gw/2+.05,gw/2-.05):
        for z in (-gd/2+.05,gd/2-.05):cyl("Post"+str(x)+str(z),"Metal",(x,b+.45,z),(x,gh/2-.08,z),.025,5)
    box("Roof","Cream",(0,gh/2-.04,0),(gw+.12,.08,gd+.12),.02);box("RoofPeak","Rose",(0,gh/2+.06,0),(gw*.6,.12,gd*.6),.02)
    cyl("Hanger","Metal",(0,gh/2+.12,0),(0,d,0),.04,6)
```

Then replace the `BUILD={...}` line with:

```python
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"]),
"FestivalWheelBase":wheel_base,"FestivalWheelRotor":wheel_rotor,"FestivalWheelGondola":gondola}
```

- [ ] **Step 2: Run, check and look**

Run with `<KITS>` = `FestivalWheelBase FestivalWheelRotor FestivalWheelGondola`. Expected: `exit=0`, and base + rotor + 8 × gondola ≤ 6000.
Run `check.py` for each of the three.
- Base: height ≈ 7.75, min height 0.
- Rotor: height ≈ 12.5, with its min height ≈ −6.3 (pivot at the axle, so a negative min is correct here).
- Gondola: height ≈ 1.2, from ≈ −0.45 to +0.78.

Read `artifacts/polo-dressing/FestivalWheelBase.png` and the check renders. Expected: an A-frame on a platform, a two-tone rim with gold bulbs and an inner ring, and a little roofed gondola with a hanger.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalWheel*.fbx
git commit -m "ART-1: Model the Ferris wheel's base, rotor and gondola

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: Stage dressing and petal canopy

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"stage"` and `"canopy"` to `CONFIG`, a stage-context helper, two builders, their `CLEAR` and `REVIEW` entries)
- Outputs: `FestivalStageMirage.fbx`, `FestivalPetalCanopy.fbx`

**Interfaces:**
- Consumes: the existing `Assets/Festival/Art/Resources/FestivalStage.fbx`, imported only as review context and for clear zones.
- Produces: both models have their pivot at the stage origin. Task 10 places both at world (0, 0, 32).
  - **Canopy:** masts at local (±10, ·, −1) with radius 0.25. The masts use `Metal`; every other canopy material is above y = 6.
  - **Dressing:** stays inside x ±11.

- [ ] **Step 1: Add the config, helper and builders**

Add to `CONFIG`:

```python
"stage":{"screen":(10,4.2,-1.0,2.6,3.2),"arch":(8.8,7.2,3.9,4.4,.16,.35),"pylon":(1.0,.5),"sign":(7.2,1.3,"PALM MIRAGE",.85),
"crowd":((-16,0,-14),(16,6,-4.7)),"dj":((-3.2,1.4,-2.4),(3.2,3.7,.2))},
"canopy":{"mast":(10,-1,11.2,.25),"cap":.55,"width":2.8,"thick":.35,"sag":.6,"petals":((-.78,-.62,9,12,"CanvasRose"),(-.35,-.94,10,18,"CanvasGold"),(.25,-.97,7,22,"CanvasMint")),"floor":6.0}
```

Add after `gondola`:

```python
SB=[]
def stage_bounds():
    if SB:return SB[0]
    old=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=os.path.join(R0,G["out"],"FestivalStage.fbx"))
    new=[o for o in bpy.data.objects if o not in old]
    for o in new:o["ctx"]=1;o["temp"]=1
    bpy.context.view_layer.update();P=[U(o.matrix_world@v.co) for o in new if o.type=="MESH" for v in o.data.vertices]
    SB.append((V([min(p[i] for p in P) for i in range(3)]),V([max(p[i] for p in P) for i in range(3)])));return SB[0]
def stage_mirage(k):
    S=G["stage"];sx,sy,sz,sw,sh=S["screen"];ax,ay,az,ah,ar,ad_=S["arch"];pw,pd=S["pylon"];bw,bh,text,ts=S["sign"]
    kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,12),0)])
    for s in (-1,1):
        x=s*sx;cyl("ScreenPost"+str(s),"Dark",(x,0,sz),(x,sy,sz),.2,8)
        box("ScreenFrame"+str(s),"Dark",(x,sy+sh/2,sz),(sw,sh,.25),.04)
        for i,m in enumerate(("StageGlowRose","StageGlowGold","StageGlowMint")):
            box("ScreenBand"+str(s)+str(i),m,(x,sy+.3+.45+i*.95,sz-.145),(sw-.3,.85,.04))
        box("Pylon"+str(s),"Dark",(s*(ax-pw/2+.15),ay/2,az),(pw,ay,pd))
    for i,m in enumerate(("Rose","Gold","Mint")):
        w,h=ax-ad_*i,ah-ad_*i
        tube("Arch"+str(i),m,[(-w*math.cos(math.pi*t/12),ay+h*math.sin(math.pi*t/12),az) for t in range(13)],ar,6)
    top=ay+ah+ar-.06+bh/2
    box("SignTrim","StageGlowGold",(0,top,az+.03),(bw+.2,bh+.2,.16));box("SignBoard","Dark",(0,top,az-.03),(bw,bh,.2))
    label("SignText","Cream",text,(0,top,az-.134),size=ts,depth=.02)
def petal(n,m,root,dx,dz,L,pitch):
    C_=G["canopy"];W,T,sag=C_["width"]/2,C_["thick"]/2,C_["sag"]
    rings=[];ts=(0,.12,.35,.6,.85,1)
    for t in ts:
        w=max(.05,W*math.sin(math.pi*min(t,.98))**.7);th=max(.03,T*w/W);y=-sag*t*t
        rings.append([(w*math.cos(6.2832*j/8),y+th*math.sin(6.2832*j/8),t*L) for j in range(8)])
    p=math.radians(pitch);d=V((dx*math.cos(p),dz*math.cos(p),-math.sin(p)))
    Tm=M.Translation(A(*root))@d.to_track_quat("Y","Z").to_matrix().to_4x4()
    return loft(n,m,rings,T=Tm)
def petal_canopy(k):
    C_=G["canopy"];mx,mz,mh,mr=C_["mast"];kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,12),0)])
    for s in (-1,1):
        x=s*mx;cyl("Mast"+str(s),"Metal",(x,0,mz),(x,mh,mz),mr,8);ico("Cap"+str(s),"Gold",(x,mh,mz),C_["cap"],2)
        for i,(dx,dz,L,pitch,m) in enumerate(C_["petals"]):petal("Petal"+str(s)+str(i),m,(x,mh,mz),dx*s,dz,L,pitch)
def canopy_clear():
    lo,hi=stage_bounds();return [G["stage"]["crowd"],((max(lo.x,-9.2),lo.y,lo.z),(min(hi.x,9.2),hi.y+.2,hi.z))]
def stage_review(k):
    stage_bounds()
    for name,eye,at in (("front",(0,1.6,-24),(0,6,0)),("high",(18,14,-22),(0,6,0))):
        render(os.path.join(G["rev"],k+"-"+name+".png"),camera(eye,at,28),lambda o:o.get("kit") in ("FestivalStageMirage","FestivalPetalCanopy") or o.get("ctx"))
```

Update `BUILD`, `CLEAR` and `REVIEW`:

```python
BUILD={"FestivalPalmTall":lambda k:palm(k,G["palm"]["tall"]),"FestivalPalmLean":lambda k:palm(k,G["palm"]["lean"]),
"FestivalWheelBase":wheel_base,"FestivalWheelRotor":wheel_rotor,"FestivalWheelGondola":gondola,
"FestivalStageMirage":stage_mirage,"FestivalPetalCanopy":petal_canopy}
CLEAR={"FestivalStageMirage":lambda:[G["stage"]["crowd"],G["stage"]["dj"]],"FestivalPetalCanopy":canopy_clear}
REVIEW={"FestivalPetalCanopy":stage_review}
```

Order matters. `REVIEW` imports the stage once (`stage_bounds` caches its bounds in `SB`). The generator deletes the `ctx` objects before `finish()`, and `CLEAR` runs inside `finish()` from the cached bounds, so no stage geometry is ever saved into `PoloDressing.blend`.

- [ ] **Step 2: Run, check and look**

Run with `<KITS>` = `FestivalStageMirage FestivalPetalCanopy`.
Expected: `exit=0`, `FestivalStageMirage` ≤ 6000 and `FestivalPetalCanopy` ≤ 4000.
If `finish` raises `blocking`:
- for the canopy, raise `"mast"[2]` (the mast height) by 0.5 m and re-run;
- for the dressing, the named part is in the crowd or DJ zone, so move it.

Run `check.py` for both. Expected: dressing x width ≤ 22, and the third `min` value is 0 for both.
Read `artifacts/polo-dressing/FestivalPetalCanopy-front.png` and `-high.png`. Expected:
- The stage from the crowd, with screens over both speaker stacks and a three-band arch behind the stage carrying a readable `PALM MIRAGE` sign.
- Six big petals fanning forward from two masts over the stage front, none touching the stage roof, and the middle of the stage left open.
- The text reads left to right from the front. If it is mirrored, change `label(...)` in `stage_mirage` to pass `R=rot(180,"y")@M.Rotation(math.pi/2,4,"X")` and re-run.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalStageMirage.fbx Assets/Festival/Art/Resources/FestivalPetalCanopy.fbx
git commit -m "ART-1: Model the main stage's Palm Mirage dressing and the petal canopy

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: Rainbow tower and giant astronaut

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"tower"` and `"astro"` to `CONFIG`, two builders, `BUILD` entries)
- Outputs: `FestivalRainbowTower.fbx`, `FestivalAstronaut.fbx`

**Interfaces:**
- Produces:
  - **Tower:** footprint ≤ 6 × 6 m, centred on its pivot.
  - **Astronaut:** faces +z, about 18 m long (z from −9.8 to +8.6) and 8.5 m wide. Task 10 turns it to face the crowd.

- [ ] **Step 1: Add the config and builders**

Add to `CONFIG`:

```python
"tower":{"h":20,"base":4.0,"top":6.0,"levels":8,"post_r":.12,"panels":40,"panel":(1.5,1.1,.08),"cols":("Rose","PaintRose","Gold","Cream","Mint","Glass","Blue")},
"astro":{"torso":((0,5.4,0),(2.2,2.1,3.5)),"helmet":((0,7.6,4.4),2.3),"visor":((0,7.5,6.25),(1.5,1.1,.5)),"pack":((0,8.1,-.6),(3.4,2.4,3.8)),
"armL":((-1.5,6.2,2.0),(-3.0,3.4,4.6),(-3.1,1.15,5.8)),"armR":((1.5,6.2,2.0),(3.5,6.4,5.8),(2.6,8.6,7.4)),"arm_r":.95,"glove":1.15,
"leg":((1.3,4.6,-2.2),(1.9,1.15,-3.6),(1.9,1.15,-7.4)),"leg_r":1.15,"boot":((1.9,1.2,-8.4),(2.2,2.4,2.8)),
"phone":((2.4,9.6,8.4),(2.6,5.0,.35)),"lens":((3.2,11.4,8.62),.35),"rec":((1.8,11.6,8.6),.15),"panel":((0,4.3,3.0),(2.0,1.2,.5))}
```

Add after `stage_review`:

```python
def rainbow_tower(k):
    T=G["tower"];h,n=T["h"],T["levels"];kit(k)
    half=lambda y:T["base"]/2+(T["top"]-T["base"])/2*(y/h)
    for sx in (-1,1):
        for sz in (-1,1):cyl("Post"+str(sx)+str(sz),"Metal",(sx*T["base"]/2,0,sz*T["base"]/2),(sx*T["top"]/2,h,sz*T["top"]/2),T["post_r"],6)
    corners=lambda s,y:[(-s,y,-s),(s,y,-s),(s,y,s),(-s,y,s)]
    for L in range(1,n+1):
        y=h*L/n;c=corners(half(y),y)
        for f in range(4):slab("Ring"+str(L)+str(f),"Metal",c[f],c[(f+1)%4],.08,.08)
    for L in range(n):
        y0,y1=h*L/n,h*(L+1)/n
        for f,(nx,nz) in enumerate(((0,-1),(1,0),(0,1),(-1,0))):
            slab("Strut"+str(L)+str(f),"Metal",(nx*half(y0),max(y0,.01),nz*half(y0)),(nx*half(y1),y1,nz*half(y1)),.08,.08)
    pw,ph,pt=T["panel"]
    for i in range(T["panels"]):
        y=.8+i/T["panels"]*(h-2.2);f=i%4;nx,nz=((0,-1),(1,0),(0,1),(-1,0))[f];along=((i//4)%3-1)*.45;s=half(y)
        c=(nx*s+(along if nz else 0),y,nz*s+(along if nx else 0))
        box("Panel"+str(i),T["cols"][i%len(T["cols"])],c,(pw,ph,pt) if nz else (pt,ph,pw))
    s=half(h);slab("TopX1","Metal",(-s,h,-s),(s,h,s),.08,.08);slab("TopX2","Metal",(s,h,-s),(-s,h,s),.08,.08)
    cyl("Crown mast","Metal",(0,h,0),(0,h+1.2,0),.06,6);ico("Star","StageGlowGold",(0,h+1.4,0),.6,1)
def astronaut(k):
    S=G["astro"];kit(k,[("Ground","Stone",(0,-.05,0),(24,.1,26),0)])
    ico("Torso","White",S["torso"][0],1,3,s=S["torso"][1],smooth=True);ico("Helmet","White",S["helmet"][0],S["helmet"][1],3,smooth=True)
    ico("Visor","Glass",S["visor"][0],1,2,s=S["visor"][1],smooth=True);box("Pack","Cream",S["pack"][0],S["pack"][1],.3)
    box("ChestPanel","Blue",S["panel"][0],S["panel"][1],.1)
    for i,m in enumerate(("StageGlowGold","StageGlowMint","StageGlowRose")):ico("Button"+str(i),m,(S["panel"][0][0]-.5+.5*i,S["panel"][0][1],S["panel"][0][2]+.28),.18,1)
    for nme in ("armL","armR"):
        pts=S[nme];tube(nme,"White",pts,S["arm_r"],8,smooth=True);ico(nme+"Glove","Gold",pts[-1],S["glove"],2,smooth=True)
    for sx in (-1,1):
        pts=[(sx*p[0],p[1],p[2]) for p in S["leg"]];tube("Leg"+str(sx),"White",pts,S["leg_r"],8,smooth=True)
        bc,bs=S["boot"];box("Boot"+str(sx),"Gold",(sx*bc[0],bc[1],bc[2]),bs,.25)
    box("Phone","Dark",S["phone"][0],S["phone"][1],.15)
    pc,ps=S["phone"];box("Screen","StageGlowMint",(pc[0],pc[1],pc[2]-ps[2]/2-.02),(ps[0]-.3,ps[1]-.6,.04))
    ico("Lens","Rubber",S["lens"][0],S["lens"][1],2);ico("Rec","StageGlowRose",S["rec"][0],S["rec"][1],1)
```

Add to `BUILD`: `"FestivalRainbowTower":rainbow_tower,"FestivalAstronaut":astronaut`.

- [ ] **Step 2: Run, check and look**

Run each model on its own, so each gallery is framed to its own size:
- `<KITS>` = `FestivalRainbowTower`: expected `exit=0`, ≤ 6000.
- `<KITS>` = `FestivalAstronaut`: expected `exit=0`, ≤ 8000.

Run `check.py` for both:
- Tower: x and depth ≤ 6.3 (posts lean out to ±3 m, plus their radius), height ≈ 22, min height 0.
- Astronaut: min height 0, length (the second `dims` value) ≈ 18.

Read both galleries and the check renders. Expected:
- The tower is a scaffold spiralling with rainbow panels up to a gold star.
- The astronaut crawls on its knees, with one gloved hand on the ground and the other holding up a phone. The phone's lit screen faces the visor and its lens and red light face forward.

If a limb floats, `finish` names it. Move that point into the torso and re-run.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalRainbowTower.fbx Assets/Festival/Art/Resources/FestivalAstronaut.fbx
git commit -m "ART-1: Model the rainbow tower and the giant astronaut filming the crowd

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: Desert horizon ridges

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"ridge"` to `CONFIG`, a builder, `BUILD` entries)
- Outputs: `FestivalDesertRidge1.fbx`, `FestivalDesertRidge2.fbx`, `FestivalDesertRidge3.fbx`

**Interfaces:**
- Produces: each ridge is 66 m wide (x ±33), with its front face at z = 0 and 6 m of depth toward +z. It has two materials: `Sand` above a `Stone` foot band. Task 10 rings 12 of them round the view at radius 115, each with its front facing the centre.

- [ ] **Step 1: Add the config and builder**

Add to `CONFIG`:

```python
"ridge":{"w":66,"d":6,"foot":2.5,"h":(30,40,24),"peaks":((0,0),(.08,.45),(.17,.3),(.27,.85),(.36,.6),(.46,1.0),(.55,.7),(.66,.9),(.76,.4),(.86,.55),(1,0))}
```

Add after `astronaut`:

```python
def ridge(k,v):
    R=G["ridge"];w,d,H=R["w"],R["d"],R["h"][v];kit(k)
    pk=R["peaks"];sh=[(t,pk[(i+3*v)%(len(pk)-2)+1][1] if 0<i<len(pk)-1 else 0) for i,(t,_) in enumerate(pk)]
    top=[(-w/2+t*w,max(R["foot"]+.5,H*y)) for t,y in sh];top[0]=(-w/2,R["foot"]);top[-1]=(w/2,R["foot"])
    ring=lambda z:[(-w/2,R["foot"],z)]+[(x,y,z) for x,y in top[1:-1]]+[(w/2,R["foot"],z)]
    loft("Ridge","Sand",[ring(0),ring(d)])
    box("Foot","Stone",(0,R["foot"]/2,d/2),(w,R["foot"],d))
```

Add to `BUILD`: `"FestivalDesertRidge1":lambda k:ridge(k,0),"FestivalDesertRidge2":lambda k:ridge(k,1),"FestivalDesertRidge3":lambda k:ridge(k,2)`.

- [ ] **Step 2: Run, check and look**

Run with `<KITS>` = `FestivalDesertRidge1 FestivalDesertRidge2 FestivalDesertRidge3`. Expected: `exit=0`, all three together ≤ 6000.
Run `check.py` for `FestivalDesertRidge2`. Expected: width 66, height 40, min height 0, and two objects (`Sand` and `Stone`).
Read the gallery. Expected: three jagged ridge silhouettes with different peak patterns, each on a stone foot band.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalDesertRidge*.fbx
git commit -m "ART-1: Model three desert ridge segments for Palm Mirage's horizon

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 6: Giggle Tank, balloon and VIP board

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"tank"`, `"balloon"` and `"vip"` to `CONFIG`, three builders, `BUILD` entries)
- Outputs: `FestivalGiggleTank.fbx`, `FestivalGiggleBalloon.fbx`, `FestivalVipBoard.fbx`

**Interfaces:**
- Produces:
  - **Tank:** pivot at its base. y from 0 to ≈ 1.2, x/z radius ≤ 0.23, the valve top at y ≈ 1.18. Same as `FestivalGiggleTank.Build`'s primitives.
  - **Balloon:** pivot at the balloon's centre. Body `Rose` (recoloured per balloon in Task 12), knot `White`, radii ≈ 0.17 / 0.21.
  - **VIP board:** pivot at the VIP sign group's origin.
    - Posts at x ±2.25, rising from y 3.4.
    - Board `Dark`, 4.8 × 0.4 m, centred at (0, 4.15, −0.012). Gold trim `StageGlowGold` sits behind it.
    - A gold crown with rose gems on top.
    - The lettering stays a TextMesh in code.

- [ ] **Step 1: Add the config and builders**

Add to `CONFIG`:

```python
"tank":{"body":((.17,0),(.21,.03),(.21,.78),(.19,.9),(.12,1.0),(.06,1.02)),"bands":((.54,.66,.22),(.3,.34,.215)),"valve":(1.0,1.17,.05),"wheel":(1.18,.1,.015)},
"balloon":{"body":((.02,-.21),(.07,-.18),(.15,-.1),(.17,0),(.15,.11),(.09,.18),(.01,.21)),"knot":(-.25,-.2,.025)},
"vip":{"pole":(2.25,3.4,.09),"board":(4.8,.4,4.15),"trim":.075}
```

Add after `ridge`:

```python
def giggle_tank(k):
    T=G["tank"];kit(k)
    cyl("Base","Dark",(0,0,0),(0,.04,0),.19,14);lathe("Body","PaintMint",T["body"],k=14,smooth=True)
    for i,(y0,y1,r) in enumerate(T["bands"]):cyl("Band"+str(i),"StageGlowRose",(0,y0,0),(0,y1,0),r,14)
    v0,v1,vr=T["valve"];cyl("Valve","Metal",(0,v0,0),(0,v1,0),vr,8);cyl("Spout","Metal",(0,1.1,0),(.12,1.1,0),.025,6)
    wy,wr,wt=T["wheel"];tube("HandWheel","Gold",[(wr*math.cos(6.2832*i/10),wy,wr*math.sin(6.2832*i/10)) for i in range(10)],wt,5,closed=True)
    slab("WheelBarX","Gold",(-wr,wy,0),(wr,wy,0),.02,.02);slab("WheelBarZ","Gold",(0,wy,-wr),(0,wy,wr),.02,.02)
    ico("Gauge","White",(0,.85,.19),.05,2)
def giggle_balloon(k):
    B=G["balloon"];kit(k);lathe("Balloon","Rose",B["body"],k=12,smooth=True)
    y0,y1,r=B["knot"];cyl("Knot","White",(0,y0,0),(0,y1,0),r,6,r2=r*.4)
def vip_board(k):
    V_=G["vip"];px,py,pz=V_["pole"];bw,bh,by=V_["board"];t=V_["trim"];kit(k,[("Anchor","Stone",(0,py-.02,pz),(2*px+.3,.04,.1),0)])
    for x in (-px,px):cyl("Post"+str(x),"Gold",(x,py,pz),(x,by+bh/2,pz),.04,6)
    box("Trim","StageGlowGold",(0,by,.025),(bw+t,bh+t,.045));box("Board","Dark",(0,by,-.012),(bw,bh,.045))
    top=by+(bh+t)/2;box("CrownBase","Gold",(0,top+.03,0),(.7,.08,.06))
    for x in (-.25,0,.25):cyl("Spike"+str(x),"Gold",(x,top+.07,0),(x,top+.32,0),.06,6,r2=.005);ico("Gem"+str(x),"StageGlowRose",(x,top+.32,0),.045,1)
```

Add to `BUILD`: `"FestivalGiggleTank":giggle_tank,"FestivalGiggleBalloon":giggle_balloon,"FestivalVipBoard":vip_board`.

The balloon's default ground shell crosses its middle at y = 0, so the audit anchors it. That's intended: the shell is removed when the parts merge.

- [ ] **Step 2: Run, check and look**

Run with `<KITS>` = `FestivalGiggleTank FestivalGiggleBalloon FestivalVipBoard`.
Expected: `exit=0`, tank ≤ 1000, balloon ≤ 300, VIP board ≤ 500.
Run `check.py` for each:
- Tank: height ≈ 1.2, min height 0, width ≤ 0.46.
- Balloon: two objects, height ≈ 0.46.
- Board: width ≈ 4.88.

Read the gallery. Expected: a rounded mint gas tank with rose bands, a valve and a gold hand wheel; a balloon with a knot; and a dark board with gold trim and a little gold crown.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalGiggleTank.fbx Assets/Festival/Art/Resources/FestivalGiggleBalloon.fbx Assets/Festival/Art/Resources/FestivalVipBoard.fbx
git commit -m "ART-1: Model the Giggle Tank, its balloon and the VIP stall board

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 7: Vision creatures

**Files:**
- Modify: `scripts/generate_polo_dressing.py` (add `"paints"` to `CONFIG`, the paint materials, five builders, `BUILD` entries)
- Outputs: `FestivalCreatureGnome.fbx`, `FestivalCreaturePixie.fbx`, `FestivalCreatureDragon.fbx`, `FestivalCreatureMushroomSprite.fbx`, `FestivalCreatureJackalope.fbx`

**Interfaces:**
- Produces: each creature has its feet at the origin, faces +z, and stands 0.2–0.5 m tall. Every part's material is `P<n>`, where n indexes `FestivalCreatures.Paints`:
  - 0 coat
  - 1 red
  - 2 skin
  - 3 cream
  - 4 pink
  - 5 wing
  - 6 green
  - 7 dark green
  - 8 tan
  - 9 antler
  - 10 eyes

  Renderers end up named `CreatureGnome P0__P0` and so on. Task 13 parses the `__P<n>` suffix.

- [ ] **Step 1: Add the paints and builders**

Add to `CONFIG`:

```python
"paints":((.22,.42,.9),(.9,.18,.16),(.95,.68,.52),(.98,.9,.62),(1,.45,.8),(.45,.95,1),(.3,.8,.3),(.15,.55,.35),(.72,.52,.32),(.85,.72,.5),(.3,.12,.4))
```

Directly after `G=CONFIG`, add the paint materials. They are review colours only; the game assigns its own:

```python
def paints():
    for i,c in enumerate(G["paints"]):
        m=bpy.data.materials.get("FK_P"+str(i)) or bpy.data.materials.new("FK_P"+str(i));m.diffuse_color=(*c,1);MAT["P"+str(i)]=m
```

Then call it after `setup(want)`, changing that line to `setup(want);paints()`.

Add after `vip_board`:

```python
def eyes(y,z,dx,r=.012):
    for s in (-1,1):ico("Eye"+str(s),"P10",(s*dx,y,z),r,1)
def gnome(k):
    kit(k);lathe("Coat","P0",[(.02,0),(.12,.005),(.13,.06),(.11,.16),(.07,.22),(.02,.23)],k=10)
    for s in (-1,1):ico("Boot"+str(s),"P8",(s*.05,.02,.03),.04,1,s=(1,.6,1.4))
    ico("Head","P2",(0,.28,0),.075,2,smooth=True);ico("Nose","P2",(0,.28,.075),.025,1)
    cyl("Beard","P3",(0,.27,.05),(0,.17,.07),.065,8,r2=.01);cyl("Hat","P1",(0,.33,0),(.02,.47,-.02),.08,8,r2=.005);eyes(.3,.065,.028)
def pixie(k):
    kit(k);lathe("Dress","P4",[(.02,0),(.09,.01),(.07,.12),(.04,.2),(.02,.22)],k=10)
    ico("Head","P2",(0,.27,0),.065,2,smooth=True);ico("Hair","P4",(0,.29,-.01),.07,2,s=(1,.8,1))
    for s in (-1,1):
        slab("WingUp"+str(s),"P5",(s*.02,.2,-.04),(s*.16,.32,-.08),.1,.008);slab("WingLow"+str(s),"P5",(s*.02,.18,-.04),(s*.13,.12,-.07),.07,.008)
        tube("Antenna"+str(s),"P10",[(s*.02,.32,0),(s*.05,.4,.02)],.006,4);ico("AntennaTip"+str(s),"P5",(s*.05,.4,.02),.015,1)
    eyes(.28,.058,.024)
def dragon(k):
    kit(k);ico("Body","P6",(0,.13,0),.1,2,s=(1,.9,1.5),smooth=True)
    for sx in (-1,1):
        for sz in (-1,1):cyl("Leg"+str(sx)+str(sz),"P7",(sx*.06,.08,sz*.07),(sx*.07,0,sz*.08),.025,6)
        slab("Wing"+str(sx),"P7",(sx*.05,.2,0),(sx*.2,.3,-.04),.14,.01);cyl("Horn"+str(sx),"P3",(sx*.03,.29,.13),(sx*.04,.34,.1),.012,5,r2=.002)
    ico("Head","P6",(0,.24,.15),.07,2,smooth=True);ico("Snout","P6",(0,.23,.22),.045,2,s=(1,.8,1.4))
    tube("Tail","P6",[(0,.12,-.14),(0,.08,-.25),(.04,.06,-.33)],.03,6);cyl("TailTip","P1",(.04,.06,-.33),(.06,.05,-.38),.025,5,r2=.002)
    eyes(.27,.2,.03)
def mushroom_sprite(k):
    kit(k);lathe("Stem","P3",[(.03,0),(.06,.01),(.055,.1),(.05,.18),(.045,.2)],k=10)
    lathe("Cap","P1",[(.04,.17),(.14,.18),(.15,.21),(.12,.26),(.06,.29),(.01,.3)],k=12,smooth=True)
    for i,p in enumerate(((.07,.27,.05),(-.08,.26,.04),(0,.28,-.08),(.1,.235,-.06),(-.03,.29,0))):ico("Spot"+str(i),"P3",p,.022,1)
    for s in (-1,1):tube("Arm"+str(s),"P3",[(s*.05,.1,0),(s*.09,.06,.02)],.01,4)
    eyes(.12,.05,.022)
def jackalope(k):
    kit(k);ico("Body","P8",(0,.11,0),.1,2,s=(1,1,1.3),smooth=True)
    for s in (-1,1):
        ico("Haunch"+str(s),"P8",(s*.06,.05,-.07),.05,1);cyl("Foot"+str(s),"P8",(s*.04,.06,.08),(s*.045,0,.1),.02,5)
        tube("Ear"+str(s),"P8",[(s*.03,.27,.1),(s*.04,.36,.09)],.018,5)
        tube("Antler"+str(s),"P9",[(s*.02,.28,.09),(s*.05,.38,.06),(s*.08,.44,.07)],.009,4);tube("Tine"+str(s),"P9",[(s*.05,.38,.06),(s*.02,.43,.05)],.007,4)
    ico("Head","P8",(0,.22,.11),.07,2,smooth=True);ico("Snout","P3",(0,.2,.17),.03,1);ico("Tail","P3",(0,.14,-.13),.03,1);eyes(.24,.16,.04)
```

Add to `BUILD`:

```python
"FestivalCreatureGnome":gnome,"FestivalCreaturePixie":pixie,"FestivalCreatureDragon":dragon,"FestivalCreatureMushroomSprite":mushroom_sprite,"FestivalCreatureJackalope":jackalope
```

- [ ] **Step 2: Run, check and look**

Run with `<KITS>` = all five creature names. Expected: `exit=0`, each ≤ 1200.
Run `check.py` for each. Expected: height in 0.2–0.5 and min height 0.
Read the gallery. Expected:
- a gnome with a red hat and beard;
- a pink pixie with cyan wings;
- a green dragon with wings and a tail;
- a red-capped mushroom with cream spots;
- a tan jackalope with antlers.

If `finish` reports a floating part, move it into its neighbour by 0.01 and re-run.

- [ ] **Step 3: Commit**

```bash
git add scripts/generate_polo_dressing.py ArtSource/PoloDressing.blend ArtSource/polo-dressing-manifest.json Assets/Festival/Art/Resources/FestivalCreature*.fbx
git commit -m "ART-1: Model VISION-2's five creatures with per-paint parts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 8: STAMPS on the held tin, PRISM source strings, Phase A docs

The tin a player holds for Tongue Stamps is `FestivalStockPrism` (`FestivalSession.cs:610`), built by `generate_festival_dressing.py`, and its label carries no text. The `PRISM` text sits on `FestivalStock`, which no runtime code shows. `PRISM SUPPLY` exists only in the `ProductionSample03` review scene. So this task:
- prints `STAMPS` on the held tin;
- fixes both PRISM strings at their source, without regenerating unused assets.

**Files:**
- Modify: `scripts/generate_festival_dressing.py` (`prism_tin`, after line 160)
- Modify: `scripts/generate_festival_world_assets.py:1317`, `scripts/finish_festival_world_package.py:69`
- Modify: `docs/ASSET_PROVENANCE.md`, `docs/BLENDER_ASSET_BRIEF.md`, `docs/superpowers/specs/2026-10-01-art1-palm-mirage-design.md`
- Outputs: `Assets/Festival/Art/Resources/FestivalStockPrism.fbx`, `ArtSource/FestivalDressing.blend`, `ArtSource/festival-dressing-manifest.json`

- [ ] **Step 1: Print STAMPS round the tin's label**

In `scripts/generate_festival_dressing.py`, `prism_tin()` line 160 ends with `olathe("LidRim","Gold",t["rim"])`. Insert after that line:

```python
    for side in (-1,1):
        o=label("Stamps"+("A" if side<0 else "B"),"Dark","STAMPS",(0,(l0+l1)/2,side*(ld+.002)),size=.07,depth=.004,R=(rot(180,"y") if side>0 else I)@M.Rotation(math.pi/2,4,"X"))
        n,t_=V((0,-side,0)),V((1,0,0))
        for v in o.data.vertices:
            u,d=v.co.dot(t_),v.co.dot(n);a=u/ld;v.co=n*(d*math.cos(a))+t_*(d*math.sin(a))+V((0,0,v.co.z))
```

This wraps the flat text onto the label cylinder (radius `ld`, the tin's axis at the origin). Arc length becomes angle, and distance along the side normal becomes radius.

- [ ] **Step 2: Regenerate and keep only the tin**

```bash
cd /Users/mboyajian/Code/friendslop-game && B=/Applications/Blender.app/Contents/MacOS/Blender
"$B" -b --factory-startup --python-expr "import bpy;bpy.ops.wm.read_factory_settings(use_empty=True)" --python-exit-code 1 --python scripts/generate_festival_dressing.py > artifacts/festival-dressing-stamps.log 2>&1; echo "exit=$?"
git checkout -- Assets/Festival/Art/Resources/FestivalMarketCounter.fbx Assets/Festival/Art/Resources/FestivalPriceTag.fbx Assets/Festival/Art/Resources/FestivalBuntingPole.fbx Assets/Festival/Art/Resources/FestivalLightPole.fbx Assets/Festival/Art/Resources/FestivalDJRiser.fbx Assets/Festival/Art/Resources/FestivalStockMoon.fbx
git status --short; git diff ArtSource/festival-dressing-manifest.json
```

Expected:
- `exit=0`.
- `git status` lists only the script, `FestivalStockPrism.fbx`, `ArtSource/FestivalDressing.blend` and the manifest.
- The manifest diff changes only `FestivalStockPrism`'s numbers. If other entries changed, stop and report it; that means the generator isn't deterministic.

Read the dressing's comparison renders under `artifacts/festival-dressing/` (the files the log names). Expected: `STAMPS` readable on both sides of the tin, lying flat on the label.

- [ ] **Step 3: Fix the two PRISM source strings**

- In `scripts/generate_festival_world_assets.py:1317`, change `"PRISM"` to `"STAMPS"`.
- In `scripts/finish_festival_world_package.py:69`, change `'PRISM SUPPLY',(0,1.68,2.61),.28` to `'GOOD TIMES SUPPLY',(0,1.68,2.61),.2`. The smaller size keeps the longer text at the old width.

Neither output is regenerated: `FestivalStock` and `ProductionSample03` aren't shown in the game.

- [ ] **Step 4: Update docs**

- In `docs/ASSET_PROVENANCE.md`, append a section:

```markdown
## October 1 Palm Mirage art pass (ART-1)

`scripts/generate_polo_dressing.py` regenerates `ArtSource/PoloDressing.blend`. It also writes the original scripted Blender models for Palm Mirage's palms, Ferris wheel, stage dressing, petal canopy, rainbow tower, astronaut and desert ridges, plus the Giggle Tank, balloon, VIP board and VISION-2's creatures, and their manifest `ArtSource/polo-dressing-manifest.json`. The astronaut is an original design. No third-party assets or references were copied.
```

- In `docs/BLENDER_ASSET_BRIEF.md`'s `## Changelog`, append `- Changed (2026-10-01, ART-1): Palm Mirage, festival 1, now has a polo-field look with palms, a lawn, desert ridges, a Ferris wheel, a dressed main stage and three landmarks. This replaces the temperate-forest target for that festival only.`
- In the spec, make three changes:
  - Replace the `FestivalDesertRidge1..3` row's size cell with `25–40 m tall, ringed round the view at 115 m: the camera's far clip is 130 m and fog ends at 90 m, so they read as hazy silhouettes`.
  - Replace the **PRISM labels** paragraph with this task's findings (STAMPS printed on the held `FestivalStockPrism`; both PRISM strings fixed at their source; the stall has no banner in the game).
  - Change "review galleries" in Merge path step 1 to "review galleries (local; `artifacts/` is gitignored)".

- [ ] **Step 5: Commit**

```bash
git add scripts/generate_festival_dressing.py scripts/generate_festival_world_assets.py scripts/finish_festival_world_package.py Assets/Festival/Art/Resources/FestivalStockPrism.fbx ArtSource/FestivalDressing.blend ArtSource/festival-dressing-manifest.json docs/ASSET_PROVENANCE.md docs/BLENDER_ASSET_BRIEF.md docs/superpowers/specs/2026-10-01-art1-palm-mirage-design.md
git commit -m "ART-1: The Tongue Stamps tin reads STAMPS; PRISM strings fixed at source

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Phase B — wiring (starts only when `.worktrees/fun-loop/2026-10-01-wave.md` says `**Status:** DONE`)

### Task 9: Merge the finished wave and import the new models

**Files:**
- Merge: `claude/fun-loop` → `art/festival-art`
- Create: `.meta` files for every new FBX (Unity generates them)

- [ ] **Step 1: Confirm the wave is done, then merge**

```bash
cd /Users/mboyajian/Code/friendslop-game && head -3 .worktrees/fun-loop/2026-10-01-wave.md
git merge --no-edit claude/fun-loop; echo "exit=$?"
```

Expected: the status line says DONE, and `exit=0` with no conflicts (the wave never touches the art paths). If there are conflicts, use the superpowers:resolving-merge-conflicts skill.

- [ ] **Step 2: Re-read the files Phase B edits**

```bash
git diff 3bed98b HEAD --stat -- Assets/Festival/Runtime/Presentation/FestivalWorld.cs Assets/Festival/Runtime/Presentation/FestivalTwistVisuals.cs Assets/Festival/Runtime/Presentation/FestivalGiggleTank.cs Assets/Festival/Runtime/Presentation/FestivalCreatures.cs Assets/Festival/Runtime/Presentation/FestivalArtView.cs Assets/Festival/Tests/EditMode/TwistVisualsTests.cs Assets/Festival/Tests/EditMode/GiggleGasPerceptionTests.cs
```

For any file listed, read the changed regions. Tasks 10–13 quote 3bed98b. If a quoted line moved or changed, apply the same edit to its new form.

- [ ] **Step 3: Import and generate**

```bash
node scripts/unity.mjs generate > artifacts/art1-generate.log 2>&1; echo "exit=$?"
git status --short Assets/Festival/Art/Resources | grep "\.meta"
```

Expected: `exit=0`, and one new `.meta` for each of the 22 new FBX. If `generate` changed anything under `Assets/Festival/Generated`, keep it: it reflects the merged code.

- [ ] **Step 4: Baseline gates**

```bash
node scripts/test-domain.mjs all > artifacts/art1-domain.log 2>&1; echo "exit=$?"
node scripts/unity.mjs test-edit > artifacts/art1-edit-base.log 2>&1; echo "exit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-edit-base.log
```

Expected: both `exit=0`, and `1` from the grep. If anything fails here, it's the merged wave's problem, not ours: stop and report it.

- [ ] **Step 5: Commit**

```bash
git add Assets/Festival/Art/Resources/*.meta Assets/Festival/Generated
git commit -m "ART-1: Import the Palm Mirage models into Unity

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 10: FestivalPoloDressing, the palette, and the world hook

**Files:**
- Create: `Assets/Festival/Runtime/Presentation/FestivalPoloDressing.cs`
- Modify: `Assets/Festival/Runtime/Presentation/FestivalArtView.cs` (`colors`, texture map)
- Modify: `Assets/Festival/Runtime/Presentation/FestivalWorld.cs` (collect the ground trees in `Build`, create the dressing, call `Show` from `SetTwists`)
- Test: `Assets/Festival/Tests/EditMode/PoloDressingTests.cs`

**Interfaces:**
- Consumes: the Phase A resources.
- Produces:
  - `FestivalPoloDressing(Transform grounds, List<GameObject> trees, Material earth)`
  - `void Show(bool polo, Vector3 view)`
  - `const string RootName="Palm Mirage dressing"`, `HorizonName="Desert horizon"`, `LawnName="Polo lawn"`
  - `static readonly string[] Models`
  - `public static bool InLandmark(Vector3 at)`
  - On `FestivalWorld`: `public FestivalPoloDressing PoloDressing { get; }`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Festival/Tests/EditMode/PoloDressingTests.cs`:

```csharp
using System.Collections.Generic;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // ART-1: Palm Mirage's dressing. Palms stand in for the grounds' trees and the landmarks stand outside the walls, all shown only at
    // Palm Mirage, and none of it collides, so play is exactly what it was.
    public sealed class PoloDressingTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup(){foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);made.Clear();FestivalCharacter.ViewTransform=null;}

        [Test]public void EveryModelKeepsItsPaletteColours()
        {
            var parent=Made("Models").transform;
            foreach(var model in FestivalPoloDressing.Models)
            {
                var go=FestivalArtView.Create(parent,model);
                Assert.That(go,Is.Not.Null,model+" loads from Resources");
                var renderers=go.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length,Is.GreaterThanOrEqualTo(2),model+" keeps two or more renderers, so Unity doesn't collapse it onto its root");
                foreach(var r in renderers)
                {
                    Assert.That(r.name.Contains("__"),Is.True,model+"/"+r.name+" names its palette colour");
                    Assert.That(r.sharedMaterial.color,Is.Not.EqualTo(Color.white),model+"/"+r.name+" has a known palette colour, not the white fallback");
                }
                Assert.That(go.GetComponentsInChildren<Collider>(true),Is.Empty,model+" never collides");
            }
        }

        [Test]public void PalmMirageSwapsTreesForPalmsBothWaysAndEmberPlayaKeepsItsTrees()
        {
            var world=World();var dressing=Dressing(world);var trees=Trees(world);
            Assert.That(trees.Count,Is.GreaterThan(60),"setup: the grounds have their trees");
            foreach(var festival in new[]{Festivals.PoloFestival,Festivals.PlayaFestival,Festivals.PoloFestival,Festivals.PlayaFestival})
            {
                world.SetTwists(Round(festival));bool polo=festival==Festivals.PoloFestival;
                Assert.That(dressing.gameObject.activeSelf,Is.EqualTo(polo),Festivals.Name(festival)+": the dressing shows only at Palm Mirage");
                foreach(var tree in trees)Assert.That(tree.activeSelf,Is.EqualTo(!polo),Festivals.Name(festival)+": "+tree.name+" hides only at Palm Mirage");
            }
        }

        [Test]public void PalmsStandOnTreeSpotsClearOfTheLandmarks()
        {
            var world=World();var dressing=Dressing(world);var spots=new List<Vector3>();
            foreach(var tree in Trees(world))if(!FestivalPoloDressing.InLandmark(tree.transform.localPosition))spots.Add(tree.transform.localPosition);
            var palms=new List<Transform>();foreach(Transform child in dressing)if(child.name.StartsWith("FestivalPalm"))palms.Add(child);
            Assert.That(palms.Count,Is.EqualTo(spots.Count),"one palm for each tree spot clear of a landmark");
            foreach(var palm in palms)
            {
                Assert.That(spots.Exists(s=>Vector3.Distance(s,palm.localPosition)<1e-3f),Is.True,palm.name+" stands on a tree spot");
                Assert.That(FestivalPoloDressing.InLandmark(palm.localPosition),Is.False,palm.name+" stands clear of the landmarks");
            }
        }

        [Test]public void LandmarksStandOutsideThePlayAreaAndTheCanopyStaysOverhead()
        {
            var world=World();var dressing=Dressing(world);world.SetTwists(Round(Festivals.PoloFestival));
            foreach(var name in new[]{"FestivalRainbowTower","FestivalAstronaut"})
            {
                var b=Bounds(dressing.Find(name));
                Assert.That(b.min.x>40||b.max.x<-40||b.min.z>40||b.max.z<-40,Is.True,name+" stands wholly outside the walls ("+b+")");
            }
            foreach(var r in dressing.Find("FestivalPetalCanopy").GetComponentsInChildren<Renderer>())
            {
                if(!r.name.EndsWith("__Metal"))Assert.That(r.bounds.min.y,Is.GreaterThanOrEqualTo(6f),r.name+" hangs above the crowd's heads");
                else
                {
                    // Both masts merge into one Metal renderer, so its bounds span from one speaker stack to the other.
                    Assert.That(-r.bounds.min.x,Is.InRange(9.5f,10.5f),"the west mast rises from its speaker stack");
                    Assert.That(r.bounds.max.x,Is.InRange(9.5f,10.5f),"the east mast rises from its speaker stack");
                    Assert.That(r.bounds.center.z,Is.InRange(30f,32f),"the masts stand in the speaker stacks' depth");
                }
            }
        }

        [Test]public void TheHorizonFollowsTheViewWithinTheFarClip()
        {
            var world=World();var dressing=Dressing(world);var eye=Made("Eye").transform;FestivalCharacter.ViewTransform=eye;
            foreach(var at in new[]{new Vector3(40,1.6f,40),new Vector3(-40,1.6f,-40),new Vector3(40,1.6f,-40),Vector3.zero})
            {
                eye.position=at;world.SetTwists(Round(Festivals.PoloFestival));
                var ridges=dressing.Find(FestivalPoloDressing.HorizonName).GetComponentsInChildren<Renderer>();
                Assert.That(ridges.Length,Is.GreaterThanOrEqualTo(12),"setup: the horizon has its ridges");
                foreach(var r in ridges)
                    Assert.That(Vector2.Distance(new Vector2(r.bounds.center.x,r.bounds.center.z),new Vector2(at.x,at.z)),Is.LessThan(125f),r.name+" stays inside the 130 m far clip from "+at);
            }
        }

        [Test]public void TheLawnLiesUnderThePaths()
        {
            var world=World();var dressing=Dressing(world);world.SetTwists(Round(Festivals.PoloFestival));
            var lawn=dressing.Find(FestivalPoloDressing.LawnName);
            Assert.That(lawn,Is.Not.Null,"Palm Mirage has its lawn");
            var b=lawn.GetComponent<Renderer>().bounds;
            Assert.That(b.center.y,Is.InRange(.0005f,.0029f),"just over the ground, under the paths (which start at 0.003 m)");
            Assert.That(b.size.x,Is.EqualTo(80).Within(.01f),"it covers the play area");
            Assert.That(lawn.GetComponentsInChildren<Collider>(true),Is.Empty,"it never collides");
        }

        private FestivalWorld World(){var world=Made("Polo world").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");return world;}
        private static Transform Dressing(FestivalWorld world)
        {
            var d=world.transform.Find(FestivalWorld.RootName).Find(FestivalPoloDressing.RootName);
            Assert.That(d,Is.Not.Null,"the grounds carry Palm Mirage's dressing");return d;
        }
        private static List<GameObject> Trees(FestivalWorld world)
        {
            var trees=new List<GameObject>();
            foreach(Transform child in world.transform.Find(FestivalWorld.RootName))if(child.name.StartsWith("FestivalTree"))trees.Add(child.gameObject);
            return trees;
        }
        private static RoundState Round(int festival)=>new RoundState{Phase="Playing",FestivalIndex=festival,LevelIndex=0,DurationSeconds=Festivals.Level(festival,0,0).DurationSeconds};
        private static Bounds Bounds(Transform part){var rs=part.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        private GameObject Made(string name){var go=new GameObject(name);made.Add(go);return go;}
    }
}
```

- [ ] **Step 2: Run them and see them fail**

```bash
node scripts/unity.mjs test-edit -testFilter "PoloDressingTests" > artifacts/art1-t10-red.log 2>&1; echo "exit=$?"
```

Expected: non-zero exit, with compile errors naming `FestivalPoloDressing`.

- [ ] **Step 3: Add the palette keys to FestivalArtView**

In `FestivalArtView.cs`, inside the `colors` initializer after `{"Glass",new Color(.30f,.73f,.79f)},{"White",new Color(.86f,.90f,.82f)}`, add:

```csharp
            ,{"Frond",new Color(.24f,.55f,.30f)},{"PalmBark",new Color(.55f,.42f,.30f)},{"Sand",new Color(.80f,.62f,.42f)}
```

In `MaterialFor`, change the texture map so palms use the bark and leaf textures:

```csharp
                :color=="Bark"||color=="PalmBark"?"FestivalBark"
                :color=="Wood"?"FestivalWood"
                :color=="Leaf"||color=="LeafWarm"||color=="Needle"||color=="Frond"?"FestivalLeaf"
```

(That replaces the existing `:color=="Bark"?"FestivalBark"` and `:color=="Leaf"||color=="LeafWarm"||color=="Needle"?"FestivalLeaf"` lines.)

- [ ] **Step 4: Write FestivalPoloDressing**

Create `Assets/Festival/Runtime/Presentation/FestivalPoloDressing.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>ART-1: Palm Mirage's polo-field look.
    /// - A palm stands at each of the grounds' tree spots, while the trees hide.
    /// - The main stage gets screens, an arch and its sign; the petal canopy reaches over the stage front.
    /// - A rainbow tower and a giant astronaut stand outside the walls.
    /// - A lawn tint covers the grounds, and desert ridges ring the view.
    /// All of it shows only at Palm Mirage (Show) and none of it collides, so sight lines, steps and paths stay exactly the rules'.</summary>
    public sealed class FestivalPoloDressing
    {
        public const string RootName="Palm Mirage dressing",HorizonName="Desert horizon",LawnName="Polo lawn";
        public static readonly string[] Models={"FestivalPalmTall","FestivalPalmLean","FestivalStageMirage","FestivalPetalCanopy","FestivalRainbowTower",
            "FestivalAstronaut","FestivalDesertRidge1","FestivalDesertRidge2","FestivalDesertRidge3","FestivalWheelBase","FestivalWheelRotor",
            "FestivalWheelGondola","FestivalGiggleTank","FestivalGiggleBalloon","FestivalVipBoard"};
        // ponytail: calibration knobs, tuned against Task 14's screenshots.
        // - Landmarks stand outside the ±40 m walls, and no palm grows within Clear metres of one.
        // - The ridges ring the view at HorizonRadius: the camera clips at 130 m and the fog ends at 90 m, so they read as hazy silhouettes.
        private static readonly (string Model,Vector3 At,float Yaw,float Clear)[] Landmarks={
            ("FestivalRainbowTower",new Vector3(-50,0,48),20,5),("FestivalAstronaut",new Vector3(54,0,8),-90,11)};
        private static readonly Vector3 StageAt=new Vector3(0,0,32);
        private const float HorizonRadius=115,LawnHeight=.0015f,BackRowZ=37.5f;
        private const int Ridges=12;
        private static readonly Color LawnTint=new Color(.55f,.78f,.42f);

        private readonly Transform root,horizon;
        private readonly List<GameObject> trees;
        private bool? shown;

        public FestivalPoloDressing(Transform grounds,List<GameObject> trees,Material earth)
        {
            this.trees=trees;
            root=new GameObject(RootName).transform;root.SetParent(grounds,false);root.gameObject.SetActive(false);
            for(int i=0;i<trees.Count;i++)
            {
                var spot=trees[i].transform;if(InLandmark(spot.localPosition))continue;
                Place(Mathf.Abs(spot.localPosition.z)>=BackRowZ?"FestivalPalmTall":"FestivalPalmLean",spot.localPosition,i*137%360,spot.localScale.x);
            }
            Place("FestivalStageMirage",StageAt,0,1);Place("FestivalPetalCanopy",StageAt,0,1);
            foreach(var l in Landmarks)Place(l.Model,l.At,l.Yaw,1);
            horizon=new GameObject(HorizonName).transform;horizon.SetParent(root,false);
            for(int i=0;i<Ridges;i++)
            {
                var ridge=FestivalArtView.Create(horizon,"FestivalDesertRidge"+(i%3+1));if(ridge==null)continue;
                var turn=Quaternion.Euler(0,i*360f/Ridges,0);ridge.transform.localRotation=turn;ridge.transform.localPosition=turn*Vector3.forward*HorizonRadius;
                ridge.transform.localScale=new Vector3(1,.8f+.15f*(i%4),1);
            }
            var lawn=GameObject.CreatePrimitive(PrimitiveType.Quad);lawn.name=LawnName;lawn.transform.SetParent(root,false);
            lawn.transform.localPosition=new Vector3(0,LawnHeight,0);lawn.transform.localRotation=Quaternion.Euler(90,0,0);lawn.transform.localScale=new Vector3(80,80,1);
            var collider=lawn.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            var renderer=lawn.GetComponent<Renderer>();renderer.sharedMaterial=new Material(earth){name="Polo lawn",color=LawnTint};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Every frame: Palm Mirage's dressing on and the trees off, or the other way round, with the ridges kept round the view.
        /// It allocates nothing.</summary>
        public void Show(bool polo,Vector3 view)
        {
            if(shown!=polo)
            {
                shown=polo;root.gameObject.SetActive(polo);
                foreach(var tree in trees)if(tree!=null)tree.SetActive(!polo);
            }
            if(polo)horizon.position=new Vector3(view.x,root.position.y,view.z);
        }

        public static bool InLandmark(Vector3 at)
        {
            foreach(var l in Landmarks)if(new Vector2(at.x-l.At.x,at.z-l.At.z).sqrMagnitude<l.Clear*l.Clear)return true;
            return false;
        }

        private void Place(string model,Vector3 at,float yaw,float scale)
        {
            var go=FestivalArtView.Create(root,model);if(go==null)return;
            go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        }
        private static void Dispose(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
```

- [ ] **Step 5: Hook it into FestivalWorld**

In `FestivalWorld.cs`:

1. Next to the other private fields (near `twists`), add:

```csharp
        private readonly List<GameObject> groundTrees=new List<GameObject>();
        public FestivalPoloDressing PoloDressing{get;private set;}
```

2. There are three ground-tree `Visual(...)` calls in `Build()` (3bed98b lines 212 for the back rows, 227 for the outer ring and approach, 237 for the gathering trees). After each `var tree=Visual(...)` line, add `if(tree!=null)groundTrees.Add(tree);` alongside the existing `if(tree!=null)` handling. Do **not** touch the camp's trees in `BuildCamp` (lines 511 and 522).

3. After `twists=new FestivalTwistVisuals(owned);` (line 331), add:

```csharp
            // ART-1: Palm Mirage's look, shown by SetTwists; like the twists it never collides.
            PoloDressing=new FestivalPoloDressing(owned,groundTrees,earth);
```

`earth` is `Build()`'s local ground material from line 60, still in scope at line 331.

4. Replace `SetTwists` with:

```csharp
        public void SetTwists(RoundState state)
        {
            if(twists!=null)twists.Apply(state,Time.unscaledDeltaTime);if(giggleTank!=null)FestivalGiggleTank.Show(giggleTank,state);
            if(PoloDressing!=null)PoloDressing.Show(state.FestivalIndex==Festivals.PoloFestival,FestivalCharacter.ViewTransform!=null?FestivalCharacter.ViewTransform.position:Vector3.zero);
        }
```

If `using System.Collections.Generic;` is missing at the top of `FestivalWorld.cs`, add it.

- [ ] **Step 6: Run the new tests, then the suites that build the world**

```bash
node scripts/unity.mjs test-edit -testFilter "PoloDressingTests|TwistVisualsTests|GiggleTankWorldTests|WorldContractTests|CrowdLayoutTests|NightLightingTests" > artifacts/art1-t10.log 2>&1; echo "exit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-t10.log
```

Expected: `exit=0` and `1`.
If `PalmsStandOnTreeSpotsClearOfTheLandmarks` fails on the count, a tree was added to `groundTrees` twice; check step 5.2.
If `LandmarksStandOutside…` fails for the astronaut, raise its x in `Landmarks` by the reported overlap.

- [ ] **Step 7: Commit**

```bash
git add Assets/Festival/Runtime/Presentation/FestivalPoloDressing.cs Assets/Festival/Runtime/Presentation/FestivalPoloDressing.cs.meta Assets/Festival/Runtime/Presentation/FestivalArtView.cs Assets/Festival/Runtime/Presentation/FestivalWorld.cs Assets/Festival/Tests/EditMode/PoloDressingTests.cs Assets/Festival/Tests/EditMode/PoloDressingTests.cs.meta
git commit -m "ART-1: Palm Mirage shows palms, landmarks, a lawn and a desert horizon

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Unity writes the two `.meta` files during the test run. If either is missing, run `node scripts/unity.mjs generate` once.)

### Task 11: The Ferris wheel and VIP board use their models

**Files:**
- Modify: `Assets/Festival/Runtime/Presentation/FestivalTwistVisuals.cs` (`Wheel`, `VipSign`, new `Art` helper, class summary)
- Modify: `Assets/Festival/Tests/EditMode/TwistVisualsTests.cs` (`AGoldSignMarksTheNightMarketsVipStall`, plus a new wheel assertion)

**Interfaces:**
- Consumes: `FestivalWheelBase`, `FestivalWheelRotor`, `FestivalWheelGondola` (body `Rose`), `FestivalVipBoard` (board `Dark`, trim `StageGlowGold`).
- Produces: the same transforms as before: `"Ferris wheel"`, `"Wheel rotor"` (spins about local x), eight `"Gondola"` groups, `VipSignName`. `Turn()` is unchanged.

- [ ] **Step 1: Restate the VIP test and add a wheel assertion (it fails first)**

In `TwistVisualsTests.cs`, add this helper beside `Part`:

```csharp
        private static Renderer Painted(Transform root,string key)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("__"+key,System.StringComparison.Ordinal))return r;
            Assert.Fail("no __"+key+" renderer under "+root.name);return null;
        }
```

In `AGoldSignMarksTheNightMarketsVipStall`, replace `Part(sign,"Gold trim").GetComponent<Renderer>()` with `Painted(sign,"StageGlowGold")`, and `Part(sign,"Board").GetComponent<Renderer>()` with `Painted(sign,"Dark")`. The premises are unchanged: the trim is the glowing gold and the board is the dark panel.

In `VipRopesRingEachZoneAndTheFerrisWheelTurnsOncePerRide`, after the line asserting `Bounds(wheel).max.y` is greater than 10, add:

```csharp
            int gondolas=0;foreach(Transform child in wheel)if(child.name=="Gondola"){gondolas++;Assert.That(child.GetComponentInChildren<Renderer>(),Is.Not.Null,"each gondola carries its model");}
            Assert.That(gondolas,Is.EqualTo(8),"eight gondolas hang from the rim");
            Assert.That(Painted(wheel,"StageGlowMint"),Is.Not.Null,"the rim glows mint and rose, as before");
```

Run:

```bash
node scripts/unity.mjs test-edit -testFilter "TwistVisualsTests" > artifacts/art1-t11-red.log 2>&1; echo "exit=$?"
```

Expected: non-zero. `Painted` finds no `__StageGlowGold` renderer under the sign, because the primitives are named "Gold trim".

- [ ] **Step 2: Swap the primitives for the models**

In `FestivalTwistVisuals.cs`, replace `VipSign` with:

```csharp
        // POLO-2 / ART-1: the modeled board (dark, gold trim, a little crown) on the stall's poles: "VIP WRISTBANDS $15", at the rules' price.
        private static Renderer VipSign(Transform sign)
        {
            float height=LetterSize*2.25f+.13f,middle=SignBottom+height/2;
            FestivalArtView.Create(sign,"FestivalVipBoard");
            var face=new GameObject("Text");face.transform.SetParent(sign,false);face.transform.localPosition=new Vector3(0,middle,-.05f);
            var text=face.AddComponent<TextMesh>();text.text="VIP WRISTBANDS $"+Catalog.FindItem(FestivalSimulation.VipWristband).Price;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=LetterSize;text.fontSize=48;text.color=new Color(.96f,.94f,.84f);
            var font=Resources.Load<Font>("FestivalDisplay");if(font!=null){text.font=font;face.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            return face.GetComponent<MeshRenderer>();
        }
```

Replace `Wheel` with:

```csharp
        // ART-1: the modeled A-frame and platform at the rules' base; the rotor (rim, spokes, bulbs) turns at the axle and the gondolas
        // hang level below its anchors, each in its own colour.
        private Transform Wheel(Transform wheel)
        {
            Art(wheel,"FestivalWheelBase",null);
            var hub=Group(wheel,"Wheel rotor",Vector3.up*AxleHeight);Art(hub,"FestivalWheelRotor",null);
            Vector3 Rim(float degrees)=>new Vector3(0,WheelRadius*Mathf.Sin(degrees*Mathf.Deg2Rad),WheelRadius*Mathf.Cos(degrees*Mathf.Deg2Rad));
            for(int k=0;k<Gondolas;k++)
            {
                anchors[k]=Rim(360f*k/Gondolas);
                gondolas[k]=Group(wheel,"Gondola",Vector3.zero);Art(gondolas[k],"FestivalWheelGondola",k%3==0?"Rose":k%3==1?"Mint":"Gold");
            }
            return hub;
        }
        // A model under parent; if body is given, the model's Rose parts take that colour instead.
        private static void Art(Transform parent,string model,string body)
        {
            var go=FestivalArtView.Create(parent,model);if(go==null||body==null)return;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("__Rose",System.StringComparison.Ordinal))r.sharedMaterial=FestivalArtView.MaterialFor(body);
        }
```

Also delete `RimSegments` from the `private const int Gondolas=8,RimSegments=16;` line; nothing uses it now. In the class summary, change "stand-ins for each festival's twists, from primitives and existing props, until ART-1 and ART-2 replace them" to "each festival's twists: Palm Mirage's from ART-1's models, Ember Playa's still from primitives until ART-2".

- [ ] **Step 3: Run the tests**

```bash
node scripts/unity.mjs test-edit -testFilter "TwistVisualsTests|PoloSessionTests" > artifacts/art1-t11.log 2>&1; echo "exit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-t11.log
```

Expected: `exit=0` and `1`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Festival/Runtime/Presentation/FestivalTwistVisuals.cs Assets/Festival/Tests/EditMode/TwistVisualsTests.cs
git commit -m "ART-1: The Ferris wheel and the VIP board are their models

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 12: The Giggle Tank and balloons use their models

**Files:**
- Modify: `Assets/Festival/Runtime/Presentation/FestivalGiggleTank.cs` (`Build`, `BuildBalloons`)
- Modify: `Assets/Festival/Tests/EditMode/GiggleGasPerceptionTests.cs:149` (balloon renderers by model name)
- Modify: `Assets/Festival/Tests/EditMode/GiggleTankWorldTests.cs` (add one assertion)

**Interfaces:**
- Consumes: `FestivalGiggleTank` and `FestivalGiggleBalloon` (body `Rose`, knot `White`).
- Produces: `Name`, `BalloonsName`, `Show` and the glow disc are unchanged.

- [ ] **Step 1: Restate the balloon test and add a tank assertion (it fails first)**

In `GiggleGasPerceptionTests.cs:149`, replace `if(r.name=="Balloon")balloons.Add(r);` with:

```csharp
if(r.name.StartsWith("GiggleBalloon",System.StringComparison.Ordinal)&&r.name.EndsWith("__Rose",System.StringComparison.Ordinal))balloons.Add(r);
```

The premise is unchanged: these are the balloons' bodies. In `GiggleTankWorldTests.ThePlaceholderIsATankThatNeverCollides`, after the renderer count assertion, add:

```csharp
            Assert.That(System.Array.Exists(renderers,r=>r.name.StartsWith("GiggleTank",System.StringComparison.Ordinal)),Is.True,"the tank is ART-1's model");
```

Run `node scripts/unity.mjs test-edit -testFilter "GiggleTankWorldTests|GiggleGasPerceptionTests" > artifacts/art1-t12-red.log 2>&1; echo "exit=$?"`. Expected: non-zero, on the two new expectations.

- [ ] **Step 2: Swap the primitives**

In `FestivalGiggleTank.cs`, replace the five `Part(tank,"Tank body"…)` to `Part(tank,"Tank hand wheel"…)` lines in `Build` with:

```csharp
            FestivalArtView.Create(tank,"FestivalGiggleTank");
```

(Keep the `"Tank glow"` line above it.) In `BuildBalloons`, replace the `Part(station,"Balloon",…)` line with:

```csharp
                var balloon=FestivalArtView.Create(station,"FestivalGiggleBalloon");
                if(balloon!=null)
                {
                    balloon.transform.localPosition=at;
                    foreach(var r in balloon.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("__Rose",System.StringComparison.Ordinal))r.sharedMaterial=FestivalArtView.MaterialFor(BalloonLook[i]);
                }
```

Update the class summary's "placeholder" wording to say the tank and balloons are ART-1's models.

- [ ] **Step 3: Run the tests**

```bash
node scripts/unity.mjs test-edit -testFilter "GiggleTankWorldTests|GiggleGasPerceptionTests" > artifacts/art1-t12.log 2>&1; echo "exit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-t12.log
```

Expected: `exit=0` and `1`. The existing checks still hold: the tank stands on the ground, 0.9–1.6 m tall, centred, ≥ 4 renderers, no colliders; balloons float above 1.65 m.

- [ ] **Step 4: Commit**

```bash
git add Assets/Festival/Runtime/Presentation/FestivalGiggleTank.cs Assets/Festival/Tests/EditMode/GiggleGasPerceptionTests.cs Assets/Festival/Tests/EditMode/GiggleTankWorldTests.cs
git commit -m "ART-1: The Giggle Tank and its balloons are their models

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 13: Vision creatures use their models

**Files:**
- Modify: `Assets/Festival/Runtime/Presentation/FestivalCreatures.cs:42-54` (`Looks`), `:177-187` (`Build`)
- Test: `Assets/Festival/Tests/EditMode/CreatureTests.cs` (one added assertion)

**Interfaces:**
- Consumes: `FestivalCreature<Name>` models with `__P<n>` parts.
- Produces: the same `"Creature <Name>"` roots, `Looks.Length` (5), `Palette` and `Paint(n)` as before.

- [ ] **Step 1: Add the failing assertion**

In `CreatureTests.CreaturesCarryTheFakeVisionTells`, inside the `foreach(var part in body.GetComponentsInChildren<Renderer>(true))` loop, add as the first line:

```csharp
                    Assert.That(part.name.Contains("__P"),Is.True,body.name+"/"+part.name+" is a part of ART-1's creature model");
```

Run `node scripts/unity.mjs test-edit -testFilter "CreatureTests" > artifacts/art1-t13-red.log 2>&1; echo "exit=$?"`. Expected: non-zero, because the primitives are named `Sphere`, `Capsule` and so on.

- [ ] **Step 2: Build creatures from their models**

Replace the `Looks` table, the `Part(...)` helper beneath it, and the `Ball/Pill/Can/Block` constant line (3bed98b lines 42–54) with:

```csharp
        // ART-1: each look is a model whose parts end __P<n>, painted from Paints[n].
        private static readonly (string Name,string Model)[] Looks={("Gnome","FestivalCreatureGnome"),("Pixie","FestivalCreaturePixie"),
            ("Dragon","FestivalCreatureDragon"),("Mushroom sprite","FestivalCreatureMushroomSprite"),("Jackalope","FestivalCreatureJackalope")};
```

Replace the body of `Build(int look)` with:

```csharp
            var root=new GameObject("Creature "+Looks[look].Name).transform;root.SetParent(transform,false);
            var model=FestivalArtView.Create(root,Looks[look].Model);
            if(model!=null)foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                int mark=renderer.name.LastIndexOf("__P",System.StringComparison.Ordinal);
                if(mark>=0&&int.TryParse(renderer.name.Substring(mark+3).Split('.')[0],out int paint)&&paint<Paints.Length)renderer.sharedMaterial=Paint(paint);
            }
            return new Creature{Root=root,Look=look};
```

`FestivalArtView.Create` already turns shadows off and strips colliders. If the compiler reports `ShadowCastingMode` or `PrimitiveType` usings as unused, leave them; if it reports errors for removed names, delete those references.

- [ ] **Step 3: Run the tests**

```bash
node scripts/unity.mjs test-edit -testFilter "CreatureTests|TripPerceptionTests|VisionMarkerTests" > artifacts/art1-t13.log 2>&1; echo "exit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-t13.log
```

Expected: `exit=0` and `1`. The existing checks still hold: 5 kinds, 0.2–0.5 m tall, no colliders, no shadows, every part fake-tinted.

- [ ] **Step 4: Commit**

```bash
git add Assets/Festival/Runtime/Presentation/FestivalCreatures.cs Assets/Festival/Tests/EditMode/CreatureTests.cs
git commit -m "ART-1: VISION-2's creatures are their models

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: Review shots, tuning, all gates and the hand-off

**Files:**
- Create: `Assets/Festival/Editor/PoloDressingShots.cs` (an Editor-only review tool)
- Modify: `FestivalPoloDressing.cs` knobs only (`Landmarks`, `HorizonRadius`, `LawnTint`, `LawnHeight`) if the shots call for it
- Modify: `docs/superpowers/specs/2026-10-01-art1-palm-mirage-design.md` (Hand-off section)

- [ ] **Step 1: Write the review tool**

Create `Assets/Festival/Editor/PoloDressingShots.cs`:

```csharp
using System.IO;
using Festival.Core;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Editor
{
    /// <summary>ART-1 review: renders Palm Mirage by day and night, and Ember Playa by day, from fixed spots into
    /// artifacts/polo-dressing/unity, through a camera like the game's (75° FOV, 130 m far clip).</summary>
    public static class PoloDressingShots
    {
        static readonly (string Name,Vector3 Eye,Vector3 At)[] Views={
            ("path-to-stage",new Vector3(0,1.6f,-10),new Vector3(0,5,32)),("under-canopy",new Vector3(3,1.6f,22),new Vector3(0,9,30)),
            ("stage-front",new Vector3(0,1.6f,14),new Vector3(0,6,32)),("wheel",new Vector3(8,1.6f,-16),new Vector3(20,6,-28)),
            ("east-astronaut",new Vector3(25,1.6f,6),new Vector3(54,5,8)),("northwest-tower",new Vector3(-20,1.6f,20),new Vector3(-50,10,48)),
            ("horizon-south",new Vector3(0,1.6f,0),new Vector3(0,10,-110)),("corner",new Vector3(38,1.6f,-38),new Vector3(-40,8,40))};
        public static void Capture()
        {
            var dir=Path.Combine(Directory.GetCurrentDirectory(),"artifacts/polo-dressing/unity");Directory.CreateDirectory(dir);
            var world=new GameObject("Shots").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var cam=new GameObject("Shot camera").AddComponent<Camera>();cam.fieldOfView=75;cam.nearClipPlane=.05f;cam.farClipPlane=130;
            var target=new RenderTexture(1280,720,24);cam.targetTexture=target;FestivalCharacter.ViewTransform=cam.transform;
            foreach(var (festival,level,tag) in new[]{(Festivals.PoloFestival,0,"polo-day"),(Festivals.PoloFestival,1,"polo-night"),(Festivals.PlayaFestival,0,"playa-day")})
            {
                var state=new RoundState{Phase="Playing",FestivalIndex=festival,LevelIndex=level,DurationSeconds=Festivals.Level(festival,level,0).DurationSeconds};
                foreach(var v in Views)
                {
                    cam.transform.position=v.Eye;cam.transform.LookAt(v.At);world.SetTwists(state);world.SetLighting(state,"");
                    cam.Render();RenderTexture.active=target;var shot=new Texture2D(1280,720,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,1280,720),0,0);shot.Apply();
                    File.WriteAllBytes(Path.Combine(dir,tag+"-"+v.Name+".png"),shot.EncodeToPNG());Object.DestroyImmediate(shot);
                }
            }
            RenderTexture.active=null;FestivalCharacter.ViewTransform=null;Debug.Log("POLO SHOTS WRITTEN: "+dir);
        }
    }
}
```

- [ ] **Step 2: Capture**

Unity must not be open on this project while the capture runs.

```bash
cd /Users/mboyajian/Code/friendslop-game && U=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
"$U" -batchmode -accept-apiupdate -projectPath "$PWD" -logFile artifacts/art1-shots.log -executeMethod Festival.Editor.PoloDressingShots.Capture -quit; echo "exit=$?"; grep -c "POLO SHOTS WRITTEN" artifacts/art1-shots.log
```

Expected: `exit=0`, `1`, and 24 PNGs in `artifacts/polo-dressing/unity/`.

- [ ] **Step 3: Look and tune**

Read every PNG. Check each item below; for any fix, change only the knob named.

- **polo-day path-to-stage:** palm rows, a green lawn, the stage arch with a readable `PALM MIRAGE`, and the canopy petals over the stage front. If the lawn is too saturated or too dark, change `LawnTint`. If the paths flicker or vanish, lower `LawnHeight`.
- **polo-day east-astronaut and northwest-tower:** both whole, standing outside the walls, not cut off by the far clip, and not swallowed by fog. If either is fogged out, move it closer to the walls (adjust `Landmarks` `At`, keeping it outside ±40 plus its footprint) and keep the Task 10 tests green.
- **polo-day horizon-south and corner:** ridges are visible as a silhouette band above the woodland rise, on every side. If they're invisible, lower `HorizonRadius` (not below 95). If ridges cut off at the edge of the frame, raise it (not above 120).
- **polo-day wheel:** gondolas hang level in rose, mint and gold, and the rim glows.
- **polo-night \*:** the screens, rim, bulbs and the sign's trim glow. Nights stay blue with neon (LIGHT-1).
- **playa-day \*:** no palms, landmarks, lawn or ridges. The trees are back and the playa looks as before.

After any knob change:
- re-run Step 2;
- re-run `node scripts/unity.mjs test-edit -testFilter "PoloDressingTests"`;
- commit with message `ART-1: Tune Palm Mirage's <knob> against review shots`.

- [ ] **Step 4: All gates**

Run each one, redirected to a file, and echo the exit code:

```bash
node scripts/test-domain.mjs all > artifacts/art1-g-domain.log 2>&1; echo "domain=$?"
node scripts/unity.mjs test-edit > artifacts/art1-g-edit.log 2>&1; echo "edit=$?"; grep -c "EDITMODE TESTS PASSED" artifacts/art1-g-edit.log
node scripts/unity.mjs test-play > artifacts/art1-g-play.log 2>&1; echo "play=$?"; grep -c "PLAYMODE TESTS PASSED" artifacts/art1-g-play.log
node scripts/unity.mjs build-mac-development > artifacts/art1-g-build.log 2>&1; echo "build=$?"
node scripts/native-smoke.mjs > artifacts/art1-g-smoke.log 2>&1; echo "smoke=$?"
node scripts/native-solo-smoke.mjs > artifacts/art1-g-solo.log 2>&1; echo "solo=$?"
```

Expected: every exit code is `0`, and each grep prints `1`.

Then compare frame time with the pre-merge build:
- the smoke log's frame-time line against the same line in `.worktrees/fun-loop`'s last smoke log;
- or, if neither prints one, the client screenshots' `DevelopmentDiagnostics` frame entries.

The gate is within 5%. If it's over, report the numbers to Matt instead of cutting art on your own.

Read the smoke's client screenshots (paths in `artifacts/art1-g-smoke.log`). They should show Palm Mirage dressing on festival 1 frames.

- [ ] **Step 5: Commit, then fill in the hand-off**

```bash
git add Assets/Festival/Editor/PoloDressingShots.cs Assets/Festival/Editor/PoloDressingShots.cs.meta
git commit -m "ART-1: Add the Palm Mirage review-shot tool

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

In the spec's `## Hand-off`, mark each item confirmed or struck, add "Branch `art/festival-art` is ready to merge into `claude/fun-loop` (fast-forward)", and commit.

Report to Matt:
- the branch and its commits;
- gate results with their exit codes;
- the review-shot folder;
- the hand-off list: merge OK, push as mattboya, sign-off in PLAYTEST-1, the queue update after the wave (ART-1 done, art freeze lifted, CROWD-4 B, VISION-4's goblin shadow and POLO-5's gondolas can use real art), and that the night-market stall has no banner in the game, so `GOOD TIMES SUPPLY` would need a new in-game sign if he wants one shown.
