using LandmarkEmulator.Gateway.Network;
using LandmarkEmulator.Gateway.Network.Client522;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Tests.Support;
using LandmarkEmulator.WorldServer.Network;
using LandmarkEmulator.WorldServer.Network.Message.Model;
using System.Linq;
using Xunit;

namespace LandmarkEmulator.Tests.Packets
{
    /// <summary>The login packets the gateway rewrites for the 0.522 client (Client522Translator).</summary>
    public class Login522Tests
    {
        [Fact]
        public void ZoneDetails_DropsTheFieldsAtTheEnd()
        {
            byte[] full = WorldSession.ZoneDetails;
            var read = new SendZoneDetails();
            read.Read(new GamePacketReader(full));

            byte[] packet = new byte[] { 0x2E, 0x00 }.Concat(full).ToArray();
            byte[] trimmed = Client522Translator.TrimZoneDetails(packet);

            int distanceField = read.Continent.DistanceField?.Length ?? 0;
            Assert.Equal(packet.Length - (4 + 4 + distanceField + 1), trimmed.Length);
            Assert.Equal(packet[..trimmed.Length], trimmed);
        }

        [Fact]
        public void ItemDefinitions_KeepEveryRecord()
        {
            byte[] dump = System.IO.File.ReadAllBytes(Repo.PathOf("LandmarkEmulator.WorldServer", "Resources", "PacketDumps", "44-CommandBase"));

            byte[] packet = ItemDefinitionConverter.Convert(new byte[] { 0x1E, 0x00 }.Concat(dump[2..]).ToArray());

            Assert.Equal(System.BitConverter.ToUInt32(dump, 8), System.BitConverter.ToUInt32(packet, 8));
            Assert.Equal((uint)packet.Length - 8, System.BitConverter.ToUInt32(packet, 4));
        }

        [Fact]
        public void SendSelf_KeepsTheCharacter()
        {
            var self = new SendSelfToClient
            {
                Guid = 7,
                AccountName = "alex",
                CharacterId = 7,
                ModelId = 21,
                CurrentProfile = 46,
                SpeechSetId = 1,
                Position = new System.Numerics.Vector4(1f, 2f, 3f, 1f),
            };
            self.Identity.FirstName = "Louisa";
            self.Profiles.Add(new SendSelfToClient.Profile { ProfileId = 46, ProfileType = 4, Mass = 15f, StandCameraHeight = 2.8f, CharSize = 3 });
            self.Inventory.Items.Add(new LandmarkEmulator.WorldServer.Network.Message.Model.Shared.InventoryItem
            {
                Definition = new() { DefinitionId = 3253 },
                Guid = 99,
                Count = 2,
                SlotId = 4,
            });
            byte[] body = Wire.Write(self);
            var header = new byte[] { 0x0F, 0x00 }.Concat(System.BitConverter.GetBytes(body.Length)).ToArray();

            byte[] packet = SendSelfConverter.Convert(header.Concat(body).ToArray());

            var expected = new SendSelfToClient694
            {
                Guid = 7,
                AccountName = "alex",
                CharacterId = 7,
                ModelId = 21,
                Unknown22 = 1,
                Model = self.Model,
                GenderMaybe = (LandmarkEmulator.Shared.Game.Entity.Static.Gender)self.Model.Gender,
                CurrentProfile = 46,
                Unknown3 = self.Position,
                Unknown4 = self.Rotation,
            };
            expected.Identity.FirstName = "Louisa";
            expected.Profiles.Add(new SendSelfToClient694.Profile
            {
                Unknown0 = 46,
                NameId = self.Profiles[0].NameId,
                Unknown3 = 4,
                Unknown15 = 1,
                Unknown19 = 2.8f,
                Unknown22 = 1,
                Unknown24 = 15f,
                Unknown29 = 3,
                Unknown30 = 1,
                Unknown31 = 1,
            });
            expected.Unknown24.Add((46, 46, 0));
            expected.Unknown30.Add((4, 46));
            expected.Loadouts[0].Unknown1.Unknown1 = 46;
            expected.Inventory.Items.Add(new InventoryItem694 { DefinitionId = 3253, Guid = 99, Count = 2, SlotId = 4, Time = 0, UnknownB8 = 0 });
            byte[] want = Wire.Write(expected);

            Assert.Equal(want, packet[6..]);
            Assert.Equal((uint)want.Length, System.BitConverter.ToUInt32(packet, 2));
        }
    }
}
