using NLog;
using System;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// ClientUpdateBase 3 ItemUpdate. Both clients drop the packet unless it is read to the end: 0.695 has two item data
    /// blocks and a u32 before the wear, 0.522 (FUN_143253d80) one block and the float at item +0x68.
    /// </summary>
    /// <remarks>
    /// A block is five u32s, a stat list and a catalyst list (the same in both clients, see <see cref="InventoryItem"/>).
    /// RE-Build fills the stat list of tools (the pick's 3 → 100, 13 → 50, 12 → 80), so the length is read from the lists.
    /// </remarks>
    public static class ItemUpdateConverter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const int Header = 4, Item = 36;
        /// <summary>u32 stat, u32 name text, u32 value, u32 multiplier, bool, u32 × 3.</summary>
        private const int Stat = 29;
        private const int Catalyst = 12;
        /// <summary>The value 0.522's constructor gives item +0x68, as in <see cref="InventoryItem694"/>.</summary>
        private const float Unknown68 = 1f;

        public static byte[] Convert(byte[] packet)
        {
            int first = Header + Item;
            int second = BlockEnd(packet, first);
            int end = second < 0 ? -1 : BlockEnd(packet, second);
            if (end < 0 || end + 8 != packet.Length)
            {
                log.Warn($"0.522: dropped an ItemUpdate of {packet.Length} bytes, its item data blocks don't add up.");
                return null;
            }

            var converted = new byte[second + 4];
            Array.Copy(packet, converted, second);
            BitConverter.TryWriteBytes(converted.AsSpan(second), Unknown68);
            return converted;
        }

        /// <summary>
        /// Where the block starting at <paramref name="start"/> ends, -1 when the packet is too short for it.
        /// </summary>
        public static int BlockEnd(byte[] packet, int start)
        {
            int stats = start + 20;
            if (stats + 4 > packet.Length)
                return -1;
            long catalysts = stats + 4 + (long)BitConverter.ToUInt32(packet, stats) * Stat;
            if (catalysts + 4 > packet.Length)
                return -1;
            long end = catalysts + 4 + (long)BitConverter.ToUInt32(packet, (int)catalysts) * Catalyst;
            return end > packet.Length ? -1 : (int)end;
        }
    }
}
