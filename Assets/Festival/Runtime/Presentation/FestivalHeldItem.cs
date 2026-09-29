using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Authored grip coordinates in the imported prop's metre space.</summary>
    public static class FestivalHeldItem
    {
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
            var root=new GameObject("Held grip "+item);root.transform.SetParent(parent,false);
            root.layer=parent.gameObject.layer;
            var inherited=parent.lossyScale;
            var inverse=new Vector3(1/Mathf.Max(.01f,Mathf.Abs(inherited.x)),
                1/Mathf.Max(.01f,Mathf.Abs(inherited.y)),1/Mathf.Max(.01f,Mathf.Abs(inherited.z)));
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
    }
}
