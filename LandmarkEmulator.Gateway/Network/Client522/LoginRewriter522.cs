using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Shared.Network.Cryptography;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// One 0.522 client's login connection, rewritten datagram by datagram on its way to and from the login server. The
    /// server is told LoginUdp_10, whose character list is the layout 0.522 reads, and the few packets that differ are
    /// converted. Sequences and acks are left alone: the client and the server keep the reliable stream between them.
    /// Fragments pass untouched (the only large packets are LoginRequest, the same in both, and the tint data).
    /// </summary>
    public sealed class LoginRewriter522
    {
        private const ushort SessionRequest = 0x01, SessionReply = 0x02, MultiPacket = 0x03, Data = 0x09;
        private const byte LoginReply = 0x02, CharacterCreateRequest = 0x05, CharacterLoginRequest = 0x07,
            CharacterSelectInfoReply = 0x0C, ServerListReply = 0x0E;
        private const int CrcLength = 2;

        private static readonly byte[] Protocol9 = Encoding.ASCII.GetBytes("LoginUdp_9\0");
        private static readonly byte[] Protocol10 = Encoding.ASCII.GetBytes("LoginUdp_10\0");

        private uint crcSeed;
        private bool compression;
        /// <summary>The first world in the server list: 0.522 asks to play on server 0.</summary>
        private ulong firstServer;

        private static ushort OpcodeOf(byte[] datagram) => datagram.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(datagram) : (ushort)0;

        /// <summary>A datagram from the client, as the login server gets it.</summary>
        public byte[] FromClient(byte[] datagram)
        {
            switch (OpcodeOf(datagram))
            {
                case SessionRequest:
                    return ReplaceProtocol(datagram);
                case Data:
                    return RewriteData(datagram, RewriteClientPacket);
                case MultiPacket:
                    return RewriteMulti(datagram, RewriteClientPacket);
                default:
                    return datagram;
            }
        }

        /// <summary>A datagram from the login server, as the client gets it.</summary>
        public byte[] FromServer(byte[] datagram)
        {
            switch (OpcodeOf(datagram))
            {
                case SessionReply:
                    // u16 opcode, u32 session, u32 CRC seed, u8 CRC length, u8 method, ...
                    crcSeed = BinaryPrimitives.ReadUInt32BigEndian(datagram.AsSpan(6));
                    compression = datagram[11] != 0;
                    return datagram;
                case Data:
                    return RewriteData(datagram, RewriteServerPacket);
                default:
                    return datagram;
            }
        }

        private static byte[] ReplaceProtocol(byte[] datagram)
        {
            int at = datagram.AsSpan().IndexOf(Protocol9);
            if (at < 0)
                return datagram;
            var replaced = new byte[datagram.Length + 1];
            Array.Copy(datagram, replaced, at);
            Protocol10.CopyTo(replaced, at);
            return replaced;
        }

        private byte[] RewriteClientPacket(byte[] packet)
        {
            switch (packet[0])
            {
                case CharacterCreateRequest:
                    // 0.522 ends after the race and gender; protocol 10 has StartingOutfit after them.
                    var longer = new byte[packet.Length + 4];
                    packet.CopyTo(longer, 0);
                    uint payload = BinaryPrimitives.ReadUInt32LittleEndian(longer.AsSpan(9));
                    BinaryPrimitives.WriteUInt32LittleEndian(longer.AsSpan(9), payload + 4);
                    return longer;
                case CharacterLoginRequest:
                    if (BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(9)) != 0 || firstServer == 0)
                        return packet;
                    var chosen = (byte[])packet.Clone();
                    BinaryPrimitives.WriteUInt64LittleEndian(chosen.AsSpan(9), firstServer);
                    return chosen;
                default:
                    return packet;
            }
        }

        private byte[] RewriteServerPacket(byte[] packet)
        {
            switch (packet[0])
            {
                case ServerListReply:
                    if (BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(1)) > 0)
                        firstServer = BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(5));
                    return packet;
                case LoginReply:
                    return LoginReply9(packet);
                case CharacterSelectInfoReply:
                    return ListUnderServer(packet);
                default:
                    return packet;
            }
        }

        /// <summary>
        /// 0.522 leaves out a character whose last server isn't a listed one, and one made through the login server has
        /// server 0 until it enters a world: those go under the first server.
        /// </summary>
        private byte[] ListUnderServer(byte[] packet)
        {
            if (firstServer == 0)
                return packet;
            byte[] list = packet;
            int at = 1 + 4 + 1;     // opcode, status, can bypass the server lock
            int count = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(at));
            at += 4;
            for (int i = 0; i < count; i++)
            {
                // u64 character, u64 last server, f64 last login, u32 status, u32 size, payload
                if (BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(at + 8)) == 0)
                {
                    if (ReferenceEquals(list, packet))
                        list = (byte[])packet.Clone();
                    BinaryPrimitives.WriteUInt64LittleEndian(list.AsSpan(at + 8), firstServer);
                }
                at += 8 + 8 + 8 + 4;
                at += 4 + BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(at));
            }
            return list;
        }

        /// <summary>The LoginUdp_9 reply: no result field, and the namespace is followed by one empty list.</summary>
        private static byte[] LoginReply9(byte[] packet)
        {
            var reader = new GamePacketReader(packet[1..]);
            bool loggedIn = reader.ReadBool();
            uint status = reader.ReadUInt();
            reader.ReadUInt();              // result
            bool member = reader.ReadBool();
            bool isInternal = reader.ReadBool();
            string space = reader.ReadString();

            var bytes = new List<byte> { LoginReply };
            var writer = new GamePacketWriter(bytes);
            writer.Write(loggedIn);
            writer.Write(status);
            writer.Write(member);
            writer.Write(isInternal);
            writer.Write(space);
            writer.Write(0u);
            return bytes.ToArray();
        }

        private byte[] RewriteData(byte[] datagram, Func<byte[], byte[]> rewrite)
        {
            byte[] body = Body(datagram);
            byte[] rewritten = RewriteSequenced(body, rewrite);
            return ReferenceEquals(rewritten, body) ? datagram : Wrap(datagram, rewritten);
        }

        /// <summary>Sub-packets carry no flag or CRC of their own; a Data one may hold a packet to rewrite.</summary>
        private byte[] RewriteMulti(byte[] datagram, Func<byte[], byte[]> rewrite)
        {
            byte[] body = Body(datagram);
            var parts = new List<byte>();
            bool changed = false;
            for (int at = 0; at < body.Length;)
            {
                int length = body[at++];
                if (length == 0xFF || at + length > body.Length)
                    return datagram;    // the long forms are for large packets, which are never rewritten
                byte[] sub = body[at..(at + length)];
                at += length;
                if (sub.Length > 2 && OpcodeOf(sub) == Data)
                {
                    byte[] inner = sub[2..];
                    byte[] rewritten = RewriteSequenced(inner, rewrite);
                    if (!ReferenceEquals(rewritten, inner))
                    {
                        changed = true;
                        sub = new[] { sub[0], sub[1] }.Concat2(rewritten);
                        if (sub.Length >= 0xFF)
                            return datagram;
                    }
                }
                parts.Add((byte)sub.Length);
                parts.AddRange(sub);
            }
            return changed ? Wrap(datagram, parts.ToArray()) : datagram;
        }

        /// <summary>Sequence, then the packet: the same array back when nothing changed.</summary>
        private static byte[] RewriteSequenced(byte[] body, Func<byte[], byte[]> rewrite)
        {
            if (body.Length < 3)
                return body;
            byte[] packet = body[2..];
            byte[] rewritten = rewrite(packet);
            return ReferenceEquals(rewritten, packet) ? body : new[] { body[0], body[1] }.Concat2(rewritten);
        }

        private byte[] Body(byte[] datagram)
        {
            byte[] body = datagram[2..^CrcLength];
            return SoeCompression.UnwrapBody(body, compression);
        }

        private byte[] Wrap(byte[] datagram, byte[] body)
        {
            byte[] wrapped = compression ? SoeCompression.WrapBody(body) : body;
            return CrcProvider.AppendCRC(new[] { datagram[0], datagram[1] }.Concat2(wrapped), crcSeed);
        }
    }

    internal static class ByteArrays
    {
        public static byte[] Concat2(this byte[] first, byte[] second)
        {
            var joined = new byte[first.Length + second.Length];
            first.CopyTo(joined, 0);
            second.CopyTo(joined, first.Length);
            return joined;
        }
    }
}
