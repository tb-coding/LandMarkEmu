using LandmarkEmulator.Shared.GameTable;
using System;
using System.IO;
using Xunit;

namespace LandmarkEmulator.Tests.Rules
{
    /// <summary>
    /// A table from another client build than the model: 0.522's Recipes.txt has 14 columns where the model has 18, and
    /// 525 of its PropDefs rows leave TRIVIAL blank. The rows stay, with the model's defaults where nothing fits.
    /// </summary>
    public class GameTableToleranceTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "lmemu-tests-" + Guid.NewGuid().ToString("N"));

        public GameTableToleranceTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }

        public class Row
        {
            public uint Id { get; set; }
            public uint NameId { get; set; }
            public float Height { get; set; }
            public bool Flag { get; set; }
            public string Text { get; set; } = "kept";
        }

        private GameTable<Row> Read(params string[] lines)
        {
            string file = Path.Combine(folder, "Rows.txt");
            File.WriteAllLines(file, lines);
            return new GameTable<Row>(file, true);
        }

        [Fact]
        public void FullRows_ReadAsBefore_WithoutAWarning()
        {
            GameTable<Row> table = Read("#*ID^NAME_ID^HEIGHT^FLAG^TEXT^", "7^11^2.5^1^stand^", "8^12^0^0^^");

            Assert.Equal(2, table.RecordCount);
            Assert.Null(table.LoadWarning);
            Row row = table.GetEntry(7);
            Assert.Equal(11u, row.NameId);
            Assert.Equal(2.5f, row.Height);
            Assert.True(row.Flag);
            Assert.Equal("stand", row.Text);
            Assert.Equal("", table.GetEntry(8).Text);
        }

        [Fact]
        public void MissingColumns_KeepTheModelDefaults()
        {
            GameTable<Row> table = Read("#*ID^NAME_ID^HEIGHT^", "7^11^2.5^", "8^12^3^");

            Assert.Equal(2, table.RecordCount);
            Row row = table.GetEntry(7);
            Assert.Equal(2.5f, row.Height);
            Assert.False(row.Flag);
            Assert.Equal("kept", row.Text);
            Assert.Contains("Rows.txt", table.LoadWarning);
            Assert.Contains("no column for: Flag (2), Text (2)", table.LoadWarning);
        }

        [Fact]
        public void ARowShorterThanTheHeader_KeepsItsOtherFields()
        {
            GameTable<Row> table = Read("#*ID^NAME_ID^HEIGHT^FLAG^TEXT^", "7^11", "8^12^3^1^x^");

            Assert.Equal(11u, table.GetEntry(7).NameId);
            Assert.Equal(0f, table.GetEntry(7).Height);
            Assert.Equal("x", table.GetEntry(8).Text);
            Assert.Contains("no column for: Height (1), Flag (1), Text (1)", table.LoadWarning);
        }

        [Fact]
        public void BlankNumbers_ReadAsZero_AndAreCounted()
        {
            GameTable<Row> table = Read("#*ID^NAME_ID^HEIGHT^FLAG^TEXT^", "7^^^^x^", "8^^3^1^y^");

            Assert.Equal(0u, table.GetEntry(7).NameId);
            Assert.Equal("x", table.GetEntry(7).Text);
            Assert.Equal(3f, table.GetEntry(8).Height);
            Assert.Contains("blank numbers: NameId (2), Height (1), Flag (1)", table.LoadWarning);
        }

        [Theory]
        [InlineData("Forge")]
        [InlineData("99999999999999")]
        public void UnreadableNumbers_ReadAsZero_AndNameTheColumn(string value)
        {
            GameTable<Row> table = Read("#*ID^NAME^HEIGHT^FLAG^TEXT^", $"7^{value}^1^0^x^");

            Assert.Equal(0u, table.GetEntry(7).NameId);
            Assert.Equal("x", table.GetEntry(7).Text);
            Assert.Contains("unreadable values: NameId (1)", table.LoadWarning);
            Assert.Contains("named differently: NameId <- NAME", table.LoadWarning);
        }

        [Fact]
        public void DifferentNames_AloneAreNoWarning()
        {
            Assert.Null(Read("#*ID^NAME^HEIGHT^FLAG^TEXT^", "7^11^1^0^x^").LoadWarning);
        }

        [Fact]
        public void EmptyLines_AddNoRow()
        {
            GameTable<Row> table = Read("#*ID^NAME_ID^HEIGHT^FLAG^TEXT^", "", "7^11^1^0^x^", "");

            Assert.Equal(1, table.RecordCount);
            Assert.Null(table.LoadWarning);
        }
    }
}
