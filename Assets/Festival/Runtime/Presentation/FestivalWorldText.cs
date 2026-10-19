using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>World labels (signs, shop tags, name tags, verdicts, the REC tag) in the festival font, like the speech bubbles.
    /// They are depth-tested, so walls hide them, and their back faces are culled, so none shows mirrored from behind.
    /// One font asset and material serve every label for the app's life.</summary>
    public static class FestivalWorldText
    {
        public static readonly Color Cream=new Color(.96f,.94f,.84f);
        static TMP_FontAsset font;
        public static TMP_FontAsset Font
        {
            get
            {
                if(font!=null)return font;
                // ponytail: one dynamic atlas for the app's life, never disposed; per-view fonts if a scene ever unloads the festival.
                font=TMP_FontAsset.CreateFontAsset(Resources.Load<UnityEngine.Font>("FestivalDisplay"));
                font.name="Festival world font";font.isMultiAtlasTexturesEnabled=false;
                font.material.shader=TMP_Settings.defaultFontAsset.material.shader;
                font.material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode,(float)CompareFunction.LessEqual);
                font.material.SetFloat(ShaderUtilities.ShaderTag_CullMode,(float)CullMode.Back);
                font.fallbackFontAssetTable=new List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};
                return font;
            }
        }
        /// <summary>A literal label on go, centred on it (or standing on it, bottom), never wrapped. size is the old TextMesh's
        /// fontSize times its characterSize: a TextMeshPro point and a TextMesh pixel at size 1 are both a tenth of a metre.
        /// Adding it swaps go's Transform for a RectTransform (same place), so a Transform held from before is dead: use the
        /// returned label's transform.</summary>
        public static TextMeshPro Add(GameObject go,string text,float size,Color color,bool bottom=false)
        {
            var label=go.AddComponent<TextMeshPro>();label.font=Font;label.fontSharedMaterial=Font.material;
            label.richText=false;label.parseCtrlCharacters=false;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Overflow;
            label.alignment=bottom?TextAlignmentOptions.Bottom:TextAlignmentOptions.Center;
            label.rectTransform.pivot=new Vector2(.5f,bottom?0:.5f);label.rectTransform.sizeDelta=Vector2.zero;
            label.fontSize=size;label.color=color;label.text=text;
            label.renderer.shadowCastingMode=ShadowCastingMode.Off;label.renderer.receiveShadows=false;
            return label;
        }
    }
}
