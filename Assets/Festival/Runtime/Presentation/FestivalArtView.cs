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
            {"Leaf",new Color(.15f,.36f,.25f)},{"LeafWarm",new Color(.33f,.43f,.24f)},
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
            material.enableInstancing=true;materials[color]=material;return material;
        }
    }
}
