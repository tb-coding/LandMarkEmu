using LandmarkEmulator.Shared.Network;
using LandmarkEmulator.Shared.Network.Message;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// Inventory item of SendSelfToClient for the 0.522 client, read by FUN_143257810 (dev-log/11, 46th test).
    /// </summary>
    /// <remarks>
    /// Client offsets: FUN_143257e60 reads +0x08 definition id, +0x0c u32, one ItemData (+0x10 ~ +0x20 u32 × 5, two lists) and +0x68 u32.
    /// Then +0x70 u64 guid, +0x78 ~ +0x80 u32 × 3, +0x84 bool, +0x90 u32, +0x98 u64, +0xa0 u32, +0xa4 u32, +0xa8 u64, +0xb0 u64,
    /// +0xb8 u32, +0xbc u32, +0xf0 byte, the +0x88 time, three optional parts and +0xe0 u32.
    /// The SendSelfToClient list reads one more byte after each record (inv-probe, 46th test), a second byte shifts every field after the inventory.
    /// ActivateItem needs +0xa4 == 1 and +0x90 to be 0 or 2 (+0x90 = ContainerId: 0 inventory, 2 equipment).
    /// Fields of unknown meaning start with the default of the ItemInstance constructor FUN_14409ce60.
    /// </remarks>
    public class InventoryItem694 : IWritable
    {
        /// <summary>
        /// Value of +0xa4 that ActivateItem needs.
        /// </summary>
        public const uint ActivatableA4 = 1u;

        /// <summary>
        /// Time value FUN_143268640 turns into the "no time" constant, any other value is seconds before now.
        /// </summary>
        public const uint NoTime = 0x7fffffffu;

        /// <summary>
        /// Added to the character_item guid to get the client item guid, keeps item guids apart from the player guids (character ids). 未確認.
        /// </summary>
        public const ulong GuidBase = 1ul << 48;

        public static ulong ToClientGuid(ulong itemGuid) => GuidBase + itemGuid;

        public uint DefinitionId { get; set; }
        public uint Unknown0C { get; set; }
        /// <summary>
        /// +0x68, the constructor sets 1.0f.
        /// </summary>
        public float Unknown68 { get; set; } = 1f;
        public ulong Guid { get; set; }
        public uint Count { get; set; } = 1;
        public uint Unknown7C { get; set; }
        public uint Unknown80 { get; set; }
        public bool Unknown84 { get; set; }
        /// <summary>
        /// +0x90, 0 = inventory, 2 = equipment.
        /// </summary>
        public uint ContainerId { get; set; }
        public ulong Unknown98 { get; set; }
        public uint SlotId { get; set; }
        public uint UnknownA4 { get; set; } = ActivatableA4;
        public ulong UnknownA8 { get; set; }
        public ulong UnknownB0 { get; set; }
        public uint UnknownB8 { get; set; } = uint.MaxValue;
        public uint UnknownBC { get; set; }
        public byte UnknownF0 { get; set; }
        /// <summary>
        /// +0x88, see <see cref="NoTime"/>.
        /// </summary>
        public uint Time { get; set; } = NoTime;
        public uint UnknownE0 { get; set; }
        /// <summary>
        /// Byte after the record, 0.695 keeps it in ClientItem +0x161 (XDigitalPixelX/LandMarkEmu ExtraData). 0.522 位置未確認.
        /// </summary>
        public byte ExtraByte { get; set; }

        public void Write(GamePacketWriter writer)
        {
            WriteInstanceData(writer);

            writer.Write(Guid);
            writer.Write(Count);
            writer.Write(Unknown7C);
            writer.Write(Unknown80);
            writer.Write(Unknown84);
            writer.Write(ContainerId);
            writer.Write(Unknown98);
            writer.Write(SlotId);
            writer.Write(UnknownA4);
            writer.Write(UnknownA8);
            writer.Write(UnknownB0);
            writer.Write(UnknownB8);
            writer.Write(UnknownBC);
            writer.Write(UnknownF0);
            writer.Write(Time);
            writer.Write(false); // FUN_143256a70 part (+0xc8)
            writer.Write(false); // FUN_14326f080 part (+0xd0)
            writer.Write(false); // meta data (+0xd8)
            writer.Write(UnknownE0);
            writer.Write(ExtraByte);
        }

        /// <summary>
        /// FUN_143257e60: definition id, u32, ItemData (5 u32 and two empty lists), +0x68.
        /// </summary>
        private void WriteInstanceData(GamePacketWriter writer)
        {
            writer.Write(DefinitionId);
            writer.Write(Unknown0C);
            for (int i = 0; i < 5; i++)
                writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(Unknown68);
        }
    }

    /// <summary>
    /// SendSelfToClient inventory for the 0.522 client: 12 (u32, bool) containers and the <see cref="InventoryItem694"/> list.
    /// With no items it writes the same bytes as the inventory of SendSelf522Data.
    /// </summary>
    public class InventoryData694 : IWritable
    {
        public List<(uint MaxSize, bool Visible)> Containers { get; set; } = new()
        {
            ( 40, false ),
            ( 60, false ),
            ( 10, false ),
            ( 10, false ),
            ( 10, false ),
            ( 250, false ),
            ( 250, false ),
            ( 0, true),
            ( 0, true),
            ( 0, true),
            ( 0, true),
            ( 0, true)
        };

        public List<InventoryItem694> Items { get; set; } = new();

        /// <summary>
        /// Size of container 0 (inventory), used by .item add.
        /// </summary>
        public static int InventorySize => (int)new InventoryData694().Containers[0].MaxSize;

        public void Write(GamePacketWriter writer)
        {
            foreach ((uint maxSize, bool visible) in Containers)
            {
                writer.Write(maxSize);
                writer.Write(visible);
            }

            writer.Write((uint)Items.Count);
            foreach (InventoryItem694 item in Items)
                item.Write(writer);
        }
    }
}
