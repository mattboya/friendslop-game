"""Generate an original, seamless festival-grass color map without dependencies."""
import bpy, math, pathlib
out=pathlib.Path(__file__).resolve().parents[1]/'Assets/Festival/Art/Resources/FestivalGround.png'
size=512
def hash2(x,y,seed):
 n=(x*374761393+y*668265263+seed*1442695041)&0xffffffff
 n=(n^(n>>13))*1274126177&0xffffffff
 return ((n^(n>>16))&0xffff)/65535
def noise(x,y,scale,seed):
 # A whole number of lattice cells per tile, wrapped, so the edges match.
 cells=max(1,round(size/scale));step=size/cells
 gx=x/step;gy=y/step;ix=math.floor(gx);iy=math.floor(gy)
 fx=gx-ix;fy=gy-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
 x0,x1,y0,y1=ix%cells,(ix+1)%cells,iy%cells,(iy+1)%cells
 a=hash2(x0,y0,seed)*(1-fx)+hash2(x1,y0,seed)*fx
 b=hash2(x0,y1,seed)*(1-fx)+hash2(x1,y1,seed)*fx
 return a*(1-fy)+b*fy
pixels=[]
for y in range(size):
 for x in range(size):
  broad=noise(x,y,64,1);fine=noise(x,y,12,7);grain=hash2(x,y,19)
  shade=(broad-.5)*.11+(fine-.5)*.08+(grain-.5)*.035
  dry=noise(x,y,96,11)>.68
  base=(.29,.40,.31) if not dry else (.38,.40,.28)
  pixels.extend((base[0]+shade,base[1]+shade,base[2]+shade*.7,1))
image=bpy.data.images.new('FestivalGround',width=size,height=size)
image.pixels=pixels;image.filepath_raw=str(out);image.file_format='PNG';image.save()
print('FESTIVAL GROUND GENERATED',out)
