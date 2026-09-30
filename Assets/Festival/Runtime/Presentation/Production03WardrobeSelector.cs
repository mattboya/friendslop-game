#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Festival.Presentation
{
    // Isolated review-scene selector. The game's four-slot runtime remains untouched.
    public sealed class Production03WardrobeSelector : MonoBehaviour
    {
        public static readonly string[] Categories={"Headgear","Sunglasses","Shirt","Pants","Shoes","FacialHair","Hairstyle","Accessory"};
        readonly Dictionary<string,SkinnedMeshRenderer> parts=new Dictionary<string,SkinnedMeshRenderer>();
        readonly Dictionary<string,int> choice=new Dictionary<string,int>();
        int selectedCategory;
        void Awake()
        {
            foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.name.StartsWith("AH03W_",StringComparison.Ordinal))parts[r.name]=r;
            foreach(var category in Categories)choice[category]=0;
            Refresh();
        }
        public int GetStyle(string category)=>choice.TryGetValue(category,out var index)?index:-1;
        public void SetStyle(string category,int index)
        {
            if(!choice.ContainsKey(category)||index < -1||index>9)throw new ArgumentOutOfRangeException(category);
            choice[category]=index;Refresh();
        }
        void Refresh()
        {
            foreach(var item in parts)item.Value.enabled=false;
            foreach(var category in Categories)
            {
                if(!choice.TryGetValue(category,out var index)||index<0)continue;
                if(parts.TryGetValue("AH03W_"+category+"_"+index,out var mesh))mesh.enabled=true;
            }
            int hat=GetStyle("Headgear"),hair=GetStyle("Hairstyle");
            if(hair>=0)
            {
                string companion=hat>=0?"AH03W_HairUnderHat":"AH03W_HairTop_"+hair;
                if(parts.TryGetValue(companion,out var mesh))mesh.enabled=true;
            }
        }
        void OnGUI()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--sample-output")>=0)return;
            var category=Categories[selectedCategory];
            GUI.Box(new Rect(16,16,300,80),"Wardrobe review: "+category+" / "+GetStyle(category));
            if(GUI.Button(new Rect(25,48,65,28),"Prev"))SetStyle(category,GetStyle(category)<=-1?9:GetStyle(category)-1);
            if(GUI.Button(new Rect(95,48,65,28),"Next"))SetStyle(category,GetStyle(category)>=9?-1:GetStyle(category)+1);
            if(GUI.Button(new Rect(165,48,130,28),"Next category"))selectedCategory=(selectedCategory+1)%Categories.Length;
        }
    }
}
#endif
