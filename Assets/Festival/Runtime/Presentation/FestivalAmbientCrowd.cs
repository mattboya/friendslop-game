using System.Collections.Generic;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Local visual festival life. Interactive NPCs remain host authoritative.</summary>
    public sealed class FestivalAmbientCrowd : MonoBehaviour
    {
        public readonly struct Spot
        {
            public readonly string Zone,Pose;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public Spot(string zone,string pose,float x,float z,float yaw)
            {Zone=zone;Pose=pose;Position=new Vector3(x,0,z);Yaw=yaw;}
        }
        sealed class Walker
        {
            public FestivalCharacter Actor;
            public Vector3[] Points;
            public float[] Lengths;
            public float Distance,Speed,Total;
        }
        struct CrowdMember
        {
            public FestivalCharacter Actor;
            public float WakeDistance;
        }
        static readonly Spot[] fixedSpots=MakeSpots();
        readonly List<CrowdMember> members=new List<CrowdMember>(62);
        readonly List<Walker> walkers=new List<Walker>(12);
        float nextVisibilityCheck;
        public static IReadOnlyList<Spot> FixedSpots=>fixedSpots;
        public int MemberCount=>members.Count;
        public int WalkerCount=>walkers.Count;
        public Vector3 FirstWalkerPosition=>walkers.Count==0?Vector3.zero:walkers[0].Actor.transform.position;

        static Spot[] MakeSpots()
        {
            var spots=new List<Spot>(50);
            // Loose friends' pods: mixed sizes, several open pockets, and an
            // unobstructed central aisle to the DJ interaction at (0, 26).
            float[] stage={
                -3.3f,25.1f,-4.0f,25.7f,-4.7f,24.9f,-3.5f,24.0f,-5.1f,23.8f,
                -6.8f,22.0f,-7.5f,21.4f,-6.0f,20.9f,-7.1f,20.1f,
                -4.1f,18.6f,-5.0f,17.9f,-3.5f,17.2f,-5.6f,18.9f,
                3.3f,25.5f,4.2f,24.9f,5.0f,25.3f,3.4f,24.4f,4.7f,23.8f,5.7f,24.5f,
                6.5f,21.6f,7.3f,20.9f,6.0f,20.4f,
                3.5f,18.4f,4.3f,18.0f,5.1f,17.4f,3.4f,17.2f
            };
            for(int i=0;i<stage.Length;i+=2)
                spots.Add(new Spot("Stage",i%8==0?"Watching":"Dance",stage[i],stage[i+1],(i*29)%45-22));
            // Poi circles sit behind the dense audience when approaching the stage.
            spots.Add(new Spot("PoiPerformer","Poi",-13.0f,12.1f,8));
            spots.Add(new Spot("PoiPerformer","Poi",13.0f,12.0f,-12));
            foreach(var point in new[]{
                new Vector2(-15.0f,10.7f),new Vector2(-15.2f,14.2f),new Vector2(-11.1f,12.9f),new Vector2(-12.0f,14.4f),
                new Vector2(15.0f,10.8f),new Vector2(14.9f,14.2f),new Vector2(11.2f,12.9f),new Vector2(12.1f,14.5f)})
            {
                var focus=new Vector2(point.x<0?-13:13,12);
                float yaw=Mathf.Atan2(focus.x-point.x,focus.y-point.y)*Mathf.Rad2Deg;
                spots.Add(new Spot("PoiAudience","Watching",point.x,point.y,yaw));
            }
            foreach(var point in new[]{
                new Vector2(-21.8f,12.6f),new Vector2(-25.4f,12.8f),new Vector2(-22.0f,8.5f),new Vector2(-27.0f,11.8f),
                new Vector2(21.0f,15.6f),new Vector2(25.5f,14.6f),new Vector2(20.3f,11.7f),new Vector2(26.2f,12.8f)})
            {
                float yaw=point.x<0?75:-75;
                spots.Add(new Spot("TreeHangout","Watching",point.x,point.y,yaw));
            }
            foreach(var point in new[]{
                new Vector2(-26.4f,1.9f),new Vector2(-21.9f,2.4f),new Vector2(-24.4f,4.1f),
                new Vector2(19.7f,-10.0f),new Vector2(24.4f,-10.5f),new Vector2(22.0f,-7.7f)})
            {
                float yaw=point.x<0?90:-90;
                spots.Add(new Spot("Picnic","Idle",point.x,point.y,yaw));
            }
            return spots.ToArray();
        }

        public void Build()
        {
            if(members.Count!=0)return;
            for(int i=0;i<fixedSpots.Length;i++)
            {
                var spot=fixedSpots[i];
                var actor=FestivalCharacter.Create(transform,"ambient_"+spot.Zone+"_"+i,Color.white);
                actor.AmbientCrowd=true;
                actor.Pose=spot.Pose;
                actor.transform.localPosition=spot.Position;
                actor.transform.localRotation=Quaternion.Euler(0,spot.Yaw,0);
                actor.transform.localScale=Vector3.Scale(Vector3.one*(spot.Zone=="Stage"?.78f:.82f),actor.ShapeScale);
                members.Add(new CrowdMember{Actor=actor,WakeDistance=spot.Zone=="Stage"?50:44});
            }
            AddRoute("main_west",new[]{new Vector3(-2.4f,0,-30),new Vector3(-2.4f,0,14),
                new Vector3(-3.6f,0,14),new Vector3(-3.6f,0,-30)},4,1.42f);
            AddRoute("main_east",new[]{new Vector3(2.5f,0,-30),new Vector3(2.5f,0,14),
                new Vector3(3.6f,0,14),new Vector3(3.6f,0,-30)},4,1.31f);
            AddRoute("market_lane",new[]{new Vector3(-4.5f,0,-22),new Vector3(-21.8f,0,-22),
                new Vector3(-21.8f,0,-15.5f),new Vector3(-5.0f,0,-15.5f)},2,1.17f);
            AddRoute("east_lane",new[]{new Vector3(4.5f,0,-4),new Vector3(22.8f,0,-4),
                new Vector3(22.8f,0,-9.0f),new Vector3(5.1f,0,-9.0f)},2,1.24f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Festival.Crowd] " + members.Count + " ambient attendees, " + walkers.Count + " walkers, " + fixedSpots.Length + " social placements.");
#endif
        }
        void AddRoute(string name,Vector3[] points,int count,float speed)
        {
            var lengths=new float[points.Length];float total=0;
            for(int i=0;i<points.Length;i++){lengths[i]=Vector3.Distance(points[i],points[(i+1)%points.Length]);total+=lengths[i];}
            for(int i=0;i<count;i++)
            {
                var actor=FestivalCharacter.Create(transform,"ambient_walk_"+name+"_"+i,Color.white);
                actor.AmbientCrowd=true;
                actor.Pose="Idle";
                actor.transform.localScale=Vector3.Scale(Vector3.one*.82f,actor.ShapeScale);
                // Two people sometimes arrive together; the next gap is much
                // larger. The lanes do not look like evenly spaced patrols.
                float[] intervals=count==4?new[]{.04f,.12f,.54f,.81f}:new[]{.08f,.19f};
                float offset=name=="main_east"?.16f:name=="east_lane"?.31f:0;
                var walker=new Walker{Actor=actor,Points=points,Lengths=lengths,Total=total,
                    Distance=total*Mathf.Repeat(intervals[i]+offset,1),Speed=speed*(.91f+i%3*.07f)};
                walkers.Add(walker);
                members.Add(new CrowdMember{Actor=actor,WakeDistance=44});
                Move(walker,0);
            }
        }
        static void Move(Walker walker,float elapsed)
        {
            float distance=Mathf.Repeat(walker.Distance+elapsed*walker.Speed,walker.Total);
            int segment=0;
            while(segment<walker.Lengths.Length-1&&distance>walker.Lengths[segment])
                distance-=walker.Lengths[segment++];
            int count=walker.Points.Length;
            var from=walker.Points[segment];var to=walker.Points[(segment+1)%count];
            var incoming=(to-from).normalized;
            float length=walker.Lengths[segment];
            Vector3 position,direction;
            float startRadius=Mathf.Min(1.15f,Mathf.Min(walker.Lengths[(segment+count-1)%count],length)*.24f);
            float endRadius=Mathf.Min(1.15f,Mathf.Min(length,walker.Lengths[(segment+1)%count])*.24f);
            if(distance<startRadius)
            {
                var before=(from-walker.Points[(segment+count-1)%count]).normalized;
                float t=.5f+distance/(2*startRadius);
                position=Corner(from,before,incoming,startRadius,t);
                direction=Vector3.Lerp(before,incoming,t).normalized;
            }
            else if(distance>length-endRadius)
            {
                var outgoing=(walker.Points[(segment+2)%count]-to).normalized;
                float t=(distance-(length-endRadius))/(2*endRadius);
                position=Corner(to,incoming,outgoing,endRadius,t);
                direction=Vector3.Lerp(incoming,outgoing,t).normalized;
            }
            else
            {
                position=Vector3.Lerp(from,to,distance/length);
                direction=incoming;
            }
            walker.Actor.transform.localPosition=position;
            walker.Actor.transform.localRotation=Quaternion.LookRotation(direction);
        }
        static Vector3 Corner(Vector3 vertex,Vector3 incoming,Vector3 outgoing,float radius,float t)
        {
            var a=vertex-incoming*radius;
            var c=vertex+outgoing*radius;
            float inverse=1-t;
            return inverse*inverse*a+2*inverse*t*vertex+t*t*c;
        }
        void Update()
        {
            float now=Time.time;
            for(int i=0;i<walkers.Count;i++)Move(walkers[i],now);
            if(now<nextVisibilityCheck)return;
            nextVisibilityCheck=now+.25f;
            var view=FestivalCharacter.ViewTransform;
            if(view==null)return;
            var eye=view.position;
            var forward=view.forward;
            for(int i=0;i<members.Count;i++)
            {
                var member=members[i];
                var actor=member.Actor;
                if(actor==null)continue;
                float range=actor.gameObject.activeSelf?member.WakeDistance+3:member.WakeDistance;
                var offset=actor.transform.position-eye;
                bool visible=offset.sqrMagnitude<range*range;
                // Keep nearby companions alive while a player turns, but skip
                // animation and skinning for the crowd well behind the view.
                if(visible&&offset.sqrMagnitude>64f)
                {
                    float facing=Vector3.Dot(forward,offset.normalized);
                    visible=facing>(actor.gameObject.activeSelf?-.25f:-.05f);
                }
                if(actor.gameObject.activeSelf!=visible)actor.gameObject.SetActive(visible);
            }
        }
    }
}
