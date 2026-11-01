using LandmarkEmulator.Shared.Network;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// 0.522 layout of an ItemDefinitions record, reordered from the 709 fields by the client offsets of dev-log/11 §V6 (FUN_14324eca0).
    /// </summary>
    /// <remarks>
    /// Confirmed order: Id ~ Unknown13, the 8 uints Unknown14 ~ MaxStackSize (+0xc8 ~ +0xe4), TintAlias ~ Quality, 4 uints (Unknown41 ~ Unknown44, +0x158 ~ +0x164),
    /// a list (+0x188), 5 uints (+0x168 ~ +0x178), then <see cref="ItemDefinition.WriteTail694"/>. 0.522 has no place for Unknown45 and Unknown46.
    /// <b>Not confirmed</b> (desktop test, can be changed in WorldServer.json Protocol694 without a rebuild):
    /// <list type="bullet">
    /// <item><see cref="DroppedStackField"/>: a stack field left out for experiments, <see cref="NoDroppedStackField"/> by default, the client reads all 8.</item>
    /// <item><see cref="ListField"/>: the list at +0x188 is assumed to be Unknown47 (item class).</item>
    /// <item><see cref="FieldsAfterList"/>: the 709 fields sent as the 5 uints. +0x170 (the third) has to be above 0 for ToolPacketActivate,
    /// with the default it is Unknown48, which is 0 for every tool of the dump.</item>
    /// <item><see cref="WriteTail"/>: writes <see cref="ItemDefinition.WriteTail694"/>, the order is the client's, only +0x1a0 = Unknown53 is assumed.</item>
    /// </list>
    /// </remarks>
    public class ItemDefinitionLayout694
    {
        /// <summary>
        /// Value of <see cref="DroppedStackField"/> that writes all 8 fields.
        /// </summary>
        public const int NoDroppedStackField = -1;

        /// <summary>
        /// Index in <see cref="ItemDefinition.StackFields"/> of a field to leave out (0 = Unknown14 … 7 = MaxStackSize).
        /// </summary>
        public const int DefaultDroppedStackField = NoDroppedStackField;

        /// <summary>
        /// 709 fields written as the 5 uints at client +0x168 ~ +0x178 after the list. 未確認.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultFieldsAfterList = new[]
        {
            nameof(ItemDefinition.Unknown48),
            nameof(ItemDefinition.Unknown49),
            nameof(ItemDefinition.Unknown50),
            nameof(ItemDefinition.Unknown51),
            nameof(ItemDefinition.Unknown52)
        };

        /// <summary>
        /// Number of uints after the list (client +0x168 ~ +0x178).
        /// </summary>
        public const int FieldsAfterListCount = 5;

        /// <summary>
        /// The list at client +0x188. 未確認.
        /// </summary>
        public const string ListField = nameof(ItemDefinition.Unknown47);

        /// <summary>
        /// Writes <see cref="ItemDefinition.WriteTail694"/> after the 5 uints, without it the item has no abilities and cannot be used as a tool.
        /// </summary>
        public const bool DefaultWriteTail = true;

        public static readonly ItemDefinitionLayout694 Default = new();

        public int DroppedStackField { get; }
        public IReadOnlyList<string> FieldsAfterList { get; }
        public bool WriteTail { get; }

        public ItemDefinitionLayout694(int droppedStackField = DefaultDroppedStackField, IReadOnlyList<string> fieldsAfterList = null, bool writeTail = DefaultWriteTail)
        {
            if (droppedStackField < NoDroppedStackField || droppedStackField > 7)
                throw new ArgumentOutOfRangeException(nameof(droppedStackField), droppedStackField, "Must be -1 ~ 7.");

            fieldsAfterList ??= DefaultFieldsAfterList;
            if (fieldsAfterList.Count != FieldsAfterListCount)
                throw new ArgumentException($"Exactly {FieldsAfterListCount} fields are needed.", nameof(fieldsAfterList));
            // Throws for unknown names.
            foreach (string name in fieldsAfterList)
                new ItemDefinition().GetUInt(name);

            DroppedStackField = droppedStackField;
            FieldsAfterList   = fieldsAfterList.ToList();
            WriteTail         = writeTail;
        }


        public void Write(ItemDefinition definition, GamePacketWriter writer)
        {
            definition.WriteHead(writer);

            uint[] stackFields = definition.StackFields;
            for (int i = 0; i < stackFields.Length; i++)
                if (i != DroppedStackField)
                    writer.Write(stackFields[i]);

            definition.WriteMiddle(writer);

            writer.Write(definition.Unknown41);
            writer.Write(definition.Unknown42);
            writer.Write(definition.Unknown43);
            writer.Write(definition.Unknown44);

            ItemDefinition.WriteUIntList(writer, definition.Unknown47);

            foreach (string name in FieldsAfterList)
                writer.Write(definition.GetUInt(name));

            if (WriteTail)
                definition.WriteTail694(writer);
        }

        public byte[] Write(ItemDefinition definition)
        {
            var output = new List<byte>();
            Write(definition, new GamePacketWriter(output));
            return output.ToArray();
        }
    }
}
