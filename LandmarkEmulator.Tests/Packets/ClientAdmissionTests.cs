using LandmarkEmulator.AuthServer.Network.Message.Model;
using LandmarkEmulator.Gateway.Network;
using LandmarkEmulator.Gateway.Network.Message;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Tests.Support;
using System;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>Which clients the gateway login admits (ClientBuilds.Admit), and the 0.522 client's login packets.</summary>
    public class ClientAdmissionTests : IDisposable
    {
        private const string Build522 = "0.522.3.116271";
        private const string Protocol694 = "LandmarkClientProtocol_694";

        public void Dispose() => ClientBuilds.Allow(Build522);

        [Fact]
        public void Protocol694_IsRefusedUnlessItsBuildIsAllowed()
        {
            ClientBuilds.Allow("");
            Assert.NotNull(ClientBuilds.Admit(Protocol694, Build522).Refusal);

            ClientBuilds.Allow("0.522.3.999999");
            Assert.NotNull(ClientBuilds.Admit(Protocol694, Build522).Refusal);
        }

        [Fact]
        public void AllowedBuild_IsServedAs709()
        {
            ClientBuilds.Allow($"0.1.2.3, {Build522}");
            (ClientProtocol served, string refusal) = ClientBuilds.Admit(Protocol694, Build522);
            Assert.Null(refusal);
            Assert.Equal(ClientProtocol.LandmarkClientProtocol_709, served);
        }

        [Theory]
        [InlineData("0.695.1.132647", null)]
        [InlineData("0.589.1.128895", "0.589")]
        public void Protocol709_AsBefore(string build, string refused)
        {
            string refusal = ClientBuilds.Admit("LandmarkClientProtocol_709", build).Refusal;
            if (refused == null)
                Assert.Null(refusal);
            else
                Assert.Contains(refused, refusal);
        }

        [Fact]
        public void TranslatorFor_OnlyAnAllowed522BuildIsConverted()
        {
            ClientBuilds.Allow("");
            Assert.IsType<PassThroughTranslator>(ClientBuilds.TranslatorFor("0.522.3.116271"));
            ClientBuilds.Allow("0.522.3.116271");
            Assert.IsType<Client522Translator>(ClientBuilds.TranslatorFor("0.522.3.116271"));
            Assert.IsType<PassThroughTranslator>(ClientBuilds.TranslatorFor("0.695.1.132647"));
            ClientBuilds.Allow("");
        }

        [Fact]
        public void UnknownProtocol_IsRefused()
            => Assert.NotNull(ClientBuilds.Admit("LandmarkClientProtocol_1", Build522).Refusal);

        /// <summary>
        /// The 0.522 LoginRequest: session, fingerprint, the locale as a string and 12 zero bytes after it. Those are the
        /// three empty strings of the 0.695 layout, so the same model reads it to the end.
        /// </summary>
        [Fact]
        public void LoginRequest_Reads522Layout()
        {
            byte[] data = Wire.Write(w =>
            {
                w.Write("0123456789abcdef0123456789abcdef");
                w.Write("<Fingerprint/>");
                w.Write("en_us");
                w.Write(0u);
                w.Write(0u);
                w.Write(0u);
            });

            var request = new LoginRequest();
            var reader = new GamePacketReader(data);
            request.Read(reader);

            Assert.Equal(0u, reader.BytesRemaining);
            Assert.Equal("0123456789abcdef0123456789abcdef", request.SessionId);
            Assert.Equal("en_us", request.Locale);
            Assert.Equal("", request.ThirdPartyAuthTicket);
            Assert.Equal("", request.ThirdPartyId);
        }
    }
}
