namespace Festival.Core
{
    /// <summary>Authored social pockets for the 24 interactive attendees.</summary>
    public static class FestivalCrowdLayout
    {
        public readonly struct Start
        {
            public readonly float X,Z,Yaw;
            public readonly string Pose;
            public Start(float x,float z,float yaw,string pose){X=x;Z=z;Yaw=yaw;Pose=pose;}
        }

        // The center stage aisle remains open. Small irregular groups sit along
        // its edges, at the poi circles, under trees, and beside the paths.
        static readonly Start[] starts={
            new Start(-11.4f,25.2f,12,"Dance"),new Start(-9.8f,23.2f,-18,"Dance"),
            new Start(-12.1f,21.0f,15,"Watching"),new Start(-10.0f,18.9f,-10,"Dance"),
            new Start(-8.8f,20.7f,20,"Dance"),new Start(11.6f,24.6f,-15,"Dance"),
            new Start(10.1f,22.4f,11,"Dance"),new Start(12.5f,20.8f,-20,"Watching"),
            new Start(9.4f,18.7f,14,"Dance"),new Start(11.7f,17.5f,-12,"Dance"),
            new Start(-15.1f,12.6f,100,"Watching"),new Start(-11.0f,10.2f,-70,"Watching"),
            new Start(15.1f,12.9f,-95,"Watching"),new Start(10.9f,10.7f,70,"Watching"),
            new Start(-24.1f,13.1f,55,"Watching"),new Start(-26.0f,9.5f,-30,"Idle"),
            new Start(22.1f,15.0f,-42,"Watching"),new Start(25.0f,11.2f,28,"Idle"),
            new Start(-18.8f,-15.2f,180,"Idle"),new Start(-10.8f,-12.5f,90,"Watching"),
            new Start(8.5f,-11.5f,-65,"Idle"),new Start(17.5f,-7.7f,45,"Watching"),
            new Start(-6.2f,-5.8f,95,"Idle"),new Start(6.5f,4.5f,-80,"Idle")
        };
        public static int Count=>starts.Length;
        // The crowd's centre, before jitter. Security patrols a ring around it.
        public static WorldPoint Centroid()
        {
            float x=0,z=0;
            foreach(var point in starts){x+=point.X;z+=point.Z;}
            return new WorldPoint(x/starts.Length,z/starts.Length);
        }
        // Each crew's standing spots are jittered from these by at most MaxJitter along each axis.
        public const float MaxJitter=.21f;
        public static Start Spot(int index)=>starts[index];
        public static Start Get(int index,int seed)
        {
            var point=Spot(index);
            return new Start(point.X+Jitter(seed,index,13),point.Z+Jitter(seed,index,29),point.Yaw,point.Pose);
        }
        static float Jitter(int seed,int index,int salt)
        {
            unchecked
            {
                uint hash=(uint)seed*747796405u+(uint)(index*97+salt)*2891336453u;
                hash^=hash>>16;hash*=2246822519u;hash^=hash>>13;
                return ((hash%1001)/1000f-.5f)*(2*MaxJitter);
            }
        }
    }
}
