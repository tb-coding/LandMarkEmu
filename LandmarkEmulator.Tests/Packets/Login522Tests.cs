using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Tests.Support;
using LandmarkEmulator.WorldServer.Network;
using LandmarkEmulator.WorldServer.Network.Message.Model;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>The packets of the 0.522 client's login (WorldSession.Login522).</summary>
    public class Login522Tests
    {
        [Fact]
        public void ZoneDetails_DropsTheFieldsAtTheEnd()
        {
            byte[] full = WorldSession.ZoneDetails;
            var read = new SendZoneDetails();
            read.Read(new GamePacketReader(full));

            byte[] trimmed = WorldSession.ZoneDetails522(full);

            int distanceField = read.Continent.DistanceField?.Length ?? 0;
            Assert.Equal(full.Length - (4 + 4 + distanceField + 1), trimmed.Length);
            Assert.Equal(full[..trimmed.Length], trimmed);
        }

        [Fact]
        public void SendSelf_WritesWithoutItems()
        {
            var message = new SendSelfToClient694();
            message.Identity.FirstName = "Louisa";
            byte[] bytes = Wire.Write(message);
            Assert.True(bytes.Length > 500, $"{bytes.Length} bytes");
        }
    }
}
