using LandmarkEmulator.Gateway.Network.Client522;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Tests.Support;
using System;
using System.Linq;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>Site records rewritten for the 0.522 client (ClaimConverter).</summary>
    public class ClaimConverterTests
    {
        /// <summary>A 0.695 record up to the guild list; both layouts share it.</summary>
        private static void Head(GamePacketWriter w)
        {
            w.Write(5ul);
            w.Write(0ul);
            w.Write(16ul);
            w.Write("Site");
            w.Write(0ul);
            w.Write("Site");
            w.Write("Welcome");
            w.Write("");
            w.Write(0u);
            for (int i = 0; i < 6; i++)
                w.Write((double)i);
            w.Write(3u);
            w.Write(0u);
            w.Write(0u);
            w.Write(0u);        // players
            w.Write(0u);        // guilds
        }

        private static void Middle(GamePacketWriter w)
        {
            w.Write(0u);        // named list
            w.Write(0u);
            w.Write(0ul);
            w.Write(false);
            w.Write(0u);
            w.Write(0u);
            w.Write(0u);
            for (int i = 0; i < 5; i++)
                w.Write("");
            w.Write(0ul);
        }

        private static void Game(GamePacketWriter w, bool extra)
        {
            w.Write(0ul);
            w.Write(0u);
            w.Write(false);
            for (int i = 0; i < 4; i++)
                w.Write(0u);
            w.Write(false);
            w.Write(0u);
            w.Write("");
            w.Write(0u);
            if (extra)
                w.Write(1u);
        }

        private static byte[] Record695() => Wire.Write(w =>
        {
            Head(w);
            Middle(w);
            Game(w, true);
            w.Write(0ul);
            w.Write("");
            w.Write(0u);
            w.Write(-1);
            w.Write(1f);
            w.Write(-1);
            w.Write(1f);
            w.Write(0u);
            w.Write(0u);
            w.Write(0u);
            w.Write(0ul);
        });

        private static byte[] Record522() => Wire.Write(w =>
        {
            Head(w);
            w.Write(0u);
            w.Write(0u);
            Middle(w);
            Game(w, false);
            w.Write(0ul);
            w.Write("");
            w.Write(0u);
        });

        private static byte[] Packet(byte sub, params byte[][] parts)
            => new byte[] { 0xF9, 0x00, sub }.Concat(parts.SelectMany(p => p)).ToArray();

        [Fact]
        public void AoiUpdate_RewritesItsRecords()
        {
            byte[] count = BitConverter.GetBytes(1u);
            byte[] rest = Wire.Hex("01 00 00 00 09 00 00 00 00 00 00 00");
            byte[] result = ClaimConverter.Convert(Packet(4, new byte[] { 1 }, count, Record695(), rest));
            Assert.Equal(Packet(4, new byte[] { 1 }, count, Record522(), rest), result);
        }

        [Fact]
        public void PlaceComplete_RewritesItsRecord()
        {
            byte[] placed = BitConverter.GetBytes(2u);
            Assert.Equal(Packet(7, placed, Record522()), ClaimConverter.Convert(Packet(7, placed, Record695())));
        }

        [Fact]
        public void PlaceComplete_Refused_HasNoRecord()
        {
            byte[] refused = Packet(7, BitConverter.GetBytes(3u));
            Assert.Equal(refused, ClaimConverter.Convert(refused));
        }

        [Fact]
        public void ManagementData_GetsTheFieldsOf522()
        {
            byte[] packet = Packet(8, Wire.Write(w =>
            {
                w.Write(86400ul);
                w.Write(1u);
                w.Write(false);
                w.Write(5ul);
                w.Write("Site");
                w.Write(false);
                w.Write(0ul);
                w.Write("");
                w.Write(1u);
                w.Write(406u);
            }));
            byte[] expected = Packet(8, Wire.Write(w =>
            {
                w.Write(86400ul);
                w.Write(1u);
                w.Write(false);
                w.Write(5ul);
                w.Write("Site");
                w.Write(false);
                w.Write(0ul);
                w.Write("");
                w.Write(0u);
                w.Write(1u);
                w.Write(406u);
                w.Write(0u);
            }));
            Assert.Equal(expected, ClaimConverter.Convert(packet));
        }

        [Fact]
        public void OtherSubs_PassUnchanged()
        {
            byte[] upkeep = Packet(10, new byte[20]);
            Assert.Same(upkeep, ClaimConverter.Convert(upkeep));
        }
    }
}
