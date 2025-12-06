namespace Shared
{
    public class ClientSettings
    {
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public bool Fullscreen { get; set; } = false;
        public bool VSync { get; set; } = true;
        public bool RemoveBackground { get; set; } = false;
    }
}
