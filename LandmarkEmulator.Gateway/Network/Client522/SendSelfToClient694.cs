using LandmarkEmulator.Gateway.Network.Message;
using LandmarkEmulator.Shared.Game;
using LandmarkEmulator.Shared.Game.Entity.Static;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Shared.Network.Message;
using LandmarkEmulator.Shared.Network.Message.Model.Shared;
using System.Collections.Generic;
using System.Numerics;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// SendSelfToClient layout for <see cref="ClientProtocol.LandmarkClientProtocol_694"/> (0.522.3.116271 client).
    /// </summary>
    /// <remarks>
    /// Field order follows the client's player data reader (FUN_143250ff0), see docs/22.
    /// Property names reuse the 0.695 names from <see cref="SendSelf522Data"/> where the fields line up.
    /// Lists whose element layout has not been worked out yet are always sent empty.
    /// The client rejects the packet when any bytes are left unread, so nothing may be appended after the last field.
    /// </remarks>
    public class SendSelfToClient694 : IWritable
    {
        private const uint HotbarCount = 9, HotbarSlots = 12;

        // FUN_143251fd0, same as SendSelf522Data.Profile but with 14 trailing uints instead of 15.
        public class Profile : IWritable
        {
            public uint Unknown0 { get; set; }
            public LandmarkText NameId { get; set; } = new();
            public uint Unknown2 { get; set; }
            public uint Unknown3 { get; set; }
            public uint Unknown4 { get; set; }
            public uint Unknown5 { get; set; }
            public uint Unknown6 { get; set; }
            public uint Unknown7 { get; set; }
            public bool Unknown8 { get; set; }
            public uint Unknown9 { get; set; }
            public List<(uint, uint, uint)> Unknown10 { get; set; } = new();
            public bool Unknown11 { get; set; }
            public uint Unknown12 { get; set; }
            public uint Unknown13 { get; set; }
            public bool Unknown14 { get; set; }
            public byte Unknown15 { get; set; }
            public bool Unknown16 { get; set; }
            public bool Unknown17 { get; set; }
            public bool Unknown18 { get; set; }

            // It is not known which of the 15 trailing 0.695 fields is missing, assumed to be the last one.
            public float Unknown19 { get; set; }
            public float Unknown20 { get; set; }
            public float Unknown21 { get; set; }
            public uint Unknown22 { get; set; }
            public float Unknown23 { get; set; }
            public float Unknown24 { get; set; }
            public uint Unknown25 { get; set; }
            public uint Unknown26 { get; set; }
            public uint Unknown27 { get; set; }
            public uint Unknown28 { get; set; }
            public uint Unknown29 { get; set; }
            public uint Unknown30 { get; set; }
            public uint Unknown31 { get; set; }
            public uint Unknown32 { get; set; }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(Unknown0);
                NameId.Write(writer);
                writer.Write(Unknown2);
                writer.Write(Unknown3);
                writer.Write(Unknown4);
                writer.Write(Unknown5);
                writer.Write(Unknown6);
                writer.Write(Unknown7);
                writer.Write(Unknown8);
                writer.Write(Unknown9);

                writer.Write((uint)Unknown10.Count);
                foreach (var item in Unknown10)
                {
                    writer.Write(item.Item1);
                    writer.Write(item.Item2);
                    writer.Write(item.Item3);
                }

                writer.Write(Unknown11);
                writer.Write(Unknown12);
                writer.Write(Unknown13);
                writer.Write(Unknown14);
                writer.Write(Unknown15);
                writer.Write(Unknown16);
                writer.Write(Unknown17);
                writer.Write(Unknown18);

                writer.Write(Unknown19);
                writer.Write(Unknown20);
                writer.Write(Unknown21);
                writer.Write(Unknown22);
                writer.Write(Unknown23);
                writer.Write(Unknown24);
                writer.Write(Unknown25);
                writer.Write(Unknown26);
                writer.Write(Unknown27);
                writer.Write(Unknown28);
                writer.Write(Unknown29);
                writer.Write(Unknown30);
                writer.Write(Unknown31);
                writer.Write(Unknown32);
            }
        }

        public ulong Guid { get; set; } = 1000000;
        public string AccountName { get; set; } = "TestAccount";
        public ulong CharacterId { get; set; }
        public uint Unknown1 { get; set; }
        public ulong LastLoginTimeRaw { get; set; }
        public uint ModelId { get; set; }
        public CharacterModelInfo Model { get; set; } = new();
        public Vector4 Unknown3 { get; set; }
        public Vector4 Unknown4 { get; set; }
        public SendSelf522Data.CharacterIdentity Identity { get; set; } = new();
        public uint Unknown5 { get; set; }
        public List<(uint, uint)> Unknown6 { get; set; } = new();
        public ulong Unknown7 { get; set; }
        public uint Unknown8 { get; set; }
        public uint Unknown9 { get; set; }
        public bool Unknown10 { get; set; }
        public bool Unknown11 { get; set; }
        public bool Unknown12 { get; set; }
        public uint Unknown13 { get; set; }
        public bool Unknown14 { get; set; }
        public bool Unknown15 { get; set; }
        public uint Unknown16 { get; set; }
        public uint Unknown17 { get; set; }
        public uint Unknown18 { get; set; }
        public uint Unknown19 { get; set; }
        public uint Unknown20 { get; set; }
        public ulong Unknown21 { get; set; }
        public uint Unknown22 { get; set; }
        public bool Unknown23 { get; set; }
        public List<(uint, uint, uint)> Unknown24 { get; set; } = new();
        public bool Unknown25 { get; set; }
        public bool Unknown26 { get; set; }
        public List<Profile> Profiles { get; set; } = new();
        public uint CurrentProfile { get; set; }
        public List<(uint, uint)> Unknown30 { get; set; } = new();
        /// <summary>
        /// Same layout as <see cref="SendSelf522Data.InventoryData"/>, items in the 0.522 layout (cloud batch 9 L3).
        /// </summary>
        public InventoryData694 Inventory { get; set; } = new();
        public Gender GenderMaybe { get; set; }
        public SendSelf522Data.QuestData Quests { get; set; } = new();
        public List<uint> Unknown40 { get; set; } = new();
        /// <summary>
        /// Hotbars (docs/22 #48), empty = 0 like before cloud batch 9.
        /// </summary>
        public bool SendFirstTimeEvents { get; set; }
        public List<uint> Unknown46 { get; set; } = new();
        public List<uint> Unknown47 { get; set; } = new();
        public SendSelf522Data.TitleData Titles { get; set; } = new();
        public List<(uint, uint)> Unknown50 { get; set; } = new();
        public List<uint> Unknown51 { get; set; } = new();
        public uint Unknown52 { get; set; }
        public List<uint> Unknown53 { get; set; } = new();
        public List<uint> Unknown54 { get; set; } = new();
        public List<bool> Unknown55 { get; set; } = new();
        public SendSelf522Data.UnknownStruct56 Unknown56 { get; set; } = new();
        public List<(uint, uint)> Unknown57 { get; set; } = new();
        public SendSelf522Data.UnknownStruct58 Unknown58 { get; set; } = new();
        public SendSelf522Data.UnknownStruct63 Unknown63 { get; set; } = new();
        public SendSelf522Data.UnknownStruct64 Unknown64 { get; set; } = new();
        public SendSelf522Data.UnknownStruct65 Unknown65 { get; set; } = new();
        public List<SendSelf522Data.Loadout> Loadouts { get; set; } = new()
        {
            new SendSelf522Data.Loadout()
        };
        public SendSelf522Data.UnknownStruct68 Unknown68 { get; set; } = new();
        public List<(uint, uint)> Unknown69 { get; set; } = new();
        public ulong Unknown70 { get; set; }
        public uint Unknown71 { get; set; }
        public ulong Unknown72 { get; set; }
        public ulong Unknown73 { get; set; }
        public uint Unknown74 { get; set; }
        public uint Unknown75 { get; set; }
        public bool IsAdminMaybe { get; set; }
        public SendSelf522Data.UnknownStruct76 Unknown76 { get; set; } = new();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(Guid);
            writer.Write(AccountName);
            writer.Write(CharacterId);
            writer.WriteUIntWith2BitLength((int)Unknown1);
            writer.Write(LastLoginTimeRaw);

            writer.Write(ModelId);
            Model.Write(writer);

            writer.Write(Unknown3);
            writer.Write(Unknown4);

            Identity.Write(writer);

            writer.Write(Unknown5);

            writer.Write((uint)Unknown6.Count);
            foreach (var item in Unknown6)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
            }

            writer.Write(Unknown7);
            writer.Write(Unknown8);
            writer.Write(Unknown9);
            writer.Write(Unknown10);
            writer.Write(Unknown11);
            writer.Write(Unknown12);
            writer.Write(Unknown13);
            writer.Write(Unknown14);
            writer.Write(Unknown15);
            writer.Write(Unknown16);
            writer.Write(Unknown17);
            writer.Write(Unknown18);
            writer.Write(Unknown19);
            writer.Write(Unknown20);
            writer.Write(Unknown21);
            writer.Write(Unknown22);
            writer.Write(Unknown23);

            writer.Write((uint)Unknown24.Count);
            foreach (var item in Unknown24)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
                writer.Write(item.Item3);
            }

            // 0.695 has 4 bools here.
            writer.Write(Unknown25);
            writer.Write(Unknown26);

            writer.Write((uint)Profiles.Count);
            foreach (Profile profile in Profiles)
                profile.Write(writer);
            writer.Write(CurrentProfile);

            writer.Write((uint)Unknown30.Count);
            foreach (var item in Unknown30)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
            }

            writer.Write(0u); // Collections
            writer.Write(0u); // Unknown31
            // 0.695 has an extra uint (Unknown32) here.
            writer.Write(0u); // Buffs

            Inventory.Write(writer);

            writer.Write(0u); // Unknown35

            writer.Write((uint)GenderMaybe);

            Quests.Write(writer);

            writer.Write(0u); // Achievements
            writer.Write(0u); // Acquaintances

            // Recipes, 0.522 only has 2 of the 4 lists.
            writer.Write(0u);
            writer.Write(0u);

            writer.Write((uint)Unknown40.Count);
            foreach (var item in Unknown40)
                writer.Write(item);

            // 0.695 has Unknown41, Unknown42 and Hotbars here, 0.522 only has one list: the hotbars (docs/22 #48).
            // Empty bars: the client needs them to accept a slot (ActionBarManager.log asks for bar id 8). The slots saved
            // on the server come later in the 0.695 packet and aren't read, so a filled bar is empty again after a relog.
            writer.Write(HotbarCount);
            for (uint bar = 0; bar < HotbarCount; bar++)
            {
                writer.Write(bar);
                writer.Write(HotbarSlots);
                for (uint slot = 0; slot < HotbarSlots; slot++)
                {
                    writer.Write(slot);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0ul);
                }
            }

            writer.Write(0u); // Mounts

            writer.Write(SendFirstTimeEvents);

            writer.Write((uint)Unknown46.Count);
            foreach (var item in Unknown46)
                writer.Write(item);

            writer.Write((uint)Unknown47.Count);
            foreach (var item in Unknown47)
                writer.Write(item);

            writer.Write(0u); // Stats

            Titles.Write(writer);

            writer.Write((uint)Unknown50.Count);
            foreach (var item in Unknown50)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
            }

            writer.Write((uint)Unknown51.Count);
            foreach (var item in Unknown51)
                writer.Write(item);

            writer.Write(Unknown52);

            writer.Write((uint)Unknown53.Count);
            foreach (var item in Unknown53)
                writer.Write(item);

            writer.Write((uint)Unknown54.Count);
            foreach (var item in Unknown54)
                writer.Write(item);

            writer.Write((uint)Unknown55.Count);
            foreach (var item in Unknown55)
                writer.Write(item);

            Unknown56.Write(writer);

            writer.Write((uint)Unknown57.Count);
            foreach (var item in Unknown57)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
            }

            Unknown58.Write(writer);

            writer.Write(0u); // Unknown59
            writer.Write(0u); // Unknown60
            writer.Write(0u); // Unknown61
            writer.Write(0u); // Unknown62

            Unknown63.Write(writer);
            Unknown64.Write(writer);
            Unknown65.Write(writer);

            writer.Write((uint)Loadouts.Count);
            foreach (SendSelf522Data.Loadout loadout in Loadouts)
                loadout.Write(writer);

            // Unknown67, 5 lists.
            for (int i = 0; i < 5; i++)
                writer.Write(0u);

            writer.Write(0u); // Not in 0.695.

            Unknown68.Write(writer);

            writer.Write((uint)Unknown69.Count);
            foreach (var item in Unknown69)
            {
                writer.Write(item.Item1);
                writer.Write(item.Item2);
            }

            writer.Write(Unknown70);
            writer.Write(Unknown71);
            writer.Write(Unknown72);
            writer.Write(Unknown73);
            writer.Write(Unknown74);
            writer.Write(Unknown75);
            writer.Write(IsAdminMaybe);

            Unknown76.Write(writer);
        }
    }
}
