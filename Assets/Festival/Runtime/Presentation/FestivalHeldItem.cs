using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Authored grip coordinates in the imported prop's metre space.</summary>
    public static class FestivalHeldItem
    {
        const string GripName="Held grip ";
        // The confetti barrel's open end: its axis is the model's +y.
        static readonly Vector3 ConfettiMuzzle=new Vector3(0,.685f,0);
        public static GameObject Create(Transform parent,string item,Vector3 palm,bool firstPerson)
        {
            string resource=Festival.Network.FestivalSession.DropModel(item);
            if(resource==null||item=="little_spoon")return null;
            Vector3 grip;float size;Quaternion rotation=Quaternion.identity;
            switch(item)
            {
                case "merch_bag":grip=new Vector3(0,.79f,0);size=.65f;break;
                case "stock_lsd":case "stock_mushrooms":grip=new Vector3(0,.27f,firstPerson?-.245f:0);size=.32f;break;
                case "confetti":grip=new Vector3(0,.18f,.19f);size=.42f;break;
                case "map":grip=new Vector3(.20f,.03f,.20f);size=.65f;rotation=Quaternion.Euler(-55,0,0);break;
                case "stage_pass":grip=new Vector3(.17f,.02f,.20f);size=.50f;rotation=Quaternion.Euler(-70,0,0);break;
                case "medical_voucher":grip=new Vector3(.24f,.02f,0);size=.50f;rotation=Quaternion.Euler(-70,0,0);break;
                case "stash_box":grip=new Vector3(.70f,.65f,0);size=.40f;break;
                case "wristband":grip=new Vector3(.30f,.18f,0);size=.30f;break;
                default:return null;
            }
            var root=new GameObject(GripName+item);root.transform.SetParent(parent,false);
            root.layer=parent.gameObject.layer;
            var inverse=Unscale(parent);
            root.transform.localScale=inverse;
            root.transform.localPosition=Vector3.Scale(palm,inverse);
            var prop=FestivalArtView.Create(root.transform,resource);
            if(prop==null){Object.Destroy(root);return null;}
            prop.transform.localRotation=rotation;
            var modelScale=Vector3.one*size;
            prop.transform.localScale=modelScale;
            prop.transform.localPosition=-(rotation*Vector3.Scale(grip,modelScale));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentDiagnostics.GraphicsEvent("InteractionVisuals","item_grip",
                "item="+item+" view="+(firstPerson?"first_person":"world")+" scale="+size+" model_grip="+grip);
#endif
            return root;
        }
        /// <summary>Where a confetti cannon shown under holder fires from: the middle of its barrel's open end, aimed along the barrel
        /// (Pose.up). False when none shows there.</summary>
        public static bool Muzzle(Transform holder,out Pose muzzle)
        {
            if(holder!=null)
                foreach(var grip in holder.GetComponentsInChildren<Transform>())
                    if(grip.name==GripName+"confetti"&&grip.childCount>0)
                    {
                        var prop=grip.GetChild(0);muzzle=new Pose(prop.TransformPoint(ConfettiMuzzle),prop.rotation);return true;
                    }
            muzzle=default;return false;
        }
        /// <summary>The local scale that cancels what a hand passes on (the imported bones' scale and the body's size), so a prop
        /// under it keeps its authored metre size. POLO-5: all but a Ferris wheel rider's RideScale, so a prop made mid-ride goes
        /// round at the rider's size and grows back with them, like one made before they boarded.</summary>
        public static Vector3 Unscale(Transform parent)
        {
            var body=parent.GetComponentInParent<FestivalCharacter>(true);
            var inherited=parent.lossyScale/(body!=null?body.RideScale:1);
            return new Vector3(1/Mathf.Max(.01f,Mathf.Abs(inherited.x)),1/Mathf.Max(.01f,Mathf.Abs(inherited.y)),1/Mathf.Max(.01f,Mathf.Abs(inherited.z)));
        }
    }
}
