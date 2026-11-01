using LandmarkEmulator.Gateway.Network.Client522;
using LandmarkEmulator.Gateway.Network.Message;
using NLog;
using System;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network
{
    /// <summary>
    /// Stands between a client whose numbering differs from the server's and the zone: the server speaks the 0.695 client's
    /// opcodes (ClientBuilds), so everything else is converted at the gateway, one packet at a time. A null result drops
    /// the packet.
    /// </summary>
    public interface IClientTranslator
    {
        /// <summary>A packet from the client, in the server's numbering.</summary>
        byte[] Inbound(Channel channel, byte[] data);

        /// <summary>A packet for the client, in the client's numbering.</summary>
        byte[] Outbound(Channel channel, byte[] data);
    }

    /// <summary>The 0.695 client: packets pass unchanged.</summary>
    public sealed class PassThroughTranslator : IClientTranslator
    {
        public static readonly PassThroughTranslator Instance = new();

        public byte[] Inbound(Channel channel, byte[] data) => data;
        public byte[] Outbound(Channel channel, byte[] data) => data;
    }

    /// <summary>
    /// The 0.522 client. Every base opcode both clients register has the same number, so bases are not translated; opcodes
    /// the client doesn't register are dropped both ways (a packet it can't name is no use to it, and one from it can't be
    /// trusted to mean the same thing here). PlayerUpdate 0x26 has a u8 sub in 0.522 and a u32 sub in 0.695, and from
    /// <see cref="PlayerUpdateNew"/> on 0.522's subs are one lower. Claims 0xF9 subs from 0x18 on are one lower in 0.522. The
    /// position channel has no opcode and passes unchanged.
    /// Of the login packets, SendZoneDetails and SendSelfToClient are rewritten to 0.522's layouts and ReferenceData keeps
    /// only an empty profile list; other bodies are not converted.
    /// </summary>
    public sealed class Client522Translator : IClientTranslator
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private const byte PlayerUpdate = 0x26;
        private const byte SendSelf = 0x0F, ZoneDetails = 0x2E, ReferenceData = 0x2F;
        private const byte Claims = 0xF9;
        private const byte ProfileDefinitionsSub = 3;
        /// <summary>The 0.522 Claims subs one lower than 0.695's (vtable constants: 0x18 to SetSupplementalData 0x3D).</summary>
        private const byte ClaimsShiftFirst = 0x18, ClaimsShiftLast = 0x3D;

        /// <summary>The opcodes the 0.522 client registers (FUN_143a4a330).</summary>
        private static readonly HashSet<int> Registered = new()
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30,
            0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40,
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50,
            0x51, 0x52, 0x53, 0x54, 0x55, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61,
            0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71,
            0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x80, 0x81,
            0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x8F, 0x90, 0x91,
            0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, 0xA0, 0xA1,
            0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB0, 0xB1,
            0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xC1,
            0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF, 0xD0, 0xD1,
            0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE0, 0xE1,
            0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF, 0xF0, 0xF1,
            0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFD, 0xFF, 0x100, 0x102, 0x103, 0x104,
            0x105, 0x106, 0x107, 0x108, 0x109, 0x10A, 0x10B, 0x10C, 0x10D, 0x10E, 0x10F, 0x110, 0x111, 0x112, 0x113, 0x114,
            0x115, 0x116, 0x117,
        };

        /// <summary>
        /// The 0.695 PlayerUpdate sub 0.522 doesn't have (StopCompositeEffect). The subs after it are one lower in 0.522:
        /// checked by name from 0xD to 0x76 against the client's vtable constants.
        /// </summary>
        private const byte PlayerUpdateNew = 0x0C;

        /// <summary>
        /// What the zone may send the client for now: the packets the 0.522 login needs. Everything else has a body laid
        /// out for 0.695 and reads wrong or crashes the client, so it stays back until it is converted.
        /// </summary>
        private static readonly HashSet<int> Converted = new()
        {
            0x03, 0x0F, 0x11, 0x12, 0x13, 0x26, 0x29, 0x2E, 0x2F, 0x37, 0xC3, 0xEA, 0xEC, 0xF9, 0x116,
        };

        /// <summary>Held-back opcodes, each logged once per connection.</summary>
        private readonly HashSet<int> heldBack = new();

        private static int? OpcodeOf(byte[] data) => data.Length >= 2 ? data[0] | (data[1] << 8) : null;

        public byte[] Inbound(Channel channel, byte[] data)
        {
            if (channel == Channel.PositionUpdate)
                return data;
            if (OpcodeOf(data) is not int opcode || !Registered.Contains(opcode))
            {
                log.Warn($"0.522: dropped a client packet with an unknown opcode: {Show(data)}");
                return null;
            }
            if (opcode == Claims)
                return data.Length >= 3 && data[2] is >= ClaimsShiftFirst and <= ClaimsShiftLast ? WithSub(data, (byte)(data[2] + 1)) : data;
            if (opcode != PlayerUpdate)
                return data;
            if (data.Length < 3)
                return null;

            // 26 00 ss body -> 26 00 ss 00 00 00 body
            var converted = new byte[data.Length + 3];
            converted[0] = data[0];
            converted[1] = data[1];
            converted[2] = data[2] >= PlayerUpdateNew ? (byte)(data[2] + 1) : data[2];
            Array.Copy(data, 3, converted, 6, data.Length - 3);
            return converted;
        }

        public byte[] Outbound(Channel channel, byte[] data)
        {
            if (channel == Channel.PositionUpdate)
                return data;
            if (OpcodeOf(data) is not int opcode || !Registered.Contains(opcode))
            {
                log.Warn($"0.522: dropped a server packet with an opcode the client doesn't have: {Show(data)}");
                return null;
            }
            if (!Converted.Contains(opcode))
            {
                if (heldBack.Add(opcode))
                    log.Info($"0.522: holding back opcode 0x{opcode:X}: {Show(data)}");
                return null;
            }
            if (opcode == Claims)
                return data.Length >= 3 && data[2] is > ClaimsShiftFirst and <= ClaimsShiftLast + 1 ? WithSub(data, (byte)(data[2] - 1)) : data;
            if (opcode == SendSelf)
                return SendSelfConverter.Convert(data);
            if (opcode == ZoneDetails)
                return TrimZoneDetails(data);
            if (opcode == ReferenceData)
                return data.Length >= 4 && data[2] == ProfileDefinitionsSub ? new byte[] { data[0], data[1], data[2], data[3], 0, 0, 0, 0 } : null;
            if (opcode != PlayerUpdate)
                return data;
            if (data.Length < 6)
                return null;
            if (data[2] == PlayerUpdateNew)
            {
                log.Warn("0.522: dropped PlayerUpdate sub 0x0C, the client doesn't have it.");
                return null;
            }

            // 26 00 ss 00 00 00 body -> 26 00 ss body
            var converted = new byte[data.Length - 3];
            converted[0] = data[0];
            converted[1] = data[1];
            converted[2] = data[2] > PlayerUpdateNew ? (byte)(data[2] - 1) : data[2];
            Array.Copy(data, 6, converted, 3, data.Length - 6);
            return converted;
        }

        /// <summary>
        /// 0.522 doesn't read the end of the zone details: the continent's ProceduralLayerType (u32) and DistanceField
        /// (string), then IsNewbieZone (bool). The string is found from the back.
        /// </summary>
        public static byte[] TrimZoneDetails(byte[] data)
        {
            int last = data.Length - 1;
            for (int length = 0; length < 256 && last - length - 4 >= 2; length++)
            {
                int start = last - length - 4;
                if (BitConverter.ToInt32(data, start) == length)
                    return data[..(start - 4)];
            }
            log.Warn("0.522: zone details without the expected end, sent as they are.");
            return data;
        }

        private static byte[] WithSub(byte[] data, byte sub)
        {
            var converted = (byte[])data.Clone();
            converted[2] = sub;
            return converted;
        }

        private static string Show(byte[] data) => BitConverter.ToString(data, 0, Math.Min(data.Length, 16));
    }
}
