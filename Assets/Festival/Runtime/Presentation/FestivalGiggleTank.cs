using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>GAS-2: the Giggle Tank's stand-in, from primitives, until the art pass. A waist-high mint gas cylinder with a
    /// glowing band, a brass valve and a glowing disc at its foot, so it reads from a distance and at night. Nothing collides, so
    /// only the rules decide who reaches it. FestivalWorld raises one on the festival grounds and shows it where the view has a
    /// tank (Show); GAS-1 builds another at camp for the Giggle Balloons.</summary>
    public static class FestivalGiggleTank
    {
        public const string Name="Giggle Tank";

        /// <summary>A tank standing on parent's origin.</summary>
        public static Transform Build(Transform parent)
        {
            var tank=new GameObject(Name).transform;tank.SetParent(parent,false);
            Part(tank,"Tank glow",PrimitiveType.Cylinder,new Vector3(0,.01f,0),new Vector3(1.1f,.01f,1.1f),"StageGlowGold");
            Part(tank,"Tank body",PrimitiveType.Cylinder,new Vector3(0,.47f,0),new Vector3(.42f,.45f,.42f),"PaintMint");
            Part(tank,"Tank shoulder",PrimitiveType.Sphere,new Vector3(0,.92f,0),new Vector3(.42f,.3f,.42f),"PaintMint");
            Part(tank,"Tank band",PrimitiveType.Cylinder,new Vector3(0,.6f,0),new Vector3(.44f,.06f,.44f),"StageGlowRose");
            Part(tank,"Tank valve",PrimitiveType.Cylinder,new Vector3(0,1.09f,0),new Vector3(.1f,.08f,.1f),"Metal");
            Part(tank,"Tank hand wheel",PrimitiveType.Cylinder,new Vector3(0,1.18f,0),new Vector3(.22f,.015f,.22f),"Gold");
            return tank;
        }

        /// <summary>Every frame: the tank stands where the view's level has one, and is hidden when there is none (none rolled,
        /// grabbed, or a spirit's view). It allocates nothing.</summary>
        public static void Show(Transform tank,RoundState state)
        {
            var at=FestivalSimulation.GiggleTankAt(state);bool shown=at!=null;
            if(tank.gameObject.activeSelf!=shown)tank.gameObject.SetActive(shown);
            if(shown)tank.localPosition=new Vector3(at.X,0,at.Z);
        }

        private static void Part(Transform tank,string name,PrimitiveType shape,Vector3 at,Vector3 size,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(tank,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(look);
            var collider=go.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
        }
    }
}
