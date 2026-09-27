namespace Festival.Integrations
{
    // Steam/voice providers plug in here; absence must remain visible to the player.
    public interface IVoiceSession
    {
        bool Available { get; }
        string Status { get; }
        void Join(string sessionId);
        void Leave();
        void SetLocalMute(bool muted);
        void SetRemoteMute(string playerId, bool muted);
        void UpdatePosition(string playerId, float x, float z, bool spirit);
    }
    public sealed class UnavailableVoiceSession : IVoiceSession
    {
        public bool Available => false;
        public string Status => "Voice unavailable — provider not configured";
        public void Join(string sessionId) { }
        public void Leave() { }
        public void SetLocalMute(bool muted) { }
        public void SetRemoteMute(string playerId, bool muted) { }
        public void UpdatePosition(string playerId, float x, float z, bool spirit) { }
    }
}
