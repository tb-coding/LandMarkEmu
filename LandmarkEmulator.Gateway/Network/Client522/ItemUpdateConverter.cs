using NLog;
using System;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// ClientUpdateBase 3 ItemUpdate. Both clients drop the packet unless it is read to the end: 0.695 has two item data
    /// blocks and a u32 before the wear, 0.522 (FUN_143253d80) one block and the float at item +0x68.
    /// </summary>
    public static class ItemUpdateConverter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const int Header = 4, Item = 36, Block = 28;
        private const int Length694 = Header + Item + 2 * Block + 4 + 4;
        /// <summary>The value 0.522's constructor gives item +0x68, as in <see cref="InventoryItem694"/>.</summary>
        private const float Unknown68 = 1f;

        public static byte[] Convert(byte[] packet)
        {
            if (packet.Length != Length694)
            {
                log.Warn($"0.522: dropped an ItemUpdate of {packet.Length} bytes, expected {Length694}.");
                return null;
            }

            var converted = new byte[Header + Item + Block + 4];
            Array.Copy(packet, converted, Header + Item + Block);
            BitConverter.TryWriteBytes(converted.AsSpan(Header + Item + Block), Unknown68);
            return converted;
        }
    }
}
