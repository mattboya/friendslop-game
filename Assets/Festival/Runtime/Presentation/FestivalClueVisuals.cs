using Festival.Core;
using Festival.Network;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Local perception cue. The host sends only the viewer's clue hint.</summary>
    public sealed class FestivalClueVisuals : MonoBehaviour
    {
        const int Segments=32;
        FestivalSession session;
        Transform marker;
        Transform outer;
        Mesh ringMesh;
        Material sunMaterial;
        Material moonMaterial;
        MeshRenderer[] renderers;
        Light glow;
        int shownClue=-1;

        public bool Visible => marker!=null&&marker.gameObject.activeSelf;

        void Awake()
        {
            session=GetComponent<FestivalSession>();
            ringMesh=CreateRing();
            sunMaterial=CreateMaterial(new Color(1,.70f,.25f));
            moonMaterial=CreateMaterial(new Color(.29f,.96f,.91f));
            marker=new GameObject("Private clue marker").transform;
            marker.SetParent(transform,false);
            outer=MakeRing("Orbiting clue ring",.49f);
            MakeRing("Inner clue ring",.30f);
            renderers=marker.GetComponentsInChildren<MeshRenderer>();
            glow=marker.gameObject.AddComponent<Light>();
            glow.type=LightType.Point;glow.range=4;glow.intensity=1.6f;glow.shadows=LightShadows.None;
            marker.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var state=session?.State;
            bool visible=state!=null&&state.Phase=="Playing"&&state.CluesRead<2
                &&session.LocalPlayer?.Life=="Alive"&&!string.IsNullOrEmpty(state.PrivateClue);
            if(marker.gameObject.activeSelf!=visible)marker.gameObject.SetActive(visible);
            if(!visible){shownClue=-1;return;}
            var point=FestivalSimulation.CluePoint(state.Seed,state.CluesRead);
            if(shownClue!=state.CluesRead)
            {
                shownClue=state.CluesRead;
                var material=point.X>0?sunMaterial:moonMaterial;
                foreach(var item in renderers)item.sharedMaterial=material;
                glow.color=material.color;
            }
            // Float just in front of the totem, in the direction the player
            // sees when approaching from the path.
            marker.position=new Vector3(point.X,2.28f+Mathf.Sin(Time.time*2)*.08f,point.Z+1.9f);
            marker.rotation=Quaternion.Euler(0,Mathf.Sin(Time.time*.7f)*20,Time.time*25);
            outer.localRotation=Quaternion.Euler(0,0,-Time.time*45);
        }

        Transform MakeRing(string label,float scale)
        {
            var go=new GameObject(label);
            go.transform.SetParent(marker,false);
            go.transform.localScale=Vector3.one*scale;
            go.AddComponent<MeshFilter>().sharedMesh=ringMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=sunMaterial;
            return go.transform;
        }

        static Mesh CreateRing()
        {
            var vertices=new Vector3[(Segments+1)*2];
            var indices=new int[Segments*12];
            for(int i=0;i<=Segments;i++)
            {
                float angle=i*Mathf.PI*2/Segments;
                var direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                vertices[i*2]=direction;
                vertices[i*2+1]=direction*.82f;
                if(i==Segments)continue;
                int v=i*2,t=i*12;
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;
                indices[t+3]=v+1;indices[t+4]=v+3;indices[t+5]=v+2;
                indices[t+6]=v+2;indices[t+7]=v+1;indices[t+8]=v;
                indices[t+9]=v+2;indices[t+10]=v+3;indices[t+11]=v+1;
            }
            var mesh=new Mesh{name="Original clue ring"};
            mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();
            return mesh;
        }

        static Material CreateMaterial(Color color)
        {
            // Use the included URP material. Shader.Find can return null in a
            // player when a dynamically requested shader is stripped.
            var material=new Material(Resources.Load<Material>("FestivalLit")){color=color};
            material.mainTexture=Texture2D.whiteTexture;
            if(material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",color*.6f);
            }
            material.enableInstancing=true;
            return material;
        }

        void OnDestroy()
        {
            if(marker!=null)Dispose(marker.gameObject);
            if(ringMesh!=null)Dispose(ringMesh);
            if(sunMaterial!=null)Dispose(sunMaterial);
            if(moonMaterial!=null)Dispose(moonMaterial);
        }
        static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
