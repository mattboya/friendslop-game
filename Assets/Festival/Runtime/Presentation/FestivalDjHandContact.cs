using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Visual-only two-bone hand contact with a DJ console.</summary>
    internal sealed class FestivalDjHandContact
    {
        readonly Transform actor,console;
        readonly Transform leftArm,leftForearm,leftHand,rightArm,rightForearm,rightHand;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool warnedReach;
#endif

        public FestivalDjHandContact(Transform actor,Transform console,
            Transform leftArm,Transform leftForearm,Transform leftHand,
            Transform rightArm,Transform rightForearm,Transform rightHand)
        {
            this.actor=actor;this.console=console;
            this.leftArm=leftArm;this.leftForearm=leftForearm;this.leftHand=leftHand;
            this.rightArm=rightArm;this.rightForearm=rightForearm;this.rightHand=rightHand;
        }

        public void Update(float beat)
        {
            // The targets stay on the rear half of the actual mixer surface.
            // Tiny alternating motions suggest knob and fader work without
            // slipping the hands off the equipment.
            var left=console.TransformPoint(new Vector3(-.46f+Mathf.Sin(beat)*.055f,.81f,.36f));
            var right=console.TransformPoint(new Vector3(.38f+Mathf.Sin(beat+1.7f)*.065f,.81f,.33f));
            Solve(leftArm,leftForearm,leftHand,left,-1);
            Solve(rightArm,rightForearm,rightHand,right,1);
        }

        void Solve(Transform arm,Transform forearm,Transform hand,Vector3 target,int side)
        {
            Vector3 shoulder=arm.position;
            float upper=Vector3.Distance(shoulder,forearm.position);
            float lower=Vector3.Distance(forearm.position,hand.position);
            if(upper<.01f||lower<.01f)return;
            var offset=target-shoulder;
            float desired=offset.magnitude;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!warnedReach&&desired>upper+lower+.045f)
            {
                warnedReach=true;
                Debug.LogWarning("[Festival.Motion] action=dj_hand_reach actor="+actor.name+
                    " excess="+(desired-upper-lower).ToString("F3"));
            }
#endif
            float distance=Mathf.Clamp(desired,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            Vector3 axis=desired>.00001f?offset/desired:actor.forward;
            Vector3 pole=Vector3.ProjectOnPlane(Vector3.down+actor.right*(side*.35f),axis).normalized;
            if(pole.sqrMagnitude<.000001f)pole=Vector3.ProjectOnPlane(actor.right,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2f*distance);
            float across=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 elbow=shoulder+axis*along+pole*across;
            Vector3 wrist=shoulder+axis*distance;
            arm.rotation=Quaternion.FromToRotation(forearm.position-shoulder,elbow-shoulder)*arm.rotation;
            forearm.rotation=Quaternion.FromToRotation(hand.position-forearm.position,wrist-forearm.position)*forearm.rotation;
        }
    }
}
