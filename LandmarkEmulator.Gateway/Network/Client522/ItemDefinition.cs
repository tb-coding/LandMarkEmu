using LandmarkEmulator.Shared.Network;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// One record of CommandBase ItemDefinitions (sub opcode 0x4B) in the 709 / 0.695 layout of the item-support branch (CommandItemDefinitions.cs).
    /// </summary>
    /// <remarks>
    /// Read from the 0.695 dump Resources/PacketDumps/44-CommandBase, the values are only a reference for the 0.522 client (dev-log/11 §V6).
    /// <see cref="Write709"/> writes the same bytes as were read, <see cref="ItemDefinitionLayout694"/> writes the 0.522 layout.
    /// Stat values are kept as raw 32 bit values so a record is written back exactly.
    /// </remarks>
    public enum ItemQuality
    {
        Average,
        Superior,
        Exceptional,
        Legendary,
        Recipe
    }

    public class ItemDefinition
    {
        // sub_142C203F0
        public class Ability
        {
            public uint Id { get; set; }
            public uint Unknown1 { get; set; }
            public string Unknown2 { get; set; } = "";
            public string Unknown3 { get; set; } = "";
            public uint Unknown4 { get; set; }
            public byte Unknown5 { get; set; }

            public void Read(GamePacketReader reader)
            {
                Id       = reader.ReadUInt();
                Unknown1 = reader.ReadUInt();
                Unknown2 = reader.ReadString();
                Unknown3 = reader.ReadString();
                Unknown4 = reader.ReadUInt();
                Unknown5 = reader.ReadByte();
            }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(Id);
                writer.Write(Unknown1);
                writer.Write(Unknown2);
                writer.Write(Unknown3);
                writer.Write(Unknown4);
                writer.Write(Unknown5);
            }
        }

        /// <summary>
        /// Stat of the definition: id, <c>StatData</c> (id, type, base, modifier) and a trailing uint, base and modifier as raw bits.
        /// </summary>
        public class Stat
        {
            public uint Id { get; set; }
            public uint DataStatId { get; set; }
            public byte Type { get; set; }
            public uint BaseRaw { get; set; }
            public uint ModifierRaw { get; set; }
            public uint Unknown { get; set; }

            public void Read(GamePacketReader reader)
            {
                Id          = reader.ReadUInt();
                DataStatId  = reader.ReadUInt();
                Type        = reader.ReadByte();
                BaseRaw     = reader.ReadUInt();
                ModifierRaw = reader.ReadUInt();
                Unknown     = reader.ReadUInt();
            }

            public void Write(GamePacketWriter writer)
            {
                writer.Write(Id);
                writer.Write(DataStatId);
                writer.Write(Type);
                writer.Write(BaseRaw);
                writer.Write(ModifierRaw);
                writer.Write(Unknown);
            }
        }

        public uint Id { get; set; }
        public byte Flags1 { get; set; }
        public byte Flags2 { get; set; }
        public byte Flags3 { get; set; }
        public uint NameId { get; set; }
        public uint DescriptionId { get; set; }
        public uint ImageSetId { get; set; }
        public uint Unknown7 { get; set; }
        public uint Unknown8 { get; set; }
        public uint Unknown9 { get; set; }
        public uint Unknown10 { get; set; }
        /// <summary>
        /// 2 = held in the hand (picks, axes, weapons), client +0x34 (dev-log/11 §V6).
        /// </summary>
        public uint EquipSlotId { get; set; }
        public string ModelFile { get; set; } = "";
        public string Unknown13 { get; set; } = "";
        public uint Unknown14 { get; set; }
        public uint Unknown15 { get; set; }
        public uint Unknown16 { get; set; }
        public uint Unknown17 { get; set; }
        public uint Unknown18 { get; set; }
        public uint MinStackSize { get; set; }
        public uint Unknown20 { get; set; }
        public uint MaxStackSize { get; set; }
        public string TintAlias { get; set; } = "";
        /// <summary>
        /// Unknown23~Unknown32.
        /// </summary>
        public uint[] Unknown23To32 { get; set; } = new uint[10];
        public string Unknown33 { get; set; } = "";
        public string Unknown34 { get; set; } = "";
        /// <summary>
        /// Unknown35~Unknown39.
        /// </summary>
        public uint[] Unknown35To39 { get; set; } = new uint[5];
        public ItemQuality Quality { get; set; }
        public uint Unknown41 { get; set; }
        public uint Unknown42 { get; set; }
        public uint Unknown43 { get; set; }
        public uint Unknown44 { get; set; }
        public uint Unknown45 { get; set; }
        public uint Unknown46 { get; set; }
        /// <summary>
        /// Looks like the item class: 2 pick, 34 axe, 115 Pulverizer, 216 Founder pick, 26 weapon (dev-log/11).
        /// </summary>
        public List<uint> Unknown47 { get; set; } = new();
        public uint Unknown48 { get; set; }
        public uint Unknown49 { get; set; }
        public uint Unknown50 { get; set; }
        public uint Unknown51 { get; set; }
        public uint Unknown52 { get; set; }
        public uint Unknown53 { get; set; }
        public List<Ability> Abilities { get; set; } = new();
        public string SecondaryModelFile { get; set; } = "";
        public string Unknown56 { get; set; } = "";
        public string SecondaryTintAlias { get; set; } = "";
        public uint Unknown58 { get; set; }
        public List<uint> Unknown59 { get; set; } = new();
        public uint GrantId { get; set; }
        public uint GrantAmount { get; set; }
        public List<Stat> Stats { get; set; } = new();
        /// <summary>
        /// Bytes after the stats, not read by the item-support branch: 8 zero bytes in every record of the 0.695 dump.
        /// </summary>
        public byte[] Trailing { get; set; } = new byte[TrailingLength];

        public const int TrailingLength = 8;

        /// <summary>
        /// The 8 uints from Unknown14 to MaxStackSize, 0.522 has only 7 of them (client +0xcc~+0xe4).
        /// </summary>
        public uint[] StackFields => new[] { Unknown14, Unknown15, Unknown16, Unknown17, Unknown18, MinStackSize, Unknown20, MaxStackSize };

        /// <summary>
        /// Value of the uint field with the property name, used by <see cref="ItemDefinitionLayout694"/> to pick the fields of the 0.522 layout.
        /// </summary>
        public uint GetUInt(string name)
        {
            return name switch
            {
                nameof(Unknown41) => Unknown41,
                nameof(Unknown42) => Unknown42,
                nameof(Unknown43) => Unknown43,
                nameof(Unknown44) => Unknown44,
                nameof(Unknown45) => Unknown45,
                nameof(Unknown46) => Unknown46,
                nameof(Unknown48) => Unknown48,
                nameof(Unknown49) => Unknown49,
                nameof(Unknown50) => Unknown50,
                nameof(Unknown51) => Unknown51,
                nameof(Unknown52) => Unknown52,
                nameof(Unknown53) => Unknown53,
                nameof(Unknown58) => Unknown58,
                "Zero"            => 0u,
                _ => throw new KeyNotFoundException($"No uint field {name} in ItemDefinition.")
            };
        }

        public void Read(GamePacketReader reader)
        {
            Id            = reader.ReadUInt();
            Flags1        = reader.ReadByte();
            Flags2        = reader.ReadByte();
            Flags3        = reader.ReadByte();
            NameId        = reader.ReadUInt();
            DescriptionId = reader.ReadUInt();
            ImageSetId    = reader.ReadUInt();
            Unknown7      = reader.ReadUInt();
            Unknown8      = reader.ReadUInt();
            Unknown9      = reader.ReadUInt();
            Unknown10     = reader.ReadUInt();
            EquipSlotId   = reader.ReadUInt();
            ModelFile     = reader.ReadString();
            Unknown13     = reader.ReadString();
            Unknown14     = reader.ReadUInt();
            Unknown15     = reader.ReadUInt();
            Unknown16     = reader.ReadUInt();
            Unknown17     = reader.ReadUInt();
            Unknown18     = reader.ReadUInt();
            MinStackSize  = reader.ReadUInt();
            Unknown20     = reader.ReadUInt();
            MaxStackSize  = reader.ReadUInt();
            TintAlias     = reader.ReadString();
            for (int i = 0; i < Unknown23To32.Length; i++)
                Unknown23To32[i] = reader.ReadUInt();
            Unknown33 = reader.ReadString();
            Unknown34 = reader.ReadString();
            for (int i = 0; i < Unknown35To39.Length; i++)
                Unknown35To39[i] = reader.ReadUInt();
            Quality   = (ItemQuality)reader.ReadUInt();
            Unknown41 = reader.ReadUInt();
            Unknown42 = reader.ReadUInt();
            Unknown43 = reader.ReadUInt();
            Unknown44 = reader.ReadUInt();
            Unknown45 = reader.ReadUInt();
            Unknown46 = reader.ReadUInt();

            uint unknown47Count = reader.ReadUInt();
            for (int i = 0; i < unknown47Count; i++)
                Unknown47.Add(reader.ReadUInt());

            Unknown48 = reader.ReadUInt();
            Unknown49 = reader.ReadUInt();
            Unknown50 = reader.ReadUInt();
            Unknown51 = reader.ReadUInt();
            Unknown52 = reader.ReadUInt();
            Unknown53 = reader.ReadUInt();

            uint abilityCount = reader.ReadUInt();
            for (int i = 0; i < abilityCount; i++)
            {
                var ability = new Ability();
                ability.Read(reader);
                Abilities.Add(ability);
            }

            SecondaryModelFile = reader.ReadString();
            Unknown56          = reader.ReadString();
            SecondaryTintAlias = reader.ReadString();
            Unknown58          = reader.ReadUInt();

            uint unknown59Count = reader.ReadUInt();
            for (int i = 0; i < unknown59Count; i++)
                Unknown59.Add(reader.ReadUInt());

            GrantId     = reader.ReadUInt();
            GrantAmount = reader.ReadUInt();

            uint statCount = reader.ReadUInt();
            for (int i = 0; i < statCount; i++)
            {
                var stat = new Stat();
                stat.Read(reader);
                Stats.Add(stat);
            }

            Trailing = reader.ReadBytes(System.Math.Min(reader.BytesRemaining, (uint)TrailingLength));
        }

        /// <summary>
        /// Writes the record in the 709 / 0.695 layout it was read in.
        /// </summary>
        public void Write709(GamePacketWriter writer)
        {
            WriteHead(writer);
            foreach (uint value in StackFields)
                writer.Write(value);
            WriteMiddle(writer);
            writer.Write(Unknown41);
            writer.Write(Unknown42);
            writer.Write(Unknown43);
            writer.Write(Unknown44);
            writer.Write(Unknown45);
            writer.Write(Unknown46);
            WriteUIntList(writer, Unknown47);
            writer.Write(Unknown48);
            writer.Write(Unknown49);
            writer.Write(Unknown50);
            writer.Write(Unknown51);
            writer.Write(Unknown52);
            writer.Write(Unknown53);
            WriteTail(writer);
        }

        /// <summary>
        /// Id ~ Unknown13, the same in 709 and 0.522 (dev-log/11 §V6).
        /// </summary>
        public void WriteHead(GamePacketWriter writer)
        {
            writer.Write(Id);
            writer.Write(Flags1);
            writer.Write(Flags2);
            writer.Write(Flags3);
            writer.Write(NameId);
            writer.Write(DescriptionId);
            writer.Write(ImageSetId);
            writer.Write(Unknown7);
            writer.Write(Unknown8);
            writer.Write(Unknown9);
            writer.Write(Unknown10);
            writer.Write(EquipSlotId);
            writer.Write(ModelFile);
            writer.Write(Unknown13);
        }

        /// <summary>
        /// TintAlias ~ Quality, the same in 709 and 0.522 (dev-log/11 §V6).
        /// </summary>
        public void WriteMiddle(GamePacketWriter writer)
        {
            writer.Write(TintAlias);
            foreach (uint value in Unknown23To32)
                writer.Write(value);
            writer.Write(Unknown33);
            writer.Write(Unknown34);
            foreach (uint value in Unknown35To39)
                writer.Write(value);
            writer.Write((uint)Quality);
        }

        /// <summary>
        /// Abilities ~ Stats and <see cref="Trailing"/>, the end of the 709 record.
        /// </summary>
        public void WriteTail(GamePacketWriter writer)
        {
            writer.Write((uint)Abilities.Count);
            foreach (Ability ability in Abilities)
                ability.Write(writer);
            writer.Write(SecondaryModelFile);
            writer.Write(Unknown56);
            writer.Write(SecondaryTintAlias);
            writer.Write(Unknown58);
            WriteUIntList(writer, Unknown59);
            writer.Write(GrantId);
            writer.Write(GrantAmount);
            writer.Write((uint)Stats.Count);
            foreach (Stat stat in Stats)
                stat.Write(writer);
            writer.WriteBytes(Trailing);
        }

        /// <summary>
        /// End of the 0.522 record, FUN_14324eca0 then FUN_143250c70: +0x1a0, abilities, 3 strings, +0x17c, a uint list,
        /// +0x230, +0x234, +0x238, +0xf8, a list at +0x240 (FUN_14326cc80, its format is not known yet so it stays empty).
        /// </summary>
        public void WriteTail694(GamePacketWriter writer)
        {
            writer.Write(Unknown53);
            writer.Write((uint)Abilities.Count);
            foreach (Ability ability in Abilities)
                ability.Write(writer);
            writer.Write(SecondaryModelFile);
            writer.Write(Unknown56);
            writer.Write(SecondaryTintAlias);
            writer.Write(Unknown58);
            WriteUIntList(writer, Unknown59);
            writer.Write(GrantId);
            writer.Write(GrantAmount);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
        }

        public static void WriteUIntList(GamePacketWriter writer, IReadOnlyCollection<uint> values)
        {
            writer.Write((uint)values.Count);
            foreach (uint value in values)
                writer.Write(value);
        }
    }
}
