using LandmarkEmulator.Gateway.Network.Client522;
using LandmarkEmulator.Shared.Network.Cryptography;
using LandmarkEmulator.Tests.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>The 0.522 client's login, rewritten for the login server (LoginRewriter522).</summary>
    public class LoginRewriter522Tests
    {
        private const uint Seed = 0x12345678;
        private const string Key = "F70IaxuU8C/w7FPXY1ibXw==";

        private readonly LoginRewriter522 rewriter = new(world: 7);
        // The client's stream and the server's: what the two ends of the relay hold.
        private readonly Arc4Provider client = new(Convert.FromBase64String(Key));
        private readonly Arc4Provider server = new(Convert.FromBase64String(Key));
        private ushort sequence;

        public LoginRewriter522Tests()
        {
            // u16 opcode, u32 session, u32 seed, u8 CRC length, u8 method 1, u8 method 2, u32 UDP size, u32 version
            byte[] reply = Wire.Hex("00 02 00 00 00 01 12 34 56 78 02 01 00 00 00 02 00 00 00 00 03");
            Assert.Same(reply, rewriter.FromServer(reply));
        }

        private static byte[] Padded(byte[] encrypted)
            => encrypted[0] == 0 ? new byte[] { 0 }.Concat(encrypted).ToArray() : encrypted;

        private byte[] DataPacket(byte[] packet)
        {
            byte[] encrypted = (byte[])packet.Clone();
            client.Encrypt(encrypted);
            encrypted = Padded(encrypted);
            byte[] body = new[] { (byte)(sequence >> 8), (byte)sequence }.Concat(encrypted).ToArray();
            sequence++;
            return CrcProvider.AppendCRC(new byte[] { 0x00, 0x09, 0 }.Concat(body).ToArray(), Seed);
        }

        private byte[] PacketOf(byte[] datagram)
        {
            Assert.True(CrcProvider.TryVerifyAndStrip(datagram, Seed, 2, out byte[] plain), "CRC");
            byte[] body = SoeCompression.UnwrapBody(plain[2..], true);
            return Decrypted(body[2..]);
        }

        private byte[] Decrypted(byte[] payload)
        {
            byte[] encrypted = payload.Length > 1 && payload[0] == 0 && payload[1] == 0 ? payload[1..] : (byte[])payload.Clone();
            server.Decrypt(encrypted);
            return encrypted;
        }

        private static byte[] Create() => Wire.Write(w =>
        {
            w.Write((byte)0x05);
            w.Write(0ul);
            w.Write(30u);
            w.WriteBytes(new byte[30]);
        });

        [Fact]
        public void SessionRequest_AsksForProtocol10()
        {
            byte[] request = Wire.Hex("00 01 00 00 00 03 00 00 00 01 00 00 02 00").Concat(Encoding.ASCII.GetBytes("LoginUdp_9\0")).ToArray();
            string text = Encoding.ASCII.GetString(rewriter.FromClient(request)[14..]);
            Assert.Equal("LoginUdp_10\0", text);
        }

        [Fact]
        public void ServerPackets_PassUnchanged()
        {
            byte[] data = Wire.Hex("00 09 00 00 01 AA BB CC DD");
            Assert.Same(data, rewriter.FromServer(data));
        }

        [Fact]
        public void CharacterCreate_GetsAStartingOutfit()
        {
            byte[] request = Create();

            byte[] packet = PacketOf(rewriter.FromClient(DataPacket(request)));

            Assert.Equal(request.Length + 4, packet.Length);
            Assert.Equal(34u, BitConverter.ToUInt32(packet, 9));
            Assert.Equal(request[13..], packet[13..^4]);
        }

        [Fact]
        public void PacketsAfterACreate_FollowTheServersStream()
        {
            rewriter.FromClient(DataPacket(Create()));
            server.Decrypt(new byte[Create().Length + 4]);

            byte[] other = { 0x0B, 1, 2, 3, 4 };
            Assert.Equal(other, PacketOf(rewriter.FromClient(DataPacket(other))));
        }

        [Fact]
        public void CharacterLogin_ServerZeroBecomesTheWorld()
        {
            byte[] login = Wire.Write(w =>
            {
                w.Write((byte)0x07);
                w.Write(1ul);
                w.Write(0ul);
                w.Write(0u);
            });

            byte[] packet = PacketOf(rewriter.FromClient(DataPacket(login)));

            Assert.Equal(7ul, BitConverter.ToUInt64(packet, 9));
        }

        [Fact]
        public void ResentPacket_GetsTheSameBytes()
        {
            byte[] datagram = DataPacket(Create());
            byte[] first = rewriter.FromClient(datagram);
            byte[] other = { 0x0B, 1 };
            rewriter.FromClient(DataPacket(other));

            Assert.Equal(first, rewriter.FromClient(datagram));
        }

        [Fact]
        public void Fragments_KeepTheStreamsInStep()
        {
            byte[] large = new byte[]{ 0x01 }.Concat(Enumerable.Range(0, 700).Select(i => (byte)i)).ToArray();
            byte[] encrypted = (byte[])large.Clone();
            client.Encrypt(encrypted);
            encrypted = Padded(encrypted);
            byte[] framed = BitConverter.GetBytes(encrypted.Length).Reverse().Concat(encrypted).ToArray();

            var received = new List<byte>();
            for (int at = 0; at < framed.Length; at += 400)
            {
                byte[] piece = framed[at..Math.Min(at + 400, framed.Length)];
                byte[] body = new[] { (byte)(sequence >> 8), (byte)sequence }.Concat(piece).ToArray();
                sequence++;
                byte[] datagram = CrcProvider.AppendCRC(new byte[] { 0x00, 0x0D, 0 }.Concat(body).ToArray(), Seed);
                byte[] result = rewriter.FromClient(datagram);
                Assert.True(CrcProvider.TryVerifyAndStrip(result, Seed, 2, out byte[] plain), "CRC");
                received.AddRange(SoeCompression.UnwrapBody(plain[2..], true)[2..]);
            }

            int size = received[0] << 24 | received[1] << 16 | received[2] << 8 | received[3];
            Assert.Equal(received.Count - 4, size);
            Assert.Equal(large, Decrypted(received.Skip(4).ToArray()));

            byte[] after = { 0x0B, 9 };
            Assert.Equal(after, PacketOf(rewriter.FromClient(DataPacket(after))));
        }

        [Fact]
        public void MultiPacket_RewritesItsDataPacket()
        {
            byte[] ack = Wire.Hex("00 15 00 03");
            byte[] play = Wire.Write(w =>
            {
                w.Write((byte)0x07);
                w.Write(1ul);
                w.Write(0ul);
            });
            byte[] encrypted = (byte[])play.Clone();
            client.Encrypt(encrypted);
            byte[] data = new byte[] { 0x00, 0x09, 0x00, 0x06 }.Concat(Padded(encrypted)).ToArray();
            var body = new List<byte> { 0, (byte)ack.Length };
            body.AddRange(ack);
            body.Add((byte)data.Length);
            body.AddRange(data);
            byte[] multi = CrcProvider.AppendCRC(new byte[] { 0x00, 0x03 }.Concat(body).ToArray(), Seed);

            byte[] result = rewriter.FromClient(multi);

            Assert.True(CrcProvider.TryVerifyAndStrip(result, Seed, 2, out byte[] plain), "CRC");
            byte[] inner = SoeCompression.UnwrapBody(plain[2..], true);
            int at = 1 + ack.Length;
            byte[] sub = inner[(at + 1)..(at + 1 + inner[at])];
            Assert.Equal(7ul, BitConverter.ToUInt64(Decrypted(sub[4..]), 9));
        }
    }
}
