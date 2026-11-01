using LandmarkEmulator.Shared.Network;
using System;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// ClaimsBase packets holding a site record, rewritten from 0.695's layout to 0.522's (FUN_1433bc480): 0.522 has a
    /// u64 list and a u32 after the special access lists, its site game has one u32 less, and it has no time of day, music,
    /// mode or adventure flags at the end. Field order follows the client's readers, dev-log/11 "0.522 ClaimsBase".
    /// </summary>
    public static class ClaimConverter
    {
        private const byte Aoi = 4, Info = 5, Complete = 7, Management = 8, EditItem = 9;

        /// <summary>A ClaimsBase packet: opcode (2 bytes), sub, body. Subs without a record come back as they are.</summary>
        public static byte[] Convert(byte[] packet)
        {
            byte sub = packet[2];
            if (sub is not (Aoi or Info or Complete or Management or EditItem))
                return packet;

            var copier = new Copier(packet[3..]);
            switch (sub)
            {
                case Aoi:
                    copier.Bytes(1);                                // full update
                    for (uint i = copier.U32(); i > 0; i--)
                        Record(copier);
                    copier.Rest();
                    break;
                case Info:
                    InfoUpdate(copier);
                    break;
                case Complete:
                    if (copier.U32() == 2)                          // placed: a record follows
                        Record(copier);
                    copier.Rest();
                    break;
                case Management:
                    copier.Bytes(8);
                    for (uint i = copier.U32(); i > 0; i--)
                    {
                        copier.Bytes(1 + 8);
                        copier.Text();
                        copier.Bytes(1 + 8);
                        copier.Text();
                        copier.Add(0u);                             // 0.522's u64 list
                        copier.Bytes(4 + 4);
                        copier.Add(0u);                             // and its third u32
                    }
                    break;
                case EditItem:
                    copier.Bytes(8 + 1);
                    Record(copier);
                    copier.Rest();
                    break;
            }

            var output = new List<byte> { packet[0], packet[1], sub };
            output.AddRange(copier.Output);
            return output.ToArray();
        }

        private static void Record(Copier copier)
        {
            copier.Bytes(8 + 8 + 8);                                // id, parent, owner
            copier.Text();
            copier.Bytes(8);
            copier.Text();
            copier.Text();
            copier.Text();
            copier.Bytes(4 + 6 * 8 + 3 * 4);                        // bounds, default access
            for (uint i = copier.U32(); i > 0; i--)
                copier.Bytes(8 + 4);                                // players
            for (uint i = copier.U32(); i > 0; i--)
                copier.Bytes(8 + 4);                                // guilds
            copier.Add(0u);                                         // 0.522's u64 list
            copier.Add(0u);                                         // and its u32
            for (uint i = copier.U32(); i > 0; i--)
            {
                copier.Bytes(8);
                copier.Text();
                copier.Bytes(4 + 1);
            }
            copier.Bytes(4 + 8 + 1 + 4 + 4);
            copier.Bytes(4);                                        // an empty list
            for (int i = 0; i < 5; i++)
                copier.Text();
            copier.Bytes(8);
            Game(copier);
            copier.Bytes(8);
            copier.Text();
            for (uint i = copier.U32(); i > 0; i--)
            {
                copier.Bytes(8 + 8 + 4 + 1);
                copier.Text();
                copier.Text();
            }
            copier.Drop(4 + 4 + 4 + 4 + 4 + 4);                     // time of day, music, mode
            for (uint i = copier.DropU32(); i > 0; i--)
                throw new InvalidOperationException("Site record with a list 0.522 can't carry.");
            copier.Drop(8);                                         // adventure flags
        }

        private static void InfoUpdate(Copier copier)
        {
            copier.Bytes(8 + 4);
            copier.Text();
            copier.Bytes(4 + 3 * 4 + 8 + 4 * 4 + 8);
            for (int i = 0; i < 5; i++)
                copier.Text();
            copier.Bytes(8);
            Game(copier);
            copier.Bytes(8);
            copier.Add(0u);                                         // 0.522's u64 list
            copier.Add(0u);                                         // and its u32
            copier.Text();
            copier.Add(0u);                                         // the record's list, sent empty
            copier.Skip();                                          // the rest: an empty list, time of day, music, mode, flags
        }

        /// <summary>
        /// The site's game (FUN_1433ba960): 0.522 has 4 u32 where 0.695 has 5 and no u32 at the end. The difficulty goes
        /// because the walk-on window shows it only when set; which of the five 0.522 lacks is not confirmed.
        /// </summary>
        private static void Game(Copier copier)
        {
            copier.Bytes(8 + 4 + 1 + 3 * 4);
            copier.Drop(4);
            copier.Bytes(4 + 1 + 4);
            copier.Text();
            for (uint i = copier.U32(); i > 0; i--)
            {
                copier.Bytes(4);
                copier.Text();
            }
            copier.Drop(4);
        }

        /// <summary>Reads the 0.695 bytes and writes what is kept.</summary>
        private sealed class Copier
        {
            private readonly byte[] input;
            private int at;
            public readonly List<byte> Output = new();

            public Copier(byte[] data) => input = data;

            public void Bytes(int count)
            {
                Output.AddRange(input[at..(at + count)]);
                at += count;
            }

            public void Drop(int count) => at += count;

            public uint U32()
            {
                uint value = BitConverter.ToUInt32(input, at);
                Bytes(4);
                return value;
            }

            public uint DropU32()
            {
                uint value = BitConverter.ToUInt32(input, at);
                at += 4;
                return value;
            }

            public void Text()
            {
                int length = BitConverter.ToInt32(input, at);
                Bytes(4 + length);
            }

            public void Add(uint value) => Output.AddRange(BitConverter.GetBytes(value));

            public void Rest() => Bytes(input.Length - at);

            public void Skip() => at = input.Length;
        }
    }
}
