public sealed class DropTileRegistryTests : RegistryTest
{
    [Fact]
    public void OverlappingRegistrationsRemainExcludedUntilLastRemoval()
    {
        DropTileRegistry.AddCell(10);
        DropTileRegistry.AddCell(10);

        DropTileRegistry.RemoveCell(10);
        Assert.True(DropTileRegistry.IsCellExcluded(10));

        DropTileRegistry.RemoveCell(10);
        Assert.False(DropTileRegistry.HasExcludedCells);
    }

    [Fact]
    public void RegistrationsBeyondByteCapacityDoNotReleaseCellEarly()
    {
        for (int i = 0; i < 300; i++)
            DropTileRegistry.AddCell(10);

        for (int i = 0; i < 299; i++)
        {
            DropTileRegistry.RemoveCell(10);
            Assert.True(DropTileRegistry.IsCellExcluded(10));
        }

        DropTileRegistry.RemoveCell(10);
        Assert.False(DropTileRegistry.HasExcludedCells);
    }

    [Fact]
    public void ClearResetsCellsAndStorageIds()
    {
        DropTileRegistry.AddCell(10);
        DropTileRegistry.DispenserStorageIds.Add(1);

        DropTileRegistry.Clear();
        DropTileRegistry.Initialize(Grid.CellCount);

        Assert.False(DropTileRegistry.HasExcludedCells);
        Assert.False(DropTileRegistry.IsCellExcluded(10));
        Assert.Empty(DropTileRegistry.DispenserStorageIds);
    }
}
