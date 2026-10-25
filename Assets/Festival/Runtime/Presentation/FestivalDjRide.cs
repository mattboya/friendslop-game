using System.Collections.Generic;
using Festival.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>The DJ set's three-lane highway, drawn as one HUD mesh from code because the art is frozen. Blocks reach the marker
    /// on the chart the host judges (DjRide).</summary>
    public sealed class FestivalDjRide : MaskableGraphic
    {
        /// <summary>How far down the stage view looks from behind and above the DJ, out over the crowd.</summary>
        public const float ViewPitch=16;
        // The road in the rect's own 0-1 space: the horizon's height, the half-width at the near edge, the marker's height, and how
        // many times nearer the marker is than the horizon (the perspective). The strip under the marker holds the HUD's steer and stop line.
        const float Horizon=.94f,NearHalf=.42f,MarkerY=.17f,Depth=6;
        static readonly Color Road=new Color(.06f,.08f,.14f,.82f),Edge=new Color(1,1,1,.3f),GreyBlock=new Color(.58f,.58f,.62f,1),Marker=new Color(1,.93f,.68f,1);
        static readonly Color[] LaneColors={new Color(.19f,1f,.76f,1),new Color(1f,.75f,.25f,1),new Color(1f,.3f,.58f,1)};
        RhythmChart chart;
        double now,lead=2;
        int lane=DjRide.StartLane;

        /// <summary>The lane after a left or right press, kept on the road; both at once cancel.</summary>
        public static int Steer(int lane,bool left,bool right)=>Mathf.Clamp(lane+(right?1:0)-(left?1:0),0,DjRide.Lanes-1);
        /// <summary>Where a note in `noteLane`, `remaining` seconds out, sits on the road: x across and y up the rect (0-1), and z its
        /// size, 1 at the marker and smaller toward the horizon, where a note `lead` seconds out appears.</summary>
        public static Vector3 Place(int noteLane,double remaining,double lead)
        {
            float u=Mathf.Clamp01((float)(remaining/lead)),scale=1/(1+u*(Depth-1));
            float y=MarkerY+(Horizon-MarkerY)*(1-scale)/(1-1/Depth),x=.5f+(noteLane-1)*(2*NearHalf/DjRide.Lanes)*scale;
            return new Vector3(x,y,scale);
        }
        /// <summary>The notes on the road now: due within the look-ahead and not yet a good window past the marker.</summary>
        public static IEnumerable<RhythmNote> OnRoad(RhythmChart chart,double now,double lead)
        {
            if(chart==null)yield break;
            foreach(var note in chart.Notes){double remaining=note.TimeSeconds-now;if(remaining>=-.15&&remaining<=lead)yield return note;}
        }
        public void Show(RhythmChart chart,double now,int lane,double lead){this.chart=chart;this.now=now;this.lane=lane;this.lead=lead;SetVerticesDirty();}

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
            {
                int i=vh.currentVertCount;
                vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
            float far=NearHalf/Depth;
            Quad(P(.5f-NearHalf,0),P(.5f-far,Horizon),P(.5f+far,Horizon),P(.5f+NearHalf,0),Road);
            for(int k=0;k<=DjRide.Lanes;k++)
            {
                float near=.5f-NearHalf+k*2*NearHalf/DjRide.Lanes,top=.5f-far+k*2*far/DjRide.Lanes;
                Quad(P(near-.003f,0),P(top-.001f,Horizon),P(top+.001f,Horizon),P(near+.003f,0),Edge);
            }
            foreach(var note in OnRoad(chart,now,lead))
            {
                var at=Place(note.Direction,note.TimeSeconds-now,lead);float w=NearHalf/DjRide.Lanes*.8f*at.z,h=.05f*at.z;
                Quad(P(at.x-w,at.y-h),P(at.x-w,at.y+h),P(at.x+w,at.y+h),P(at.x+w,at.y-h),note.Grey?GreyBlock:LaneColors[Mathf.Clamp(note.Direction,0,DjRide.Lanes-1)]);
            }
            var marker=Place(lane,0,lead);float half=NearHalf/DjRide.Lanes*.9f;
            Quad(P(marker.x-half,MarkerY-.07f),P(marker.x-half*.55f,MarkerY+.02f),P(marker.x+half*.55f,MarkerY+.02f),P(marker.x+half,MarkerY-.07f),Marker);
        }
    }
}
