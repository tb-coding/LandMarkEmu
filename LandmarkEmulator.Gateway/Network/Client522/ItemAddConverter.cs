using LandmarkEmulator.Shared.Network;
using NLog;
using System;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// ClientUpdateBase 2 ItemAdd: a new item while playing (mined and gathered materials, given items). Both clients read
    /// {i32 length, blob}; the blob is a byte (a definition follows when it is not 0) and the item record. 0.522's reader
    /// FUN_143253ba0 wants its own record (<see cref="InventoryItem694"/>, as in SendSelfToClient), so the 0.695 record is
    /// read the way <see cref="SendSelfConverter"/> reads the login inventory (73rd test: mined materials never arrived).
    /// </summary>
    public static class ItemAddConverter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const int Header = 4;

        public static byte[] Convert(byte[] packet)
        {
            try
            {
                var reader = new GamePacketReader(packet[(Header + 4)..]);
                if (reader.ReadByte() != 0)
                {
                    // RE-Build sends no definition here (the client has them all from login).
                    log.Warn("0.522: dropped an ItemAdd that carries a definition, not converted.");
                    return null;
                }
                InventoryItem694 item = SendSelfConverter.ReadItem(reader, extraByte: false);

                var blob = new List<byte>();
                var writer = new GamePacketWriter(blob);
                writer.Write((byte)0);
                item.Write(writer);

                var converted = new List<byte>(packet[..Header]);
                converted.AddRange(BitConverter.GetBytes(blob.Count));
                converted.AddRange(blob);
                return converted.ToArray();
            }
            catch (Exception e)
            {
                log.Warn($"0.522: dropped an ItemAdd of {packet.Length} bytes that wouldn't convert ({e.Message}).");
                return null;
            }
        }
    }
}
