namespace Rth.Shared.Net
{
    /// <summary>
    /// The dev "::fly" camera, sent as server state under <see cref="Key"/>: while <see cref="On"/>,
    /// the client flies its camera free of the player and streams the world around the camera.
    /// </summary>
    public sealed class FlyState
    {
        public const string Key = "dev.fly";

        public bool On;
    }
}
