using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Visual-only support and swing feet. Never changes the networked actor root.</summary>
    internal sealed class FestivalFootPlant
    {
        const float StanceFraction=.54f;
        sealed class Foot
        {
            public readonly Transform Thigh,Shin,Ankle;
            public readonly Vector3 RestInActor;
            public readonly Quaternion RestRotationInActor;
            public Vector3 Anchor,SwingFrom,SwingTo;
            public Quaternion AnchorRotation,SwingFromRotation,SwingToRotation;
            public bool Initialized,WasStance;
            public Foot(Transform actor,Transform thigh,Transform shin,Transform ankle)
            {
                Thigh=thigh;Shin=shin;Ankle=ankle;
                RestInActor=actor.InverseTransformPoint(ankle.position);
                RestRotationInActor=Quaternion.Inverse(actor.rotation)*ankle.rotation;
            }
            public void Reset(){Initialized=false;}
        }

        readonly Transform actor,hips;
        readonly Vector3 restHips;
        readonly Foot left,right;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool warnedReach;
        bool loggedRouteJump;
#endif

        public FestivalFootPlant(Transform actor,Transform hips,Transform leftThigh,Transform leftShin,
            Transform leftFoot,Transform rightThigh,Transform rightShin,Transform rightFoot)
        {
            this.actor=actor;this.hips=hips;restHips=hips.localPosition;
            left=new Foot(actor,leftThigh,leftShin,leftFoot);
            right=new Foot(actor,rightThigh,rightShin,rightFoot);
        }

        public void Update(bool walking,Vector3 displacement,float speed,float cycle,float strideDistance,float dt)
        {
            if(!walking||displacement.sqrMagnitude>.04f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(walking&&displacement.sqrMagnitude>.04f&&(left.Initialized||right.Initialized)&&!loggedRouteJump)
                {
                    loggedRouteJump=true;
                    Debug.Log("[Festival.Motion] action=footplant_reset actor="+actor.name+" reason=route_jump");
                }
#endif
                left.Reset();right.Reset();
                hips.localPosition=Vector3.Lerp(hips.localPosition,restHips,1-Mathf.Exp(-12f*dt));
                return;
            }
            float pace=Mathf.Clamp01((speed-1.4f)/2.4f);
            float planted=Mathf.Pow(Mathf.Abs(Mathf.Sin(cycle)),2);
            // Imported bone parents carry a rotated 100x transform. Convert
            // the intended world bob through that parent before editing local position.
            float scale=actor.lossyScale.y;
            var bob=actor.right*(Mathf.Sin(cycle)*.018f*scale)+
                Vector3.down*((.085f+.015f*pace-planted*.010f)*scale);
            var pelvis=restHips+hips.parent.InverseTransformVector(bob);
            hips.localPosition=Vector3.Lerp(hips.localPosition,pelvis,1-Mathf.Exp(-16f*dt));
            var direction=Vector3.ProjectOnPlane(displacement,Vector3.up);
            if(direction.sqrMagnitude<.00001f)direction=actor.forward;
            direction.Normalize();
            Step(left,Mathf.Repeat(cycle/(2*Mathf.PI),1f),direction,strideDistance);
            Step(right,Mathf.Repeat(cycle/(2*Mathf.PI)+.5f,1f),direction,strideDistance);
        }

        void Step(Foot foot,float phase,Vector3 direction,float strideDistance)
        {
            bool stance=phase<StanceFraction;
            if(!foot.Initialized)
            {
                foot.Anchor=foot.Ankle.position;
                foot.Anchor.y=GroundAnkleHeight(foot);
                foot.AnchorRotation=foot.Ankle.rotation;
                foot.SwingFrom=foot.Anchor;foot.SwingFromRotation=foot.AnchorRotation;
                PlanLanding(foot,direction,strideDistance,phase);
                foot.WasStance=stance;foot.Initialized=true;
            }
            else if(stance&&!foot.WasStance)
            {
                foot.Anchor=foot.SwingTo;
                foot.AnchorRotation=foot.SwingToRotation;
            }
            else if(!stance&&foot.WasStance)
            {
                foot.SwingFrom=foot.Anchor;
                foot.SwingFromRotation=foot.AnchorRotation;
                PlanLanding(foot,direction,strideDistance,phase);
            }
            else if(!stance)
            {
                // Route turns change the predicted touchdown while the shoe
                // is airborne. Ease the target to the new walking direction.
                var previous=foot.SwingTo;
                var previousRotation=foot.SwingToRotation;
                PlanLanding(foot,direction,strideDistance,phase);
                foot.SwingTo=Vector3.Lerp(previous,foot.SwingTo,.25f);
                foot.SwingToRotation=Quaternion.Slerp(previousRotation,foot.SwingToRotation,.25f);
            }
            if(stance&&Vector3.Distance(foot.Anchor,foot.Thigh.position)>
                Vector3.Distance(foot.Thigh.position,foot.Shin.position)+
                Vector3.Distance(foot.Shin.position,foot.Ankle.position)-.02f)
            {
                // Navigation can skip across a corner without a stride event.
                // Replant rather than drag a shoe across the ground.
                foot.Anchor=foot.Ankle.position;
                foot.Anchor.y=GroundAnkleHeight(foot);
                foot.AnchorRotation=foot.Ankle.rotation;
            }
            foot.WasStance=stance;
            Vector3 target=foot.Anchor;
            Quaternion orientation=foot.AnchorRotation;
            if(!stance)
            {
                float u=Mathf.Clamp01((phase-StanceFraction)/(1f-StanceFraction));
                float smooth=u*u*(3-2*u);
                target=Vector3.Lerp(foot.SwingFrom,foot.SwingTo,smooth)+
                    Vector3.up*(.11f*actor.lossyScale.y*Mathf.Sin(u*Mathf.PI));
                orientation=Quaternion.Slerp(foot.SwingFromRotation,foot.SwingToRotation,smooth);
            }
            SolveLeg(foot,target);
            foot.Ankle.rotation=orientation;
        }

        void PlanLanding(Foot foot,Vector3 direction,float strideDistance,float phase)
        {
            float scale=actor.lossyScale.y;
            Vector3 lane=actor.right*(foot.RestInActor.x*scale);
            foot.SwingTo=actor.position+lane+direction*(strideDistance*(1f-phase)+.21f*scale);
            foot.SwingTo.y=GroundAnkleHeight(foot);
            foot.SwingToRotation=actor.rotation*foot.RestRotationInActor;
        }

        float GroundAnkleHeight(Foot foot)=>actor.TransformPoint(new Vector3(0,foot.RestInActor.y,0)).y;

        void SolveLeg(Foot foot,Vector3 target)
        {
            var hip=foot.Thigh.position;
            float upper=Vector3.Distance(hip,foot.Shin.position);
            float lower=Vector3.Distance(foot.Shin.position,foot.Ankle.position);
            if(upper<.01f||lower<.01f)return;
            var offset=target-hip;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!warnedReach&&offset.magnitude>upper+lower+.10f)
            {
                warnedReach=true;
                Debug.LogWarning("[Festival.Motion] action=footplant_reach actor="+actor.name+
                    " excess="+(offset.magnitude-upper-lower).ToString("F3")+
                    " upper="+upper.ToString("F3")+" lower="+lower.ToString("F3")+
                    " target="+target.ToString("F2")+" hip="+hip.ToString("F2"));
            }
#endif
            float distance=Mathf.Clamp(offset.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.002f);
            var axis=offset.sqrMagnitude>.000001f?offset.normalized:Vector3.down;
            var pole=Vector3.ProjectOnPlane(actor.forward,axis).normalized;
            if(pole.sqrMagnitude<.000001f)pole=Vector3.ProjectOnPlane(actor.right,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2f*distance);
            float across=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var knee=hip+axis*along+pole*across;
            var ankle=hip+axis*distance;
            foot.Thigh.rotation=Quaternion.FromToRotation(foot.Shin.position-hip,knee-hip)*foot.Thigh.rotation;
            foot.Shin.rotation=Quaternion.FromToRotation(foot.Ankle.position-foot.Shin.position,ankle-foot.Shin.position)*foot.Shin.rotation;
        }
    }
}
