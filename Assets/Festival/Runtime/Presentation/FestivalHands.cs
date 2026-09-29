using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Camera-local arms; only presentation, never authoritative physics.</summary>
    public sealed class FestivalHands : MonoBehaviour
    {
        static readonly Vector3 RightPalm=new Vector3(.18f,-.35f,.82f);
        Material material;
        Texture2D palette;
        Vector3 rest,previousCameraPosition;
        float previousCameraYaw,walkPhase,movement,turnSway,restLeft=1,restRight=1;
        bool raiseLeft,raiseRight,presenting;
        SkinnedMeshRenderer[] restMeshes;
        int[] restLeftIndices,restRightIndices;
        int receiptSequence;
        float receiptAt=-10;
        string receiptItem="";
        static readonly Vector3 LoweredGrip=new Vector3(.0336f,-.309f,.025f);
        static readonly string[] GripNames={"RodL","RodR","BagR","TinR","PaperR"};
        readonly int[] gripIndices={-1,-1,-1,-1,-1};
        readonly float[] gripTargets=new float[5];
        SkinnedMeshRenderer gripSkin;
        string gripState="";
        Transform attachments;
        FestivalPoiRig leftPoi,rightPoi,equippedPoi,unpaidPoi;
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
            go.transform.localPosition=new Vector3(0,-.20f,.35f);
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
            foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin.name=="HandsSkin_"+look.Shape)hands.gripSkin=skin;
            if(hands.gripSkin!=null)
                for(int shape=0;shape<hands.gripSkin.sharedMesh.blendShapeCount;shape++)
                    for(int grip=0;grip<GripNames.Length;grip++)
                        if(hands.gripSkin.sharedMesh.GetBlendShapeName(shape).EndsWith(GripNames[grip]))hands.gripIndices[grip]=shape;
            hands.restMeshes=System.Array.FindAll(go.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.enabled);
            hands.restLeftIndices=new int[hands.restMeshes.Length];hands.restRightIndices=new int[hands.restMeshes.Length];
            for(int i=0;i<hands.restMeshes.Length;i++)
            {
                hands.restLeftIndices[i]=-1;hands.restRightIndices[i]=-1;
                for(int j=0;j<hands.restMeshes[i].sharedMesh.blendShapeCount;j++)
                {
                    var key=hands.restMeshes[i].sharedMesh.GetBlendShapeName(j);
                    if(key.EndsWith("RestL"))hands.restLeftIndices[i]=j;
                    if(key.EndsWith("RestR"))hands.restRightIndices[i]=j;
                }
            }
            hands.previousCameraPosition=camera.transform.position;hands.previousCameraYaw=camera.transform.eulerAngles.y;
            hands.rest=go.transform.localPosition;
            // Authored finger grips target source x=+/-.32, z=-.268, forward=.84;
            // the .56 hand scale and camera offset put them here in camera space.
            hands.attachments=new GameObject("First-person hand attachments").transform;
            hands.attachments.SetParent(camera.transform,false);
            hands.leftPoi=FestivalPoiRig.Create(hands.attachments,new Vector3(-RightPalm.x,RightPalm.y,RightPalm.z),0,true,true);
            hands.rightPoi=FestivalPoiRig.Create(hands.attachments,RightPalm,1,true,true);
            hands.carriedBand=Held(hands.attachments,"FestivalWristband",RightPalm+new Vector3(.01f,-.04f,-.08f),.18f);
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
        public void SetState(PlayerState player,double simulationSeconds=-1,bool receiving=false)
        {
            if(player!=null&&receiptSequence!=player.VisualReceiptSequence)
            {
                receiptSequence=player.VisualReceiptSequence;receiptItem=player.VisualReceivedItem;
                float age=simulationSeconds<0?0:(float)(simulationSeconds-player.VisualReceiptAt);
                receiptAt=age>=0&&age<1?Time.time-age:-10;
            }
            string held=player?.HeldOfferId??"";
            if(player!=null&&!string.IsNullOrEmpty(player.VisualOfferItem))held=player.VisualOfferItem;
            if(held!=unpaidId)
            {
                if(unpaidId!=""&&held==""&&player!=null&&previousCash>=0&&player.Cash<previousCash)handoffAt=Time.time;
                if(unpaidProp!=null)Destroy(unpaidProp);
                if(unpaidPoi!=null)Destroy(unpaidPoi.gameObject);
                unpaidProp=null;unpaidPoi=null;unpaidId=held;
                if(held!=""&&held!="little_spoon")
                {
                    if(held=="poi_led"||held=="poi_practice")
                        unpaidPoi=FestivalPoiRig.Create(attachments,RightPalm,1,held=="poi_led",true);
                    else
                    {
                        var resource=FestivalSession.DropModel(held);
                        if(resource!=null)unpaidProp=FestivalHeldItem.Create(attachments,held,RightPalm,true);
                    }
                }
            }
            string equipped=player?.EquippedItemId??"";
            if(Time.time-receiptAt<.85f&&receiptItem!="")equipped=receiptItem;
            if(receiving)equipped="";
            if(equipped=="little_spoon")equipped=""; // Passive neck item, never a hand prop.
            if(equipped!=equippedId)
            {
                if(equippedProp!=null)Destroy(equippedProp);
                if(equippedPoi!=null)Destroy(equippedPoi.gameObject);
                equippedProp=null;equippedId=equipped;
                if(equipped=="poi_led"||equipped=="poi_practice")equippedPoi=FestivalPoiRig.Create(attachments,RightPalm,1,equipped=="poi_led",true);
                else
                {
                    var resource=FestivalSession.DropModel(equipped);
                    if(resource!=null)equippedProp=FestivalHeldItem.Create(attachments,equipped,RightPalm,true);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","first_person_equipment","item="+(equipped==""?"none":equipped)+" grip="+(equippedPoi!=null?"poi":equippedProp!=null?"prop":"none")+" palm="+RightPalm);
#endif
            }
            bool poi=player!=null && player.VisualPose=="Poi";
            if(leftPoi!=null){leftPoi.gameObject.SetActive(poi);leftPoi.Spinning=poi;}
            if(rightPoi!=null){rightPoi.gameObject.SetActive(poi);rightPoi.Spinning=poi;}
            if(carriedBand!=null)carriedBand.SetActive(player!=null && player.Wristbands.Count>0 && !poi && held=="");
            if(unpaidPoi!=null)unpaidPoi.gameObject.SetActive(player!=null&&player.Life=="Alive"&&!poi);
            if(unpaidProp!=null)unpaidProp.SetActive(player!=null&&player.Life=="Alive"&&!poi);
            if(equippedProp!=null)equippedProp.SetActive(held==""&&!poi&&player.Life=="Alive");
            if(equippedPoi!=null)equippedPoi.gameObject.SetActive(held==""&&!poi&&player.Life=="Alive");
            SetGrip(poi?"poi":held!=""?held:equipped,player!=null&&player.Life=="Alive");
            presenting=receiving||!string.IsNullOrEmpty(player?.VisualOfferItem);
            raiseRight=poi||held!=""||equipped!=""||receiving;
            raiseLeft=poi||held=="map"||equipped=="map";
            previousCash=player?.Cash??-1;
        }
        void SetGrip(string item,bool alive)
        {
            if(!alive)item="";
            if(item==gripState)return;
            gripState=item;
            for(int i=0;i<gripTargets.Length;i++)gripTargets[i]=0;
            switch(item)
            {
                case "poi":gripTargets[0]=gripTargets[1]=100;break;
                case "poi_led":case "poi_practice":case "confetti":gripTargets[1]=100;break;
                case "merch_bag":gripTargets[2]=100;break;
                case "stock_lsd":case "stock_mushrooms":gripTargets[3]=100;break;
                case "map":case "stage_pass":case "medical_voucher":gripTargets[4]=100;break;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","finger_grip","item="+(item==""?"rest":item)+" articulated="+(gripSkin!=null));
#endif
        }
        void LateUpdate()
        {
            if(gripSkin!=null)
            {
                float blend=1-Mathf.Exp(-18*Time.deltaTime);
                for(int i=0;i<gripIndices.Length;i++)if(gripIndices[i]>=0)
                    gripSkin.SetBlendShapeWeight(gripIndices[i],Mathf.Lerp(gripSkin.GetBlendShapeWeight(gripIndices[i]),gripTargets[i],blend));
            }
            float blendPose=1-Mathf.Exp(-9*Time.deltaTime);
            restLeft=Mathf.Lerp(restLeft,raiseLeft?0:1,blendPose);restRight=Mathf.Lerp(restRight,raiseRight?0:1,blendPose);
            for(int i=0;i<restMeshes.Length;i++)
            {
                if(restLeftIndices[i]>=0)restMeshes[i].SetBlendShapeWeight(restLeftIndices[i],restLeft*100);
                if(restRightIndices[i]>=0)restMeshes[i].SetBlendShapeWeight(restRightIndices[i],restRight*100);
            }
            var camera=transform.parent;
            float distance=Vector3.Distance(camera.position,previousCameraPosition);previousCameraPosition=camera.position;
            float speed=distance<1?distance/Mathf.Max(.001f,Time.deltaTime):0;
            movement=Mathf.Lerp(movement,Mathf.Clamp01(speed/3),blendPose);
            if(distance<1)walkPhase+=distance*5;
            float yaw=camera.eulerAngles.y;
            turnSway=Mathf.Lerp(turnSway,Mathf.Clamp(Mathf.DeltaAngle(previousCameraYaw,yaw),-3,3)*-.003f,blendPose);previousCameraYaw=yaw;
            float handoff=Mathf.Clamp01((Time.time-handoffAt)/.65f);
            float present=Mathf.Sin(handoff*Mathf.PI)*.12f+(presenting?.06f:0);
            var motion=new Vector3(turnSway+Mathf.Sin(walkPhase)*movement*.014f,
                Mathf.Sin(Time.time*2.1f)*.004f+Mathf.Sin(walkPhase*2)*movement*.009f,present);
            transform.localPosition=rest+motion;
            // Attachment offsets use the same Blender Rest morph displacement.
            if(attachments!=null)attachments.localPosition=motion+LoweredGrip*restRight;
            if(leftPoi!=null)leftPoi.transform.localPosition=new Vector3(-RightPalm.x,RightPalm.y,RightPalm.z)+Vector3.Scale(LoweredGrip,new Vector3(-1,1,1))*restLeft-LoweredGrip*restRight;

        }
        void OnDisable(){if(attachments!=null)attachments.gameObject.SetActive(false);}
        void OnEnable(){if(attachments!=null)attachments.gameObject.SetActive(true);}
        void OnDestroy()
        {
            if(attachments!=null){if(Application.isPlaying)Destroy(attachments.gameObject);else DestroyImmediate(attachments.gameObject);}
            if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            if(palette!=null){if(Application.isPlaying)Destroy(palette);else DestroyImmediate(palette);}
        }
    }
}
