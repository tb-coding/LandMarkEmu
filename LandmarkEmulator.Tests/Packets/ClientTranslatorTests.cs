using LandmarkEmulator.Gateway.Network;
using LandmarkEmulator.Gateway.Network.Message;
using LandmarkEmulator.Tests.Support;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>The gateway's conversion of client packets (IClientTranslator).</summary>
    public class ClientTranslatorTests
    {
        private readonly Client522Translator translator = new();

        [Fact]
        public void PassThrough_ChangesNothing()
        {
            byte[] data = Wire.Hex("99 99 01 02");
            Assert.Same(data, PassThroughTranslator.Instance.Inbound(Channel.Zone, data));
            Assert.Same(data, PassThroughTranslator.Instance.Outbound(Channel.Zone, data));
        }

        [Theory]
        [InlineData("63 00 01 02 03 04")]   // KeepAlive
        [InlineData("13 01 05")]            // WarpNetworkBase, above 0xFF
        [InlineData("F9 00 1B")]            // ClaimsBase
        public void RegisteredOpcodes_PassUnchanged(string hex)
        {
            byte[] data = Wire.Hex(hex);
            Assert.Same(data, translator.Inbound(Channel.Zone, data));
            Assert.Same(data, translator.Outbound(Channel.Zone, data));
        }

        [Theory]
        [InlineData("56 00 01")]    // ClaimReportSubmissionBase, 0.695 only
        [InlineData("19 01 00")]    // above the client's table
        [InlineData("63")]          // no opcode
        public void UnknownOpcodes_AreDropped(string hex)
        {
            Assert.Null(translator.Inbound(Channel.Zone, Wire.Hex(hex)));
            Assert.Null(translator.Outbound(Channel.Zone, Wire.Hex(hex)));
        }

        [Fact]
        public void PositionChannel_HasNoOpcode()
        {
            byte[] data = Wire.Hex("56 00 01");
            Assert.Same(data, translator.Inbound(Channel.PositionUpdate, data));
            Assert.Same(data, translator.Outbound(Channel.PositionUpdate, data));
        }

        [Fact]
        public void PlayerUpdate_InboundSubBecomesU32()
            => Assert.Equal(Wire.Hex("26 00 70 00 00 00 AA BB"), translator.Inbound(Channel.Zone, Wire.Hex("26 00 70 AA BB")));

        [Fact]
        public void PlayerUpdate_OutboundSubBecomesU8()
            => Assert.Equal(Wire.Hex("26 00 01 AA BB"), translator.Outbound(Channel.Zone, Wire.Hex("26 00 01 00 00 00 AA BB")));

        [Fact]
        public void PlayerUpdate_RenumberedSub_GoesBothWays()
        {
            Assert.Equal(Wire.Hex("26 00 57 AA"), translator.Outbound(Channel.Zone, Wire.Hex("26 00 58 00 00 00 AA")));
            Assert.Equal(Wire.Hex("26 00 58 00 00 00 AA"), translator.Inbound(Channel.Zone, Wire.Hex("26 00 57 AA")));
        }

        [Fact]
        public void PlayerUpdate_TooShort_IsDropped()
        {
            Assert.Null(translator.Inbound(Channel.Zone, Wire.Hex("26 00")));
            Assert.Null(translator.Outbound(Channel.Zone, Wire.Hex("26 00 01 00")));
        }
    }
}
