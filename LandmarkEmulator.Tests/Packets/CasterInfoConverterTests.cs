using LandmarkEmulator.Gateway.Network;
using LandmarkEmulator.Gateway.Network.Message;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.WorldServer.Game;
using LandmarkEmulator.WorldServer.Network.Message.Model;
using System;
using System.Collections.Generic;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>The gateway's CasterInfoReply conversion to 0.522's layout (73rd test: tools unusable without it).</summary>
    public class CasterInfoConverterTests
    {
        private readonly Client522Translator translator = new();

        /// <summary>0.522's reply body: 155 bytes with empty strings and lists, then the 4 byte opcode and sub.</summary>
        private const int Length522 = 4 + 159;

        private static byte[] Packet(AbilityCasterInfoReply reply)
        {
            var bytes = new List<byte> { 0x27, 0, 3, 0 };
            reply.Write(new GamePacketWriter(bytes));
            return bytes.ToArray();
        }

        private static AbilityCasterInfoReply Pick() => new()
        {
            AbilityId = 1018,
            CostAmount = 5,
            NameId = 558821,
            UpdatesToolMode = true,
            MaxRange = 6f,
            MinRange = 0.5f,
            U68 = 1,
            ArcDegrees = 90f,
            AnimationId = 245,
            CastTime = 0f,
            RecoveryTime = 1.5f,
            DescriptionId = 558822,
        };

        [Fact]
        public void Reply_GetsThe0522Layout()
        {
            byte[] converted = translator.Outbound(Channel.Zone, Packet(Pick()));

            Assert.Equal(Length522, converted.Length);
            Assert.Equal(new byte[] { 0x27, 0, 3, 0 }, converted[..4]);
            Assert.Equal(1018u, BitConverter.ToUInt32(converted, 4));
            Assert.Equal(5u, BitConverter.ToUInt32(converted, 12));          // the cost, 0.522 +0x20
            Assert.Equal(558821u, BitConverter.ToUInt32(converted, 4 + 40)); // name id
            Assert.Equal(1, converted[4 + 44]);                              // updates tool mode
            Assert.Equal(6f, BitConverter.ToSingle(converted, 4 + 45));      // max range
            Assert.Equal(90f, BitConverter.ToSingle(converted, 4 + 65));     // arc
            Assert.Equal(245u, BitConverter.ToUInt32(converted, 4 + 92));    // animation
            Assert.Equal(558822u, BitConverter.ToUInt32(converted, 4 + 112)); // description, after the cast time and one bool
            Assert.Equal(1u, BitConverter.ToUInt32(converted, 4 + 155));     // +0xe0, last
        }

        [Fact]
        public void EffectsKeywordsAndTargeters_AreLeftOut()
        {
            AbilityCasterInfoReply reply = Pick();
            reply.ExecutedEffects.Add(new MovementEffects.Effect(0x7faad620, 1, 2, 3));
            reply.Keywords.Add(7);
            reply.ExecutedTargeters.Add(new ClientExecutedTargeter { Id = 1, Type = ClientExecutedTargeter.Sweep });

            byte[] converted = translator.Outbound(Channel.Zone, Packet(reply));

            Assert.Equal(Length522, converted.Length);
            Assert.Equal(558822u, BitConverter.ToUInt32(converted, 4 + 112));
            Assert.Equal(1u, BitConverter.ToUInt32(converted, 4 + 155));
        }

        [Fact]
        public void ShortReply_IsDropped()
            => Assert.Null(translator.Outbound(Channel.Zone, new byte[] { 0x27, 0, 3, 0, 1, 2, 3 }));

        [Fact]
        public void OtherAbilitySubs_AreHeldBack()
            => Assert.Null(translator.Outbound(Channel.Zone, new byte[] { 0x27, 0, 0x12, 0, 0, 0, 0, 0, 0 }));

        [Fact]
        public void CastAttempt_GetsAnEmptyCastKeyAndTail()
        {
            // 0.522: 27 00 01 00, ability 1018, aim point (1, 2, 3), then the rest of its 126 bytes.
            var packet = new byte[126];
            packet[0] = 0x27;
            packet[2] = 1;
            BitConverter.TryWriteBytes(packet.AsSpan(4), 1018u);
            BitConverter.TryWriteBytes(packet.AsSpan(8), 1f);
            BitConverter.TryWriteBytes(packet.AsSpan(12), 2f);
            BitConverter.TryWriteBytes(packet.AsSpan(16), 3f);
            packet[100] = 0xEE;

            byte[] converted = translator.Inbound(Channel.Zone, packet);

            var attempt = new AbilityCastAttempt();
            attempt.Read(new GamePacketReader(converted[4..]));
            Assert.Equal(0ul, attempt.CastGuid);
            Assert.Equal(1018u, attempt.AbilityId);
            Assert.Equal((1f, 2f, 3f), (attempt.TargetX, attempt.TargetY, attempt.TargetZ));
            Assert.Empty(attempt.Tail);
            Assert.Null(attempt.RingPoint);
        }

        [Fact]
        public void CastSuccess_Gets0522Layout()
        {
            var bytes = new List<byte> { 0x27, 0, 7, 0 };
            new AbilityCastSuccess { CasterGuid = 0x1122, CastGuid = 0x33, CastHash = 0x44, AbilityId = 1018, AnimationId = 245 }
                .Write(new GamePacketWriter(bytes));

            byte[] converted = translator.Outbound(Channel.Zone, bytes.ToArray());

            Assert.Equal(43, converted.Length);
            Assert.Equal(bytes.GetRange(0, 11).ToArray(), converted[..11]);
            Assert.Equal(0x1122ul, BitConverter.ToUInt64(converted, 11));
            Assert.Equal(1018u, BitConverter.ToUInt32(converted, 27));
            Assert.Equal(245u, BitConverter.ToUInt32(converted, 39));
        }

        [Fact]
        public void ItemAdd_GetsThe0522ItemRecord()
        {
            var item = new LandmarkEmulator.WorldServer.Network.Message.Model.Shared.InventoryItem();
            item.Definition.DefinitionId = 212;
            item.Guid = 0x10000005;
            item.Count = 3;
            item.ContainerId = 0;
            item.SlotId = 9;
            var bytes = new List<byte> { 0x29, 0, 2, 0 };
            new ClientUpdateItemAdd { Item = item }.Write(new GamePacketWriter(bytes));

            byte[] converted = translator.Outbound(Channel.Zone, bytes.ToArray());

            Assert.NotNull(converted);
            Assert.Equal(converted.Length - 8, BitConverter.ToInt32(converted, 4));
            Assert.Equal(0, converted[8]);                                    // no definition
            Assert.Equal(212u, BitConverter.ToUInt32(converted, 9));          // definition id
            int guid = 9 + 4 + 4 + 28 + 4;                                    // definition, u32, one empty block, +0x68
            Assert.Equal(0x10000005ul, BitConverter.ToUInt64(converted, guid));
            Assert.Equal(3u, BitConverter.ToUInt32(converted, guid + 8));
        }

        [Fact]
        public void CastFail_EndsTheCastAsASuccessWithoutAnimation()
        {
            var bytes = new List<byte> { 0x27, 0, 6, 0 };
            new AbilityCastFail { CasterGuid = 0x1122, AbilityId = 1018, AnimationId = 99, Kind = 2 }.Write(new GamePacketWriter(bytes));

            byte[] converted = translator.Outbound(Channel.Zone, bytes.ToArray());

            Assert.Equal(43, converted.Length);
            Assert.Equal(7, converted[2]);
            Assert.Equal(7, converted[4]);
            Assert.Equal(1018u, BitConverter.ToUInt32(converted, 27));
            Assert.Equal(0u, BitConverter.ToUInt32(converted, 39));
        }
    }
}
