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

            var bytes = new List<byte> { packet[0], packet[1] };
            var body = new List<byte>();
            message.Write(new GamePacketWriter(body));
            var writer = new GamePacketWriter(bytes);
            writer.Write((uint)body.Count);
            bytes.AddRange(body);
            return bytes.ToArray();
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
