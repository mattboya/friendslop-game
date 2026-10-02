using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>GAS-2 / ART-1: the Giggle Tank, ART-1's model on a glowing disc. A waist-high mint gas cylinder with a glowing
    /// band, a brass valve and hand wheel, standing on that disc at its foot, so it reads from a distance and at night. Nothing collides, so
    /// only the rules decide who reaches it. FestivalWorld raises one on the festival grounds and shows it where the view has a
    /// tank (Show). GAS-1 raises one more at camp with a bunch of balloons tied to its valve (BuildBalloons).</summary>
    public static class FestivalGiggleTank
    {
        public const string Name="Giggle Tank",BalloonsName="Giggle Balloons";
        // GAS-1: where each balloon floats over the valve, and its colour: a bunch at head height, so camp sees it from the mat.
        private static readonly Vector3[] BalloonAt={new Vector3(-.32f,2.05f,.12f),new Vector3(.27f,2.2f,-.18f),new Vector3(.04f,2.42f,.26f),new Vector3(.42f,1.92f,.28f),new Vector3(-.2f,2.3f,-.32f)};
        private static readonly string[] BalloonLook={"Rose","Mint","Gold","Blue","StageGlowRose"};
        private static readonly Vector3 Valve=new Vector3(0,1.18f,0);

        /// <summary>A tank standing on parent's origin.</summary>
        public static Transform Build(Transform parent)
        {
            var tank=new GameObject(Name).transform;tank.SetParent(parent,false);
            Part(tank,"Tank glow",PrimitiveType.Cylinder,new Vector3(0,.01f,0),new Vector3(1.1f,.01f,1.1f),"StageGlowGold");
            FestivalArtView.Create(tank,"FestivalGiggleTank",true);
            return tank;
        }

        /// <summary>GAS-1: camp's Giggle Balloon station standing on parent's origin: a tank with balloons on strings from its valve.
        /// Like the tank, nothing in it collides.</summary>
        public static Transform BuildBalloons(Transform parent)
        {
            var station=new GameObject(BalloonsName).transform;station.SetParent(parent,false);
            Build(station);
            for(int i=0;i<BalloonAt.Length;i++)
            {
                var at=BalloonAt[i];var rise=at-Valve;
                // A unit cylinder is 2 m tall: half the string's length as its height scale, turned to run from the valve to the balloon.
                Part(station,"Balloon string",PrimitiveType.Cylinder,(Valve+at)*.5f,new Vector3(.012f,rise.magnitude*.5f,.012f),"White").localRotation=Quaternion.FromToRotation(Vector3.up,rise);
                var balloon=FestivalArtView.Create(station,"FestivalGiggleBalloon",true);
                if(balloon!=null)
                {
                    balloon.transform.localPosition=at;
                    foreach(var r in balloon.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("__Rose",System.StringComparison.Ordinal))r.sharedMaterial=FestivalArtView.MaterialFor(BalloonLook[i]);
                }
            }
            return station;
        }

        /// <summary>Every frame: the tank stands where the view's level has one, and is hidden when there is none (none rolled,
        /// grabbed, or a spirit's view). It allocates nothing.</summary>
        public static void Show(Transform tank,RoundState state)
        {
            var at=FestivalSimulation.GiggleTankAt(state);bool shown=at!=null;
            if(tank.gameObject.activeSelf!=shown)tank.gameObject.SetActive(shown);
            if(shown)tank.localPosition=new Vector3(at.X,0,at.Z);
        }

        private static Transform Part(Transform tank,string name,PrimitiveType shape,Vector3 at,Vector3 size,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(tank,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(look);
            var collider=go.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
            return go.transform;
        }
    }
}
