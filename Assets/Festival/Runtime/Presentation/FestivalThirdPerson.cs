using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>CAM-1: the over-the-shoulder view, a player's own choice (V, or CAMERA VIEW in the settings). FestivalSession eases
    /// the eye exactly as in first person and hangs the camera from it on a boom, set straight from the eye each frame with no
    /// ease of its own, so switching views is a cut. The boom is pulled in by the same colliders that block a cop's sight, so the
    /// camera never sees through a wall. Lying down, a camp visit and the Ferris wheel stay first person (Applies).</summary>
    public static class FestivalThirdPerson
    {
        // The boom: from a pivot just over the eye, back, right and up, turned with the view's yaw and pitch (never its roll).
        const float PivotLift=.1f,Back=2.4f,Right=.45f,Up=.25f;
        // A sphere this wide clears every wall; the camera never comes closer to the pivot than Nearest, or lower than Floor over the
        // festival's flat ground.
        const float Radius=.2f,Nearest=.35f,Floor=.25f;
        // A boom pulled in shorter than this would look out from inside your own head, so your body is hidden again.
        const float InsideHead=.6f;
        // Your own body (FestivalSession puts it on layer 31) and the nameplates (layer 30) never pull the camera in.
        const int Body=1<<31,Labels=1<<30;

        /// <summary>Whether the view goes over the shoulder: when the player wants it, except lying on the grass (TRIP-4 looks up at
        /// the clue cloud), in a camp visit (its rooms are 5.8 m with no wall colliders) and on the Ferris wheel (POLO-5's seated
        /// gondola eye). It comes back on its own afterwards; the setting is never touched.</summary>
        public static bool Applies(bool wanted,bool lying,bool inCampVisit,bool onWheel)=>wanted&&!lying&&!inCampVisit&&!onWheel;

        /// <summary>Where the camera goes for this eye and view, and how long the boom is once walls have pulled it in.</summary>
        public static Vector3 Place(Vector3 eye,float yaw,float pitch,out float boom)
        {
            var pivot=eye+Vector3.up*PivotLift;
            var offset=Quaternion.Euler(pitch,yaw,0)*new Vector3(Right,Up,-Back);
            var direction=offset.normalized;boom=offset.magnitude;
            if(Physics.SphereCast(pivot,Radius,direction,out var hit,boom,~(Body|Labels),QueryTriggerInteraction.Ignore))boom=Mathf.Max(Nearest,hit.distance);
            var camera=pivot+direction*boom;camera.y=Mathf.Max(Floor,camera.y);return camera;
        }

        /// <summary>The view's culling mask for a boom this long (0 in first person): your body shows only on a boom long enough to
        /// see it from outside.</summary>
        public static int Mask(int mask,float boom)=>boom>=InsideHead?mask|Body:mask&~Body;
    }
}
