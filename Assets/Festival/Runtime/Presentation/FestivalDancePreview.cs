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
        public void Show(FestivalCharacter actor,Camera worldCamera)
        {
            if(actor==null||worldCamera==null){Hide();return;}
            bool changed=dancer!=actor;
            dancer=actor;
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
        void PositionCamera()
        {
            var target=dancer.transform.position+Vector3.up*1.05f;
            var offset=dancer.transform.forward*3.2f+dancer.transform.right*2.6f+Vector3.up*.65f;
            // Check scenery along the viewing ray so the camera cannot sit inside a wall.
            float distance=offset.magnitude;
            if(Physics.SphereCast(target,.15f,offset.normalized,out var hit,distance,~(1<<31),QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(.65f,hit.distance-.15f);
            liveCamera.transform.position=target+offset.normalized*distance;
            liveCamera.transform.LookAt(target);
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
