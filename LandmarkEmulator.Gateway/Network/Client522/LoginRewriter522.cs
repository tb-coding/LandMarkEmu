using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Shared.Network.Cryptography;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// One 0.522 client's login connection, rewritten datagram by datagram on its way to the login server. The server is
    /// told LoginUdp_10, whose character list and tint data are what 0.522 needs. Its replies pass unchanged, and of the
    /// client's packets CharacterCreateRequest and CharacterLoginRequest are converted.
    /// <para>
    /// Sequences and acks are left alone: the client and the server keep the reliable stream between them. Packet bodies
    /// are RC4 streams, one for each direction of the session, so once a packet changes length the server's stream runs
    /// ahead of the client's: everything the client sends is decrypted and encrypted again, in order. The relay and the
    /// client are on one PC, so packets arrive in order; a resent sequence gets the bytes sent the first time.
    /// </para>
    /// </summary>
    public sealed class LoginRewriter522
    {
        private const ushort SessionRequest = 0x01, SessionReply = 0x02, MultiPacket = 0x03, Data = 0x09, DataFragment = 0x0D;
        private const byte CharacterCreateRequest = 0x05, CharacterLoginRequest = 0x07;
        private const int CrcLength = 2;
        /// <summary>The login server's key (AuthServer.EncryptionKey, the client's built-in one).</summary>
        private const string Key = "F70IaxuU8C/w7FPXY1ibXw==";

        private static readonly byte[] Protocol9 = Encoding.ASCII.GetBytes("LoginUdp_9\0");
        private static readonly byte[] Protocol10 = Encoding.ASCII.GetBytes("LoginUdp_10\0");

        private readonly Arc4Provider fromClient = new(Convert.FromBase64String(Key));
        private readonly Arc4Provider toServer = new(Convert.FromBase64String(Key));
        private readonly Dictionary<ushort, byte[]> sent = new();
        private readonly ulong world;
        private uint crcSeed;
        private bool compression;
        /// <summary>Bytes of the fragmented packet still to come.</summary>
        private int fragmentLeft;

        /// <param name="world">The world id Play goes to: 0.522 asks for server 0.</param>
        public LoginRewriter522(ulong world = 1) => this.world = world;

        private static ushort OpcodeOf(byte[] datagram) => datagram.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(datagram) : (ushort)0;

        /// <summary>A datagram from the client, as the login server gets it.</summary>
        public byte[] FromClient(byte[] datagram)
        {
            switch (OpcodeOf(datagram))
            {
                case SessionRequest:
                    return ReplaceProtocol(datagram);
                case Data:
                case DataFragment:
                    return Wrap(datagram, Sequenced(OpcodeOf(datagram), Body(datagram)));
                case MultiPacket:
                    return Wrap(datagram, Multi(Body(datagram)));
                default:
                    return datagram;
            }
        }

        /// <summary>A datagram from the login server, as the client gets it.</summary>
        public byte[] FromServer(byte[] datagram)
        {
            if (OpcodeOf(datagram) == SessionReply)
            {
                // u16 opcode, u32 session, u32 CRC seed, u8 CRC length, u8 method, ...
                crcSeed = BinaryPrimitives.ReadUInt32BigEndian(datagram.AsSpan(6));
                compression = datagram[11] != 0;
            }
            return datagram;
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

        /// <summary>Sequence and payload of a Data or DataFragment, in the server's stream.</summary>
        private byte[] Sequenced(ushort opcode, byte[] body)
        {
            ushort sequence = BinaryPrimitives.ReadUInt16BigEndian(body);
            if (sent.TryGetValue(sequence, out byte[] first))
                return first;
            byte[] payload = opcode == Data ? WholePacket(body[2..]) : Fragment(body[2..]);
            var converted = new byte[2 + payload.Length];
            converted[0] = body[0];
            converted[1] = body[1];
            payload.CopyTo(converted, 2);
            sent[sequence] = converted;
            return converted;
        }

        private byte[] WholePacket(byte[] payload)
        {
            if (TrySplit(payload, out List<byte[]> pieces))
            {
                var joined = new List<byte> { 0x00, 0x19 };
                foreach (byte[] piece in pieces)
                {
                    byte[] converted = Convert522(piece);
                    WriteLength(joined, converted.Length);
                    joined.AddRange(converted);
                }
                return joined.ToArray();
            }
            return Convert522(payload);
        }

        /// <summary>Encrypted bytes with the optional 0x00 in front when the first one is 0 (DataStreamOutput.PackData).</summary>
        private byte[] Convert522(byte[] encrypted)
        {
            byte[] plain = Unpad(encrypted);
            fromClient.Decrypt(plain);
            byte[] converted = Rewrite(plain);
            toServer.Encrypt(converted);
            return Pad(converted);
        }

        private static byte[] Unpad(byte[] encrypted)
            => encrypted.Length > 1 && encrypted[0] == 0 && encrypted[1] == 0 ? encrypted[1..] : (byte[])encrypted.Clone();

        private static byte[] Pad(byte[] encrypted)
        {
            if (encrypted.Length == 0 || encrypted[0] != 0)
                return encrypted;
            var padded = new byte[encrypted.Length + 1];
            encrypted.CopyTo(padded, 1);
            return padded;
        }

        /// <summary>
        /// A piece of a fragmented packet: nothing to rewrite in those (LoginRequest and its like), but they move the RC4
        /// streams. The first piece starts with the packet's size.
        /// </summary>
        private byte[] Fragment(byte[] payload)
        {
            int at = 0;
            bool first = fragmentLeft == 0;
            if (first)
            {
                fragmentLeft = BinaryPrimitives.ReadInt32BigEndian(payload);
                at = 4;
            }
            byte[] encrypted = payload[at..];
            bool padded = first && encrypted.Length > 1 && encrypted[0] == 0 && encrypted[1] == 0;
            byte[] plain = padded ? encrypted[1..] : (byte[])encrypted.Clone();
            fragmentLeft -= encrypted.Length;
            fromClient.Decrypt(plain);
            toServer.Encrypt(plain);

            var converted = new List<byte>();
            if (first)
            {
                bool padNow = plain.Length > 0 && plain[0] == 0;
                int size = fragmentLeft + plain.Length + (padNow ? 1 : 0);
                var length = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(length, size);
                converted.AddRange(length);
                if (padNow)
                    converted.Add(0);
            }
            converted.AddRange(plain);
            return converted.ToArray();
        }

        private byte[] Rewrite(byte[] packet)
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
                    if (BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(9)) == 0)
                        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(9), world);
                    return packet;
                default:
                    return packet;
            }
        }

        /// <summary>Sub-packets carry no flag or CRC of their own.</summary>
        private byte[] Multi(byte[] body)
        {
            var parts = new List<byte>();
            for (int at = 0; at < body.Length;)
            {
                int length = ReadLength(body, ref at);
                byte[] sub = body[at..(at + length)];
                at += length;
                ushort opcode = OpcodeOf(sub);
                if (opcode is Data or DataFragment)
                {
                    byte[] inner = Sequenced(opcode, sub[2..]);
                    var whole = new byte[2 + inner.Length];
                    whole[0] = sub[0];
                    whole[1] = sub[1];
                    inner.CopyTo(whole, 2);
                    sub = whole;
                }
                WriteLength(parts, sub.Length);
                parts.AddRange(sub);
            }
            return parts.ToArray();
        }

        /// <summary>The client's 0x00 0x19 form: several length-prefixed packets in one.</summary>
        private static bool TrySplit(byte[] payload, out List<byte[]> pieces)
        {
            pieces = null;
            if (payload.Length < 2 || payload[0] != 0x00 || payload[1] != 0x19)
                return false;
            var found = new List<byte[]>();
            for (int at = 2; at < payload.Length;)
            {
                int length = ReadLength(payload, ref at);
                if (length == 0 || at + length > payload.Length)
                    return false;
                found.Add(payload[at..(at + length)]);
                at += length;
            }
            pieces = found;
            return found.Count > 0;
        }

        /// <summary>One byte below 0xFF, else 0xFF and a u16 (a lone 0xFF before a packet's 00 opcode byte is 255).</summary>
        private static int ReadLength(byte[] data, ref int at)
        {
            int length = data[at++];
            if (length != 0xFF || at + 2 > data.Length || data[at] == 0x00)
                return length;
            length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
            at += 2;
            return length;
        }

        private static void WriteLength(List<byte> output, int length)
        {
            if (length < 0xFF)
            {
                output.Add((byte)length);
                return;
            }
            output.Add(0xFF);
            output.Add((byte)(length >> 8));
            output.Add((byte)length);
        }

        private byte[] Body(byte[] datagram) => SoeCompression.UnwrapBody(datagram[2..^CrcLength], compression);

        private byte[] Wrap(byte[] datagram, byte[] body)
        {
            byte[] wrapped = compression ? SoeCompression.WrapBody(body) : body;
            var full = new byte[2 + wrapped.Length];
            full[0] = datagram[0];
            full[1] = datagram[1];
            wrapped.CopyTo(full, 2);
            return CrcProvider.AppendCRC(full, crcSeed);
        }
    }
}
