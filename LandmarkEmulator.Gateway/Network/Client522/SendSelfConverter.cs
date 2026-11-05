using LandmarkEmulator.Shared.Game;
using LandmarkEmulator.Shared.Game.Entity.Static;
using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Shared.Network.Message.Model.Shared;
using System.Collections.Generic;
using System.Numerics;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// Rewrites the zone's SendSelfToClient (0.695 layout) for the 0.522 client: the character only, no items or hotbars.
    /// The two layouts share their start, so the 0.695 fields are read in order up to the profiles and the rest is left.
    /// </summary>
    public static class SendSelfConverter
    {
        private sealed class Profile
        {
            public uint Id, NameId, Type, Size, Feet, Armor;
            public float Stand, Crouch, Sight, Capsule, Mass;
        }

        /// <param name="packet">Opcode (2 bytes), u32 size, body.</param>
        public static byte[] Convert(byte[] packet)
        {
            var reader = new GamePacketReader(packet[6..]);
            var message = new SendSelfToClient694
            {
                Guid = reader.ReadULong(),
                AccountName = reader.ReadString(),
                CharacterId = reader.ReadULong(),
            };
            reader.ReadUInt2BitLength();    // transient id
            reader.ReadULong();             // server time
            message.ModelId = reader.ReadUInt();

            var model = new CharacterModelInfo();
            model.Read(reader);
            message.Model = model;
            message.GenderMaybe = (Gender)model.Gender;

            Vector4 position = ReadVector(reader);
            Vector4 rotation = ReadVector(reader);
            if (position.X != 0f || position.Y != 0f || position.Z != 0f)
            {
                message.Unknown3 = position;
                message.Unknown4 = rotation;
            }

            reader.ReadUInt();              // gender
            reader.ReadUInt();
            reader.ReadUInt();              // honor
            message.Identity.FirstName = reader.ReadString();
            reader.ReadString();            // last name

            reader.ReadUInt();              // coin
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 8);
            reader.ReadULong();             // creation date
            Skip(reader, 8);                // unused, played minutes
            Skip(reader, 3);                // underage, unused, dead
            reader.ReadUInt();              // membership
            Skip(reader, 2);                // Player Studio, nag screen
            Skip(reader, 16);               // country, language, preferred languages, unused
            reader.ReadUInt();              // login count
            reader.ReadULong();             // unused timestamp
            message.Unknown22 = reader.ReadUInt();  // speech set: the race
            reader.ReadBool();
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 12);
            Skip(reader, 4);                // four flags

            var profiles = new List<Profile>();
            for (uint i = reader.ReadUInt(); i > 0; i--)
                profiles.Add(ReadProfile(reader));
            uint current = reader.ReadUInt();
            Profile profile = profiles.Find(p => p.Id == current) ?? profiles[0];

            // Which field goes where is what the client was tested with on our own server (SendSelfToClient694).
            message.Profiles.Add(new SendSelfToClient694.Profile
            {
                Unknown0 = profile.Id,
                NameId = new LandmarkText(profile.NameId),
                Unknown3 = profile.Type,
                Unknown15 = 1,
                Unknown19 = profile.Stand,
                Unknown20 = profile.Sight,
                Unknown21 = profile.Capsule,
                Unknown22 = 1,
                Unknown23 = profile.Crouch,
                Unknown24 = profile.Mass,
                Unknown29 = profile.Size,
                Unknown30 = profile.Armor,
                Unknown31 = profile.Feet,
            });
            message.CurrentProfile = profile.Id;
            message.Unknown24.Add((profile.Id, profile.Id, 0));
            message.Unknown30.Add((profile.Type, profile.Id));
            message.Loadouts[0].Unknown1.Unknown1 = profile.Id;
            ReadInventory(reader, message.Inventory);

            var bytes = new List<byte> { packet[0], packet[1] };
            var body = new List<byte>();
            message.Write(new GamePacketWriter(body));
            var writer = new GamePacketWriter(bytes);
            writer.Write((uint)body.Count);
            bytes.AddRange(body);
            return bytes.ToArray();
        }

        /// <summary>
        /// The bag, which comes after the profiles. Collections and buffs in front of it have long records: with any of
        /// those the character goes in with an empty bag.
        /// </summary>
        private static void ReadInventory(GamePacketReader reader, InventoryData694 inventory)
        {
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 8);            // last played profile by type
            if (reader.ReadUInt() != 0)     // collections
                return;
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 12);           // ability cooldowns
            reader.ReadUInt();              // cast key seed
            if (reader.ReadUInt() != 0)     // buffs
                return;

            Skip(reader, 12 * 5);           // the containers, always the same 12
            for (uint i = reader.ReadUInt(); i > 0; i--)
                inventory.Items.Add(ReadItem(reader));
        }

        /// <summary>
        /// One 0.695 item record as 0.522's. <paramref name="extraByte"/>: the record is followed by the byte the login list has
        /// after each item; an ItemAdd may end without it (then 0.522's extra byte stays 0).
        /// </summary>
        public static InventoryItem694 ReadItem(GamePacketReader reader, bool extraByte = true)
        {
            var item = new InventoryItem694 { DefinitionId = reader.ReadUInt(), Unknown0C = reader.ReadUInt() };
            SkipRandomData(reader);
            SkipRandomData(reader);
            reader.ReadUInt();              // cost multiplier
            item.Guid = reader.ReadULong();
            item.Count = reader.ReadUInt();
            item.Unknown7C = reader.ReadUInt();
            item.Unknown80 = reader.ReadUInt();
            item.Unknown84 = reader.ReadBool();
            item.ContainerId = reader.ReadUInt();
            item.Unknown98 = reader.ReadULong();
            item.SlotId = reader.ReadUInt();
            reader.ReadUInt();              // owner type: 0.522 needs its own value for ActivateItem (InventoryItem694)
            item.UnknownA8 = reader.ReadULong();
            item.UnknownB0 = reader.ReadULong();
            item.UnknownB8 = reader.ReadUInt();
            item.UnknownBC = reader.ReadUInt();
            item.UnknownF0 = reader.ReadByte();
            item.Time = reader.ReadUInt();
            if (reader.ReadByte() != 0)     // rental
                Skip(reader, 16);
            if (reader.ReadByte() != 0)     // design
            {
                reader.ReadULong();
                reader.ReadString();
                reader.ReadString();
            }
            if (reader.ReadByte() != 0)     // meta data
                if (reader.ReadUInt() == 1)
                    Skip(reader, 8);
            item.UnknownE0 = reader.ReadUInt();
            if (extraByte || reader.BytesRemaining > 0)
                item.ExtraByte = reader.ReadByte();
            return item;
        }

        private static void SkipRandomData(GamePacketReader reader)
        {
            Skip(reader, 20);
            for (uint i = reader.ReadUInt(); i > 0; i--)
            {
                Skip(reader, 4);
                new LandmarkText().Read(reader);
                Skip(reader, 8 + 1 + 12);
            }
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 12);
        }

        private static Profile ReadProfile(GamePacketReader reader)
        {
            var profile = new Profile { Id = reader.ReadUInt() };
            var name = new LandmarkText();
            name.Read(reader);
            profile.NameId = name.Id;
            Skip(reader, 4);
            profile.Type = reader.ReadUInt();
            Skip(reader, 16);
            Skip(reader, 1);
            Skip(reader, 4);
            for (uint i = reader.ReadUInt(); i > 0; i--)
                Skip(reader, 12);
            Skip(reader, 1 + 8 + 1 + 1 + 3);
            profile.Stand = reader.ReadSingle();
            profile.Crouch = reader.ReadSingle();
            profile.Sight = reader.ReadSingle();
            reader.ReadUInt();
            profile.Capsule = reader.ReadSingle();
            profile.Mass = reader.ReadSingle();
            Skip(reader, 12);
            profile.Size = reader.ReadUInt();
            profile.Feet = reader.ReadUInt();
            profile.Armor = reader.ReadUInt();
            Skip(reader, 12);
            return profile;
        }

        private static Vector4 ReadVector(GamePacketReader reader)
            => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static void Skip(GamePacketReader reader, uint bytes) => reader.ReadBytes(bytes);
    }
}
