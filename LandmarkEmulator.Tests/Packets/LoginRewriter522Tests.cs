using LandmarkEmulator.AuthServer.Network.Message.Model;
using LandmarkEmulator.Gateway.Network.Client522;
using LandmarkEmulator.Shared.Network;
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
        private readonly LoginRewriter522 rewriter = new();

        public LoginRewriter522Tests()
        {
            // u16 opcode, u32 session, u32 seed, u8 CRC length, u8 method 1, u8 method 2, u32 UDP size, u32 version
            byte[] reply = Wire.Hex("00 02 00 00 00 01 12 34 56 78 02 01 00 00 00 02 00 00 00 00 03");
            Assert.Same(reply, rewriter.FromServer(reply));
        }

        private static byte[] DataPacket(byte[] packet, bool zlib = false)
        {
            byte[] body = new byte[] { 0x00, 0x05 }.Concat(packet).ToArray();
            byte[] wrapped = zlib ? SoeCompression.WrapBody(body) : new byte[] { 0 }.Concat(body).ToArray();
            return CrcProvider.AppendCRC(new byte[] { 0x00, 0x09 }.Concat(wrapped).ToArray(), Seed);
        }

        private static byte[] PacketOf(byte[] datagram)
        {
            Assert.True(CrcProvider.TryVerifyAndStrip(datagram, Seed, 2, out byte[] plain), "CRC");
            byte[] body = SoeCompression.UnwrapBody(plain[2..], true);
            Assert.Equal(new byte[] { 0x00, 0x05 }, body[..2]);
            return body[2..];
        }

        [Fact]
        public void SessionRequest_AsksForProtocol10()
        {
            byte[] request = Wire.Hex("00 01 00 00 00 03 00 00 00 01 00 00 02 00").Concat(Encoding.ASCII.GetBytes("LoginUdp_9\0")).ToArray();
            string text = Encoding.ASCII.GetString(rewriter.FromClient(request)[14..]);
            Assert.Equal("LoginUdp_10\0", text);
        }

        [Fact]
        public void OtherDatagrams_PassUnchanged()
        {
            byte[] ack = Wire.Hex("00 15 00 00 05 AA BB");
            Assert.Same(ack, rewriter.FromClient(ack));
            byte[] fragment = Wire.Hex("00 0D 00 00 05 01 02 03 AA BB");
            Assert.Same(fragment, rewriter.FromServer(fragment));
            byte[] other = DataPacket(new byte[] { 0x0B, 1, 2 });
            Assert.Same(other, rewriter.FromClient(other));
        }

        [Fact]
        public void CharacterCreate_GetsAStartingOutfit()
        {
            byte[] request = Wire.Write(w =>
            {
                w.Write((byte)0x05);
                w.Write(0ul);
                w.Write(30u);
                w.WriteBytes(new byte[30]);
            });

            byte[] packet = PacketOf(rewriter.FromClient(DataPacket(request)));

            Assert.Equal(request.Length + 4, packet.Length);
            Assert.Equal(34u, BitConverter.ToUInt32(packet, 9));
            Assert.Equal(request[13..], packet[13..^4]);
        }

        [Fact]
        public void CharacterLogin_ServerZeroBecomesTheFirstInTheList()
        {
            byte[] list = Wire.Write(w =>
            {
                w.Write((byte)0x0E);
                w.Write(1u);
                w.Write(7ul);
            });
            byte[] data = DataPacket(list);
            Assert.Same(data, rewriter.FromServer(data));

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
        public void CharacterList_ServerZeroGoesUnderTheFirstServer()
        {
            rewriter.FromServer(DataPacket(Wire.Write(w =>
            {
                w.Write((byte)0x0E);
                w.Write(1u);
                w.Write(7ul);
            })));
            var reply = new CharacterSelectInfoReply { Status = 1 };
            reply.Characters.Add(new CharacterSelectInfoReply.Character { CharacterId = 4, CharacterData = new() });
            byte[] list = new byte[] { 0x0C }.Concat(Wire.Write(reply)).ToArray();

            byte[] packet = PacketOf(rewriter.FromServer(DataPacket(list)));

            var read = new CharacterSelectInfoReply();
            read.Read(new GamePacketReader(packet[1..]));
            Assert.Equal(7ul, read.Characters[0].LastServerId);
        }

        [Fact]
        public void LoginReply_IsTheProtocol9Layout()
        {
            var modern = new LoginReply { LoggedIn = true, Status = 1, IsMember = true };
            byte[] reply = new byte[] { 0x02 }.Concat(Wire.Write(modern)).ToArray();

            byte[] packet = PacketOf(rewriter.FromServer(DataPacket(reply, zlib: true)));

            var expected = new LoginReply_9 { LoggedIn = true, Status = 1, IsMember = true };
            Assert.Equal(Wire.Write(expected), packet[1..]);
        }

        [Fact]
        public void MultiPacket_RewritesItsDataPacket()
        {
            byte[] ack = Wire.Hex("00 15 00 03");
            byte[] login = Wire.Write(w =>
            {
                w.Write((byte)0x0E);
                w.Write(1u);
                w.Write(5ul);
            });
            rewriter.FromServer(DataPacket(login));
            byte[] play = Wire.Write(w =>
            {
                w.Write((byte)0x07);
                w.Write(1ul);
                w.Write(0ul);
            });
            byte[] data = new byte[] { 0x00, 0x09, 0x00, 0x06 }.Concat(play).ToArray();
            var body = new List<byte> { 0 };
            body.Add((byte)ack.Length);
            body.AddRange(ack);
            body.Add((byte)data.Length);
            body.AddRange(data);
            byte[] multi = CrcProvider.AppendCRC(new byte[] { 0x00, 0x03 }.Concat(body).ToArray(), Seed);

            byte[] result = rewriter.FromClient(multi);

            Assert.True(CrcProvider.TryVerifyAndStrip(result, Seed, 2, out byte[] plain), "CRC");
            byte[] inner = SoeCompression.UnwrapBody(plain[2..], true);
            Assert.Equal(5ul, BitConverter.ToUInt64(inner, 1 + ack.Length + 1 + 4 + 9));
        }
    }
}
