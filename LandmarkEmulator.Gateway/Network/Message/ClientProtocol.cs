namespace LandmarkEmulator.Gateway.Network.Message
{
    public enum ClientProtocol
    {
        //ClientProtocol_630 = 630,
        /// <summary>
        /// The 0.522 client. Only accepted for an allowed build (ClientBuilds), and served as 709: the zone has no
        /// handlers of its own for it.
        /// </summary>
        LandmarkClientProtocol_694 = 694,
        LandmarkClientProtocol_709 = 709,

        ClientProtocol_ALL = LandmarkClientProtocol_709
    }
}
