using System;
using UnityEngine;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>A live third-person camera on the session's world actor and festival.</summary>
    public sealed class FestivalDancePreview : MonoBehaviour
    {
        RawImage image;
        Camera liveCamera;
        RenderTexture texture;
        FestivalCharacter dancer;
        double seconds,beatSeconds;bool reducedMotion;
        public bool IsVisible => liveCamera!=null&&liveCamera.enabled;
        public FestivalCharacter Dancer => dancer;

        public void Initialize(RawImage target)
        {
            image=target;
            texture=new RenderTexture(768,576,24,RenderTextureFormat.ARGB32)
            {name="Live festival dance camera",antiAliasing=2,filterMode=FilterMode.Bilinear};
            texture.Create();image.texture=texture;image.raycastTarget=false;
            var cameraObject=new GameObject("Live festival dance camera");
            cameraObject.transform.SetParent(transform,false);
            liveCamera=cameraObject.AddComponent<Camera>();liveCamera.enabled=false;
        }
        // Shows `actor` `seconds` into a challenge with this beat (DANCE-4: the camera moves with its music).
        public void Show(FestivalCharacter actor,Camera worldCamera,double seconds,double beatSeconds,bool reducedMotion)
        {
            if(actor==null||worldCamera==null){Hide();return;}
            bool changed=dancer!=actor;
            dancer=actor;this.seconds=seconds;this.beatSeconds=beatSeconds;this.reducedMotion=reducedMotion;
            liveCamera.CopyFrom(worldCamera);
            liveCamera.targetTexture=texture;
            liveCamera.cullingMask=(worldCamera.cullingMask|(1<<31))&~(1<<30);
            liveCamera.rect=new Rect(0,0,1,1);
            liveCamera.fieldOfView=48;
            liveCamera.aspect=768f/576f;
            liveCamera.nearClipPlane=.12f;
            liveCamera.enabled=true;
            PositionCamera();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(changed)Debug.Log("[Festival.DanceCamera] Following world actor "+actor.name+" at "+actor.transform.position);
#endif
        }
        void LateUpdate(){if(IsVisible&&dancer!=null)PositionCamera();}
        void PositionCamera()=>Frame(liveCamera.transform,dancer.transform,dancer.Pose=="Dj",seconds,beatSeconds,reducedMotion);
        // DANCE-4: where the camera is `seconds` into a challenge with this beat: a slow orbit of ±20° around the dancer, once every
        // 16 beats, and a push in of up to 5% of the way to them that peaks on each beat (the chart's quarter notes, 2 s + n beats)
        // and eases back. Reduced motion keeps a ±8° drift and never pushes.
        public static (float Yaw,float Push) Path(double seconds,double beatSeconds,bool reducedMotion)
        {
            float yaw=(reducedMotion?8:20)*(float)Math.Sin(2*Math.PI*seconds/(16*beatSeconds));
            if(reducedMotion)return (yaw,0);
            double beats=(seconds-2)/beatSeconds;float sinceBeat=(float)(beats-Math.Floor(beats));
            return (yaw,.05f*(1-sinceBeat)*(1-sinceBeat));
        }
        // Frames the dancer from in front and to the right, along the path; the DJ deck framing holds still.
        public static void Frame(Transform camera,Transform dancer,bool atDeck,double seconds,double beatSeconds,bool reducedMotion)
        {
            var target=dancer.position+Vector3.up*(atDeck?1.55f:1.05f);
            var offset=atDeck
                ? dancer.forward*1.2f+dancer.right*2.4f+Vector3.up*1.1f
                : dancer.forward*3.2f+dancer.right*2.6f+Vector3.up*.65f;
            var path=atDeck?default:Path(seconds,beatSeconds,reducedMotion);
            offset=Quaternion.AngleAxis(path.Yaw,Vector3.up)*offset;
            // Check scenery along the viewing ray so the camera cannot sit inside a wall.
            float distance=offset.magnitude;
            if(Physics.SphereCast(target,.15f,offset.normalized,out var hit,distance,~(1<<31),QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(.65f,hit.distance-.15f);
            // The beat's push dollies in from wherever the wall check left the camera, so a wall always wins.
            distance*=1-path.Push;
            camera.position=target+offset.normalized*distance;
            camera.LookAt(target);
        }
        public void Hide(){if(liveCamera!=null)liveCamera.enabled=false;}
        public void Step(int direction){if(IsVisible&&dancer!=null)dancer.PulseDanceStep(direction);}
        void OnDestroy()
        {
            if(image!=null)image.texture=null;
            if(texture!=null){texture.Release();Destroy(texture);}
            if(liveCamera!=null)Destroy(liveCamera.gameObject);
        }
    }
}
