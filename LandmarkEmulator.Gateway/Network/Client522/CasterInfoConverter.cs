using NLog;
using System;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// Abilities 0x27 sub 3 CasterInfoReply. The client asks for an ability's caster info (CasterInfoRequest, sub 2) before
    /// it lets a held tool or an item be used, and keeps asking until a reply it can read arrives (73rd test: 2218 requests
    /// in 40 minutes, every tool and item unusable). The server writes 0.695's reply (RE-Build AbilityCasterInfoReply);
    /// 0.522's reader FUN_14379c7c0 drops the packet unless it is read to the end and has fewer fields.
    /// </summary>
    /// <remarks>
    /// 0.522 layout from Landmark-Emu's AbilityCasterInfoReply694 (46th test, the tools of our server work with it). The
    /// fields both have are copied in order; 0.695's recovery time, last u32s and bools and the executed-targeter list
    /// have no place in 0.522 and are left out. The executed-effect and keyword lists are sent empty (their 0.522 element
    /// layouts are not checked; our server sends them empty too).
    /// </remarks>
    public static class CasterInfoConverter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        /// <summary>u32 × 3 and 9 f32 and u32 × 2: one 0.695 ClientExecutedEffectData element.</summary>
        private const int Effect = 14 * 4;
        /// <summary>One keyword map entry: u32 key, u32 value.</summary>
        private const int Keyword = 8;

        public static byte[] Convert(byte[] packet)
        {
            try
            {
                return ConvertBody(packet);
            }
            catch (ArgumentOutOfRangeException)
            {
                log.Warn($"0.522: dropped a CasterInfoReply of {packet.Length} bytes, it is shorter than 0.695's layout.");
                return null;
            }
        }

        private static byte[] ConvertBody(byte[] packet)
        {
            var r = new Reader(packet, 4);
            var w = new List<byte>(packet.Length);
            w.AddRange(packet.AsSpan(0, 4).ToArray());

            w.Copy(r, 4);           // ability id
            w.Copy(r, 4);           // kind hash
            r.Skip(4);              // cost resource (0.695 only; 0.522 has one cost, the stamina)
            w.Copy(r, 4);           // cost amount → 0.522 +0x20
            r.Skip(1);              // ignore cost pool (0.695 only)
            w.Copy(r, 4);           // +0x24 (0.695 U2c)
            w.U32(0);               // +0x28
            w.U32(0);               // +0x2c
            w.Copy(r, 4);           // +0x70 cooldown
            w.Copy(r, 4);           // +0x74
            w.Copy(r, 4);           // +0x84 (0.695 U88)
            w.Copy(r, 4);           // +0x30
            w.Copy(r, 4);           // name id
            w.Copy(r, 1);           // updates tool mode
            w.Copy(r, 4);           // max range
            w.Copy(r, 4);           // min range
            w.Copy(r, 4);           // +0x68 repeats with harvest speed
            w.Copy(r, r.StringLength());
            w.Copy(r, r.StringLength());
            w.Copy(r, 4);           // arc degrees
            w.Copy(r, 4);           // +0x8c
            w.Copy(r, 1);           // +0x90
            w.Copy(r, 4);           // +0x98
            w.Copy(r, 4);           // +0x94
            w.Copy(r, 4);           // +0x9c (0.695 Ua0)
            w.Copy(r, 1);           // +0xa0
            w.Copy(r, 1);           // +0xa1
            w.Copy(r, 4);           // +0xa4
            w.Copy(r, 4);           // animation id
            w.Copy(r, 4);           // +0xac
            w.Copy(r, 4);           // +0xb0
            w.Copy(r, 3);           // +0xb4..+0xb6
            w.Copy(r, 4);           // cast time
            r.Skip(4);              // recovery time (0.695 only)
            w.Copy(r, 1);           // +0xb8
            w.Copy(r, 4);           // description id
            w.Copy(r, 1);           // +0xc0
            r.Skip(4 + (int)r.U32At() * Effect);
            w.U32(0);               // +0xe8 executed effects: none
            w.Copy(r, 6);           // +0xc1, +0xc2, +0xc5, +0xc6, +0xc7, +0xc8
            r.Skip(4 + (int)r.U32At() * Keyword);
            w.U32(0);               // +0x108 keywords: none
            w.Copy(r, 1);           // +0xc9
            w.Copy(r, 4);           // +0xcc max charge
            w.Copy(r, 4);           // +0xd0 min charge
            w.Copy(r, 12);          // +0xd4..+0xdc
            w.Copy(r, 3);           // +0xb7, +0xc3, +0xc4
            w.Copy(r, 4);           // +0xe0
            return w.ToArray();
        }

        private sealed class Reader
        {
            private readonly byte[] data;
            public int Position { get; private set; }

            public Reader(byte[] data, int position)
            {
                this.data = data;
                Position = position;
            }

            public ReadOnlySpan<byte> Take(int count)
            {
                ReadOnlySpan<byte> span = data.AsSpan(Position, count);
                Position += count;
                return span;
            }

            public void Skip(int count) => Take(count);
            public uint U32() => BitConverter.ToUInt32(Take(4));
            public uint U32At() => BitConverter.ToUInt32(data.AsSpan(Position, 4));
            /// <summary>The length of the string at the cursor with its u32 length in front.</summary>
            public int StringLength() => 4 + (int)U32At();
        }

        private static void Copy(this List<byte> w, Reader r, int count) => w.AddRange(r.Take(count).ToArray());
        private static void U32(this List<byte> w, uint value) => w.AddRange(BitConverter.GetBytes(value));
    }
}
