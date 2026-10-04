using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Festival.Tests
{
    public sealed class SpeechBubbleTests
    {
        [Test]
        public void EssentialResourcesResolveAndRenderFallbackAndRuntimeFestivalText()
        {
            var settings=Resources.Load<TMP_Settings>("TMP Settings");
            Assert.That(settings,Is.Not.Null,"The installed TMP Essential Resources must be imported.");
            Assert.That(TMP_Settings.instance,Is.SameAs(settings));
            var fallback=TMP_Settings.defaultFontAsset;
            Assert.That(fallback,Is.Not.Null);
            Assert.That(fallback,Is.SameAs(Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")));
            Assert.That(fallback.atlasTexture,Is.Not.Null);
            Assert.That(TMP_Settings.defaultSpriteAsset,Is.Not.Null);
            Assert.That(TMP_Settings.defaultStyleSheet,Is.Not.Null);
            var shader=Shader.Find("TextMeshPro/Mobile/Distance Field");
            Assert.That(shader,Is.Not.Null);
            Assert.That(shader.isSupported,Is.True);
            Assert.That(fallback.material.shader,Is.SameAs(shader),"The Resources font must retain the runtime bubble shader.");

            var source=Resources.Load<Font>("FestivalDisplay");
            Assert.That(source,Is.Not.Null);
            TMP_FontAsset dynamicFont=null;
            GameObject root=null;
            try
            {
                root=new GameObject("TMP resource consumer");
                var text=root.AddComponent<TextMeshPro>();
                text.font=fallback;
                text.text="Festival friends!";
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterCount,Is.EqualTo(17));
                Assert.That(text.mesh.vertexCount,Is.GreaterThan(0));
                Assert.That(text.fontSharedMaterial.shader,Is.SameAs(shader));

                dynamicFont=TMP_FontAsset.CreateFontAsset(source);
                Assert.That(dynamicFont,Is.Not.Null);
                Assert.That(dynamicFont.sourceFontFile,Is.SameAs(source));
                Assert.That(dynamicFont.atlasPopulationMode,Is.EqualTo(AtlasPopulationMode.Dynamic));
                dynamicFont.material.shader=shader;
                text.font=dynamicFont;
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterCount,Is.EqualTo(17));
                Assert.That(text.mesh.vertexCount,Is.GreaterThan(0));
                Assert.That(text.fontSharedMaterial.shader,Is.SameAs(fallback.material.shader));
            }
            finally
            {
                if(root!=null)Object.DestroyImmediate(root);
                if(dynamicFont!=null)
                {
                    foreach(var atlas in dynamicFont.atlasTextures)if(atlas!=null)Object.DestroyImmediate(atlas);
                    Object.DestroyImmediate(dynamicFont.material);
                    Object.DestroyImmediate(dynamicFont);
                }
            }
        }
    }
}
