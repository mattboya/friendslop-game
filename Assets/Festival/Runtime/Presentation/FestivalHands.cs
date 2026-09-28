using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Camera-local arms; only presentation, never authoritative physics.</summary>
    public sealed class FestivalHands : MonoBehaviour
    {
        Material material;
        Texture2D palette;
        Vector3 rest;
        FestivalPoiRig leftPoi,rightPoi,equippedPoi;
        GameObject carriedBand;
        GameObject unpaidProp,equippedProp;
        string unpaidId="",equippedId="";
        int previousCash=-1;
        float handoffAt=-10;
        public static FestivalHands Create(Camera camera,string playerId)
        {
            var prefab=Resources.Load<GameObject>("FestivalHands");
            if(prefab==null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Festival.Art] First-person Blender hands asset is missing.");
#endif
                return null;
            }
            var go=Instantiate(prefab,camera.transform);
            go.name="First-person festival hands";
            go.transform.localPosition=new Vector3(0,-.28f,.35f);
            go.transform.localRotation=Quaternion.identity;
            go.transform.localScale=Vector3.one*.56f;
            var hands=go.AddComponent<FestivalHands>();
            var look=FestivalAppearance.For(playerId);
            var template=Resources.Load<Material>("FestivalLit");
            if(template==null)template=new Material(Shader.Find("Standard"));
            hands.material=new Material(template){name="First-person festival palette"};
            hands.material.SetFloat("_Smoothness",.18f);
            hands.palette=look.CreatePalette();hands.material.mainTexture=hands.palette;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial=hands.material;
                renderer.enabled=renderer.name=="HandsSkin_"+look.Shape || renderer.name=="HandsSleeve_"+look.Shirt;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hands.rest=go.transform.localPosition;
            hands.leftPoi=FestivalPoiRig.Create(camera.transform,new Vector3(-.24f,-.48f,.72f),0,true,true);
            hands.rightPoi=FestivalPoiRig.Create(camera.transform,new Vector3(.24f,-.48f,.72f),1,true,true);
            hands.carriedBand=Held(go.transform,"FestivalWristband",new Vector3(.30f,-.36f,.84f),.32f);
            hands.SetState(null);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Festival.Art] hands shape={look.Shape} sleeves={look.Shirt} seed={FestivalAppearance.Pick(playerId,"appearance",10000)}");
#endif
            return hands;
        }
        static GameObject Held(Transform parent,string resource,Vector3 position,float scale)
        {
            var prop=FestivalArtView.Create(parent,resource);if(prop==null)return null;
            prop.transform.localPosition=position;prop.transform.localRotation=resource=="FestivalStock"?Quaternion.Euler(0,180,0):Quaternion.identity;prop.transform.localScale=Vector3.one*scale;
            return prop;
        }
        public void SetState(PlayerState player)
        {
            string held=player?.HeldOfferId??"";
            if(held!=unpaidId)
            {
                if(unpaidId!=""&&held==""&&player!=null&&previousCash>=0&&player.Cash<previousCash)handoffAt=Time.time;
                if(unpaidProp!=null)Destroy(unpaidProp);
                unpaidProp=null;unpaidId=held;
                if(held!="")
                {
                    var resource=FestivalSession.DropModel(held);
                    if(resource!=null)unpaidProp=Held(transform,resource,new Vector3(.38f,-.14f,1.0f),.42f);
                }
            }
            string equipped=player?.EquippedItemId??"";
            if(equipped=="little_spoon")equipped=""; // Passive neck item, never a hand prop.
            if(equipped!=equippedId)
            {
                if(equippedProp!=null)Destroy(equippedProp);
                if(equippedPoi!=null)Destroy(equippedPoi.gameObject);
                equippedProp=null;equippedId=equipped;
                if(equipped=="poi_led"||equipped=="poi_practice")equippedPoi=FestivalPoiRig.Create(transform,new Vector3(.42f,-.28f,.90f),1,equipped=="poi_led",true);
                else
                {
                    var resource=FestivalSession.DropModel(equipped);
                    if(resource!=null)equippedProp=Held(transform,resource,new Vector3(.42f,-.36f,.90f),.42f);
                }
            }
            bool poi=player!=null && player.VisualPose=="Poi";
            if(leftPoi!=null){leftPoi.gameObject.SetActive(poi);leftPoi.Spinning=poi;}
            if(rightPoi!=null){rightPoi.gameObject.SetActive(poi);rightPoi.Spinning=poi;}
            if(carriedBand!=null)carriedBand.SetActive(player!=null && player.Wristbands.Count>0 && !poi && held=="");
            if(equippedProp!=null)equippedProp.SetActive(held==""&&!poi&&player.Life=="Alive");
            if(equippedPoi!=null)equippedPoi.gameObject.SetActive(held==""&&!poi&&player.Life=="Alive");
            previousCash=player?.Cash??-1;
        }
        void LateUpdate()
        {
            // Small, non-authoritative breathing motion. Hands never move gameplay targets.
            float handoff=Mathf.Clamp01(1-(Time.time-handoffAt)/.38f);
            transform.localPosition=rest+new Vector3(Mathf.Sin(Time.time*1.7f)*.006f,Mathf.Sin(Time.time*2.1f)*.008f,.16f*Mathf.Sin((1-handoff)*Mathf.PI)*handoff);
        }
        void OnDestroy()
        {
            if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            if(palette!=null){if(Application.isPlaying)Destroy(palette);else DestroyImmediate(palette);}
        }
    }
}
