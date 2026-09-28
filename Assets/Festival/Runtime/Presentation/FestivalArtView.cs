using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>Shared, original Blender prop presentation with no gameplay collider.</summary>
    public static class FestivalArtView
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly Dictionary<string,Color> colors=new Dictionary<string,Color>{
            {"Dark",new Color(.09f,.10f,.17f)},{"Wood",new Color(.39f,.25f,.18f)},
            {"Mint",new Color(.16f,.78f,.64f)},{"Rose",new Color(.93f,.26f,.47f)},
            {"Gold",new Color(.98f,.68f,.21f)},{"Cream",new Color(.91f,.84f,.65f)},
            {"Blue",new Color(.23f,.42f,.73f)},{"Metal",new Color(.37f,.41f,.47f)},
            {"Leaf",new Color(.22f,.43f,.28f)},{"LeafWarm",new Color(.38f,.48f,.27f)},
            {"Bark",new Color(.44f,.31f,.24f)},
            {"Needle",new Color(.16f,.31f,.26f)},
            {"Stone",new Color(.43f,.46f,.42f)},
            {"PaintRose",new Color(.65f,.21f,.34f)},
            {"PaintMint",new Color(.21f,.56f,.51f)},
            {"PaintGold",new Color(.72f,.48f,.22f)},
            {"AutoGlass",new Color(.18f,.30f,.38f)},
            {"CanvasRose",new Color(.93f,.26f,.47f)},
            {"CanvasGold",new Color(.98f,.68f,.21f)},
            {"CanvasMint",new Color(.16f,.78f,.64f)},
            {"CanvasCream",new Color(.91f,.84f,.65f)},
            {"CanvasDark",new Color(.13f,.20f,.24f)},
            {"Rubber",new Color(.10f,.12f,.16f)},
            {"Glass",new Color(.30f,.73f,.79f)},{"White",new Color(.86f,.90f,.82f)}
        };
        public static GameObject Create(Transform parent,string resource)
        {
            var prefab=Resources.Load<GameObject>(resource);
            if(prefab==null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Festival.Rendering] action=missing_resource resource="+resource);
#endif
                return null;
            }
            var go=Object.Instantiate(prefab,parent);go.name=resource;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                string name=renderer.name;int index=name.LastIndexOf("__",System.StringComparison.Ordinal);
                string key=index<0?"Dark":name.Substring(index+2).Split('.')[0];
                renderer.sharedMaterial=MaterialFor(key);
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>())
            {
                collider.enabled=false;
                if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
            }
            return go;
        }
        public static Material MaterialFor(string color)
        {
            if(materials.TryGetValue(color,out var material))return material;
            var template=Resources.Load<Material>("FestivalLit");
            if(template==null)return null;
            material=new Material(template){name="Festival art "+color,color=colors.TryGetValue(color,out var tint)?tint:Color.white};
            string map=color.StartsWith("Canvas",System.StringComparison.Ordinal)?"FestivalCanvas"
                :color=="Bark"?"FestivalBark"
                :color=="Wood"?"FestivalWood"
                :color=="Leaf"||color=="LeafWarm"||color=="Needle"?"FestivalLeaf"
                :color=="Stone"?"FestivalGround":"";
            if(map!="")material.mainTexture=Resources.Load<Texture2D>(map);
            if(material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness",color=="Metal" ? .48f : color=="Glass" ? .68f :
                    color=="AutoGlass" ? .46f : color.StartsWith("Paint",System.StringComparison.Ordinal) ? .56f :
                    color.StartsWith("Canvas",System.StringComparison.Ordinal) ? .06f : color=="Bark" ? .04f :
                    color=="Rubber"||color=="Stone"||color=="Needle" ? .02f : .16f);
            if(material.HasProperty("_Metallic") && color.StartsWith("Paint",System.StringComparison.Ordinal))
                material.SetFloat("_Metallic",.20f);
            if(color=="AutoGlass")
            {
                // The shallow windscreen faces the sunset key light; URP's
                // broad specular highlight otherwise washes the glass white.
                if(material.HasProperty("_SpecularHighlights"))material.SetFloat("_SpecularHighlights",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            material.enableInstancing=true;materials[color]=material;return material;
        }
    }
}
