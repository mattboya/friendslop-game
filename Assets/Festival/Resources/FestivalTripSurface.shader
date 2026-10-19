// TRIP-11: FestivalTripSurfaces redraws the scenery with this. It multiplies what is there (0.5 leaves it alone), so black stays
// black. No keywords and no textures, so one variant ships and the motion comes only from the look.
// _FestivalTripSurface: x Tongue Stamps' lattice (0-1), y Fun Guys' cells (0-1), z drift (metres), w breathing (a share of the size).
Shader "Hidden/Festival/TripSurface"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "FestivalTripSurface"
            ZWrite Off ZTest LEqual Cull Back
            Blend DstColor SrcColor
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _FestivalTripSurface;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // The world plane the face lies closest to, so the pattern sits flat on each surface.
            float2 Plane(float3 p,float3 n){ n=abs(n); return n.y>=max(n.x,n.z)?p.xz:(n.x>=n.z?p.zy:p.xy); }
            float2 Hash2(float2 p){ p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))); return frac(sin(p)*43758.5453); }

            // A triangular lattice: crisp lines in three directions, each its own colour, over alternately tinted triangles.
            float3 Lattice(float2 uv)
            {
                const float2 a=float2(1,0), b=float2(.5,.8660254), c=float2(-.5,.8660254);
                float3 d=float3(dot(uv,a),dot(uv,b),dot(uv,c));
                float3 edge=abs(frac(d+.5)-.5), width=fwidth(d)*1.5+.035;
                float3 lines=1-smoothstep(width*.5,width,edge);
                float fill=frac((floor(d.x)+floor(d.y)+floor(d.z))*.5)*2; // 0 or 1: every other triangle
                float3 tint=lerp(float3(.85,1.1,1.25),float3(1.25,.9,1.1),fill);
                float3 colour=lerp(tint,float3(1.6,.55,.55),lines.x);
                colour=lerp(colour,float3(.55,1.6,.6),lines.y);
                return lerp(colour,float3(.6,.65,1.65),lines.z);
            }

            // Curved cells: the gap between the nearest two of a jittered grid of points, soft and branching.
            float3 Cells(float2 uv)
            {
                float2 g=floor(uv), f=frac(uv); float near=8, next=8;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 o=float2(x,y), r=o+Hash2(g+o)-f; float d=dot(r,r);
                    if(d<near){ next=near; near=d; } else if(d<next) next=d;
                }
                float wall=1-smoothstep(0,.18,sqrt(next)-sqrt(near));
                return lerp(float3(1.12,.92,1.18),float3(.6,1.35,1.3),wall);
            }

            half4 Frag(Varyings input):SV_Target
            {
                float4 look=_FestivalTripSurface;
                float3 n=normalize(input.normalWS);
                // Strong close by and gone by 40 m, so far landmarks keep their shape and the fog its colour.
                float fade=saturate((40-distance(input.positionWS,_WorldSpaceCameraPos))/25);
                float3 lattice=Lattice(Plane(input.positionWS,n)*2.6+look.z*float2(.8,.6));
                float2 cellsAt=Plane(input.positionWS,n)*1.3/(1+look.w*4);
                cellsAt+=.25*sin(cellsAt.yx*1.7+look.w*25);
                float3 cells=Cells(cellsAt);
                float3 multiply=1;
                multiply=lerp(multiply,lattice,look.x*.45*fade);
                multiply=lerp(multiply,cells,look.y*.45*fade);
                // Fun Guys' breathing swells patches of the scenery in turn, before any cells show.
                multiply*=1+look.w*1.2*fade*sin(dot(input.positionWS.xz,float2(.31,.23)));
                return half4(saturate(multiply*.5),1);
            }
            ENDHLSL
        }
    }
}
