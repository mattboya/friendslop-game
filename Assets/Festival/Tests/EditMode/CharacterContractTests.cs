using NUnit.Framework;
using UnityEngine;
using Festival.Presentation;
using Festival.Core;

namespace Festival.Tests
{
    public sealed class CharacterContractTests
    {
        [Test]public void CrowdHasStableIndependentEyeStates()
        {
            var first=new FestivalSimulation(11).State.Npcs;
            var again=new FestivalSimulation(11).State.Npcs;
            int wide=0,red=0,both=0;
            for(int i=0;i<first.Count;i++)
            {
                Assert.That(first[i].HighlyIntoxicated,Is.EqualTo(again[i].HighlyIntoxicated));
                Assert.That(first[i].RedEyes,Is.EqualTo(again[i].RedEyes));
                if(first[i].Kind!="Wook")continue;
                if(first[i].HighlyIntoxicated)wide++;
                if(first[i].RedEyes)red++;
                if(first[i].HighlyIntoxicated&&first[i].RedEyes)both++;
            }
            Assert.That(wide,Is.InRange(3,4));
            Assert.That(red,Is.InRange(4,6));
            Assert.That(both,Is.GreaterThan(0));
        }
        [Test]public void DistantMeshesPreserveBoneBindingsAndFacialShapeKeys()
        {
            var detailed=Resources.Load<GameObject>("FestivalCharacter");
            var distant=Resources.Load<GameObject>("FestivalCharacterDistant");
            Assert.That(distant,Is.Not.Null);
            var lower=distant.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var high in detailed.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var low=System.Array.Find(lower,r=>r.name==high.name);Assert.That(low,Is.Not.Null,high.name);
                Assert.That(low.bones.Length,Is.EqualTo(high.bones.Length),high.name);
                for(int i=0;i<high.bones.Length;i++)
                {
                    Assert.That(low.bones[i].name,Is.EqualTo(high.bones[i].name),high.name+" bone order");
                    for(int j=0;j<16;j++)Assert.That(low.sharedMesh.bindposes[i][j],Is.EqualTo(high.sharedMesh.bindposes[i][j]).Within(.0001f),high.name+" bind pose");
                }
                Assert.That(low.sharedMesh.triangles.Length,Is.LessThan(high.sharedMesh.triangles.Length),high.name);
                if(high.name.StartsWith("Face_"))
                {
                    Assert.That(high.sharedMesh.GetBlendShapeIndex("Blink"),Is.GreaterThanOrEqualTo(0));
                    Assert.That(low.sharedMesh.GetBlendShapeIndex("Blink"),Is.GreaterThanOrEqualTo(0));
                    Assert.That(high.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes"),Is.GreaterThanOrEqualTo(0));
                    Assert.That(low.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes"),Is.GreaterThanOrEqualTo(0));
                    Assert.That(high.sharedMaterials.Length,Is.EqualTo(2),"Eye whites need their own tintable material");
                }
            }
        }
        [Test]public void ImportedFacesKeepEyeWhitesSeparateFromIrisAndPupilColorCells()
        {
            foreach(var resource in new[]{"FestivalCharacter","FestivalCharacterDistant"})
            {
                var model=Resources.Load<GameObject>(resource);
                foreach(var face in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if(!face.name.StartsWith("Face_"))continue;
                    var mesh=face.sharedMesh;
                    Assert.That(mesh.subMeshCount,Is.EqualTo(2),resource+"/"+face.name);
                    var uv=mesh.uv;
                    int iris=0,pupils=0,whites=0,irisOther=0,pupilsOther=0,whitesOther=0;
                    foreach(var vertex in mesh.GetTriangles(0))
                    {
                        int cell=Mathf.FloorToInt(uv[vertex].x*8);
                        if(cell==4)whites++;
                        if(cell==6)irisOther++;
                        if(cell==5)pupilsOther++;
                    }
                    foreach(var vertex in mesh.GetTriangles(1))
                    {
                        int cell=Mathf.FloorToInt(uv[vertex].x*8);
                        if(cell==6)iris++;
                        if(cell==5)pupils++;
                        if(cell==4)whitesOther++;
                    }
                    Assert.That(iris,Is.GreaterThan(0),resource+"/"+face.name+" iris missing from palette material");
                    Assert.That(pupils,Is.GreaterThan(0),resource+"/"+face.name+" pupil missing from palette material");
                    Assert.That(whites,Is.GreaterThan(0),resource+"/"+face.name+" eye white missing from tinted material");
                    Assert.That(irisOther+pupilsOther+whitesOther,Is.Zero,resource+"/"+face.name+" eye colors cross the imported material boundary");
                }
            }
        }
        [Test]public void DistanceDetailPreservesFitAndKeepsPortraitDetailed()
        {
            var root=new GameObject("Distance wardrobe test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"quality_distance_profile",Color.white);
                var body=System.Array.Find(actor.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.enabled&&r.name.StartsWith("Body_"));
                var detailed=body.sharedMesh;
                actor.UpdateDetailForDistance(11);Assert.That(actor.UsesDistantMesh,Is.True);Assert.That(body.sharedMesh,Is.Not.SameAs(detailed));
                foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!renderer.enabled||renderer.name.StartsWith("Body_"))continue;
                    int fit=renderer.sharedMesh.GetBlendShapeIndex("Fit_"+actor.Appearance.Gender+"_"+actor.Appearance.Shape);
                    Assert.That(fit,Is.GreaterThanOrEqualTo(0));Assert.That(renderer.GetBlendShapeWeight(fit),Is.EqualTo(100));
                }
                actor.UpdateDetailForDistance(9);Assert.That(actor.UsesDistantMesh,Is.True,"Distance hysteresis");
                actor.UpdateDetailForDistance(7);Assert.That(body.sharedMesh,Is.SameAs(detailed));
                actor.AlwaysHighDetail=true;actor.UpdateDetailForDistance(1000);Assert.That(actor.UsesDistantMesh,Is.False,"Portrait keeps detailed geometry");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]public void RuntimeCharacterHasSkinPaletteAndNamedAnimationBones()
        {
            var model=Resources.Load<GameObject>("FestivalCharacter");
            Assert.That(model,Is.Not.Null);
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin,Is.Not.Null);Assert.That(skin.sharedMesh.vertexCount,Is.GreaterThan(0));
            Assert.That(skin.sharedMesh.triangles.Length/3,Is.LessThan(18000),"A single modular mesh exceeded the revised character geometry budget");
            Assert.That(skin.sharedMaterials.Length,Is.EqualTo(1));
            foreach(var name in new[]{"Hips","Spine","Head","ArmL","ArmR","ForearmL","ForearmR","LegL","LegR","ShinL","ShinR"})
                Assert.That(System.Array.Exists(skin.bones,b=>b.name==name),Is.True,"Missing animation bone: "+name);
            Assert.That(Resources.Load<Texture2D>("FestivalPalette"),Is.Not.Null);
        }
        [Test]public void BlenderCharacterIncludesIndependentWardrobeSlotsAndThreeBodyShapes()
        {
            var model=Resources.Load<GameObject>("FestivalCharacter");
            var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var category in new[]{"Headgear","Sunglasses","Shirt","Pants","Shoes","FacialHair","Hairstyle","HairTop","Accessory"})
                for(int variant=0;variant<4;variant++)
                    Assert.That(System.Array.Exists(renderers,r=>r.name==category+"_"+variant),Is.True,category+" variant missing: "+variant);
            foreach(var renderer in renderers)
            {
                if(renderer.name.StartsWith("Body_"))continue;
                for(int gender=0;gender<2;gender++)for(int shape=0;shape<3;shape++)
                    Assert.That(renderer.sharedMesh.GetBlendShapeIndex("Fit_"+gender+"_"+shape),Is.GreaterThanOrEqualTo(0),renderer.name+" fit shape missing");
            }
            Assert.That(System.Array.Exists(renderers,r=>r.name=="Equipment_LittleSpoon"),Is.True,"Little Spoon necklace mesh missing");
            for(int gender=0;gender<2;gender++)for(int shape=0;shape<3;shape++)
                Assert.That(System.Array.Exists(renderers,r=>r.name=="Body_"+gender+"_"+shape),Is.True,"Body shape missing");
            for(int gender=0;gender<2;gender++)for(int face=0;face<6;face++)
                Assert.That(System.Array.Exists(renderers,r=>r.name=="Face_"+gender+"_"+face),Is.True,"Face style missing");
            var hands=Resources.Load<GameObject>("FestivalHands");Assert.That(hands,Is.Not.Null);
            foreach(var category in new[]{"HandsSkin_0","HandsSkin_1","HandsSkin_2","HandsSleeve_0","HandsSleeve_1","HandsSleeve_2","HandsSleeve_3"})
                Assert.That(System.Array.Exists(hands.GetComponentsInChildren<Renderer>(true),r=>r.name==category),Is.True,category+" missing");
        }
        [Test]public void OutfitIsStableAndFacialHairOnlyAppearsOnMen()
        {
            bool manWithFacialHair=false,womanSeen=false,optionalAbsent=false,scruffySeen=false,deadpanSeen=false;
            var headgear=new bool[5];var sunglasses=new bool[5];var accessories=new bool[5];var facialHair=new bool[5];
            for(int i=0;i<300;i++)
            {
                var id="generated_attendee_"+i;
                var a=FestivalAppearance.For(id);var repeat=FestivalAppearance.For(id);
                Assert.That(a.Gender,Is.EqualTo(repeat.Gender));Assert.That(a.Shirt,Is.EqualTo(repeat.Shirt));
                Assert.That(a.Pants,Is.EqualTo(repeat.Pants));Assert.That(a.HairColor,Is.EqualTo(repeat.HairColor));
                Assert.That(a.Shirt,Is.InRange(0,3));Assert.That(a.Pants,Is.InRange(0,3));
                if(a.Face<3)scruffySeen=true;else deadpanSeen=true;
                if(a.Gender==0 && a.FacialHair>=0)manWithFacialHair=true;
                if(a.Gender==1){womanSeen=true;Assert.That(a.FacialHair,Is.EqualTo(-1));}
                if(a.Headgear<0 || a.Sunglasses<0 || a.Accessory<0)optionalAbsent=true;
                headgear[a.Headgear+1]=true;sunglasses[a.Sunglasses+1]=true;accessories[a.Accessory+1]=true;
                if(a.Gender==0)facialHair[a.FacialHair+1]=true;
            }
            Assert.That(manWithFacialHair,Is.True);Assert.That(womanSeen,Is.True);Assert.That(optionalAbsent,Is.True);
            Assert.That(scruffySeen&&deadpanSeen,Is.True,"Both face families must appear in the crowd");
            foreach(var pair in new[]{("headgear",headgear),("sunglasses",sunglasses),("accessories",accessories),("facial hair",facialHair)})
                for(int variant=0;variant<5;variant++)Assert.That(pair.Item2[variant],Is.True,pair.Item1+" option "+(variant-1)+" never generated");
        }
        [Test]public void RuntimeActorEnablesExactlyOneRequiredVariantPerSlot()
        {
            var root=new GameObject("Wardrobe test");
            try
            {
                var actor=FestivalCharacter.Create(root.transform,"generated_attendee_17",Color.white);
                var enabled=System.Array.FindAll(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.enabled);
                int triangles=0;foreach(var renderer in enabled)triangles+=renderer.sharedMesh.triangles.Length/3;
                Assert.That(triangles,Is.LessThan(45000),"One assembled attendee exceeded the 45k triangle budget");
                foreach(var category in new[]{"Body_","Face_","Shirt_","Pants_","Shoes_","Hairstyle_"})
                    Assert.That(System.Array.FindAll(enabled,r=>r.name.StartsWith(category)).Length,Is.EqualTo(1),category+" selection count");
                Assert.That(System.Array.FindAll(enabled,r=>r.name.StartsWith("HairTop_")).Length,Is.LessThanOrEqualTo(1));
                foreach(var renderer in enabled)
                {
                    if(renderer.name.StartsWith("Body_"))continue;
                    var fit="Fit_"+actor.Appearance.Gender+"_"+actor.Appearance.Shape;
                    int index=renderer.sharedMesh.GetBlendShapeIndex(fit);
                    Assert.That(index,Is.GreaterThanOrEqualTo(0),renderer.name+" fit missing");
                    Assert.That(renderer.GetBlendShapeWeight(index),Is.EqualTo(100).Within(.1f),renderer.name+" fit not selected");
                    for(int other=0;other<renderer.sharedMesh.blendShapeCount;other++)
                        if(other!=index)Assert.That(renderer.GetBlendShapeWeight(other),Is.Zero,renderer.name+" has an unwanted imported shape active");
                }
                foreach(var category in new[]{"Headgear_","Sunglasses_","FacialHair_","Accessory_"})
                    Assert.That(System.Array.FindAll(enabled,r=>r.name.StartsWith(category)).Length,Is.LessThanOrEqualTo(1),category+" selection count");
                var spoon=System.Array.Find(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.name=="Equipment_LittleSpoon");
                Assert.That(spoon,Is.Not.Null);Assert.That(spoon.enabled,Is.False);
                actor.SetLittleSpoon(true);Assert.That(spoon.enabled,Is.True);
                actor.SetLittleSpoon(false);Assert.That(spoon.enabled,Is.False);
                var face=System.Array.Find(enabled,r=>r.name.StartsWith("Face_"));
                actor.SetHighlyIntoxicated(true);
                int wide=face.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes");
                Assert.That(face.GetBlendShapeWeight(wide),Is.EqualTo(100));
                actor.SetRedEyes(true);
                Assert.That(face.sharedMaterials[0].color.g,Is.LessThan(.6f));
                actor.UpdateDetailForDistance(11);
                wide=face.sharedMesh.GetBlendShapeIndex("WideIntoxicatedEyes");
                Assert.That(face.GetBlendShapeWeight(wide),Is.EqualTo(100),"Wide eyes survive distance switch");
                actor.SetHighlyIntoxicated(false);actor.SetRedEyes(false);
                Assert.That(face.GetBlendShapeWeight(wide),Is.Zero);
                Assert.That(face.sharedMaterials[0].color.g,Is.GreaterThan(.9f));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]public void HatsSelectClearHairLayersForEveryHeadgearType()
        {
            var root=new GameObject("Hat and hair fit test");
            try
            {
                for(int hat=-1;hat<4;hat++)
                {
                    string id=null;
                    for(int candidate=0;candidate<1000;candidate++)
                    {
                        var possible="generated_attendee_"+candidate;
                        if(FestivalAppearance.For(possible).Headgear==hat){id=possible;break;}
                    }
                    Assert.That(id,Is.Not.Null,"No deterministic profile found for hat="+hat);
                    var actor=FestivalCharacter.Create(root.transform,id,Color.white);
                    var renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Assert.That(System.Array.Exists(renderers,r=>r.name=="Hairstyle_"+actor.Appearance.Hairstyle && r.enabled),Is.True);
                    Assert.That(System.Array.Exists(renderers,r=>r.name=="HairTop_"+actor.Appearance.Hairstyle && r.enabled),Is.EqualTo(hat<0));
                    Assert.That(System.Array.Exists(renderers,r=>r.name=="HairUnderHat" && r.enabled),Is.EqualTo(hat==2||hat==3));
                    Object.DestroyImmediate(actor.gameObject);
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
