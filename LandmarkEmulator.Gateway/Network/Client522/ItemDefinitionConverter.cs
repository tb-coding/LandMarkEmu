using LandmarkEmulator.Shared.Network;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// CommandBase 0x4B ItemDefinitions: the batch is the same in both clients (u32 size, u32 count, per record a u16
    /// compressed length, a u16 length and an LZ4 block), the records are written in 0.522's layout.
    /// </summary>
    public static class ItemDefinitionConverter
    {
        /// <param name="packet">Opcode (2 bytes), sub (2 bytes), u32 size, body.</param>
        public static byte[] Convert(byte[] packet)
        {
            var reader = new GamePacketReader(packet[8..]);
            uint count = reader.ReadUInt();

            var body = new List<byte>();
            var writer = new GamePacketWriter(body);
            writer.Write(count);
            for (uint i = 0; i < count; i++)
            {
                ushort compressedLength = ReadLE(reader);
                ushort length = ReadLE(reader);
                var record = new GamePacketReader(Lz4Block.Decode(reader.ReadBytes(compressedLength), length));
                var definition = new ItemDefinition();
                definition.Read(record);

                byte[] converted = ItemDefinitionLayout694.Default.Write(definition);
                byte[] compressed = Lz4Block.Encode(converted);
                writer.WriteLE((ushort)compressed.Length);
                writer.WriteLE((ushort)converted.Length);
                writer.WriteBytes(compressed);
            }

            var output = new List<byte> { packet[0], packet[1], packet[2], packet[3] };
            new GamePacketWriter(output).Write((uint)body.Count);
            output.AddRange(body);
            return output.ToArray();
        }

        private static ushort ReadLE(GamePacketReader reader) => reader.ReadUShort();
    }
}
