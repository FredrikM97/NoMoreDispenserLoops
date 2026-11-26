public sealed class DispenserComponentTests : RegistryTest
{
    private static (NoMoreDispenserLoopsComponent Component, UnityEngine.GameObject Object) CreateDispenser()
    {
        var go = new UnityEngine.GameObject
        {
            Cell = 20,
            Storage = new Storage { Id = 42 },
            Dispenser = new ObjectDispenser { dropOffset = new CellOffset { Value = 1 } },
            Rotatable = new Rotatable(),
            Prioritizable = new Prioritizable()
        };
        var component = new NoMoreDispenserLoopsComponent { gameObject = go };
        component.TestPrefabInit();
        return (component, go);
    }

    [Fact]
    public void SpawnRotationToggleAndCleanupUpdateRegistrations()
    {
        var (component, go) = CreateDispenser();
        component.SetCheckboxValue(true);
        component.TestSpawn();
        Assert.True(component.GetCheckboxValue());
        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.Contains(42, DropTileRegistry.DispenserStorageIds);

        go.Rotatable.Flipped = true;
        component.Trigger((int)GameHashes.Rotated);
        Assert.False(DropTileRegistry.IsCellExcluded(21));
        Assert.True(DropTileRegistry.IsCellExcluded(19));

        component.SetCheckboxValue(false);
        Assert.False(DropTileRegistry.HasExcludedCells);
        Assert.Contains(42, DropTileRegistry.DispenserStorageIds);

        component.SetCheckboxValue(true);
        Assert.True(DropTileRegistry.IsCellExcluded(19));

        component.TestCleanUp();
        Assert.False(DropTileRegistry.HasExcludedCells);
        Assert.DoesNotContain(42, DropTileRegistry.DispenserStorageIds);
    }

    [Fact]
    public void GameSpawnKeepsPreviouslyRegisteredDispensers()
    {
        var (component, _) = CreateDispenser();
        component.SetCheckboxValue(true);
        component.TestSpawn();

        GameSpawnPatch.Prefix();

        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.Contains(42, DropTileRegistry.DispenserStorageIds);
    }

    [Fact]
    public void NewDispenserDefaultsToDisabledAndDoesNotRegisterDropCell()
    {
        var (component, _) = CreateDispenser();

        component.TestSpawn();

        Assert.False(component.GetCheckboxValue());
        Assert.False(DropTileRegistry.HasExcludedCells);
        Assert.Contains(42, DropTileRegistry.DispenserStorageIds);
    }

    [Fact]
    public void DisablingOneDispenserKeepsAnotherDispensersOverlappingCellExcluded()
    {
        var (first, _) = CreateDispenser();
        var (second, secondObject) = CreateDispenser();
        secondObject.Storage.Id = 43;
        first.SetCheckboxValue(true);
        second.SetCheckboxValue(true);
        first.TestSpawn();
        second.TestSpawn();

        first.SetCheckboxValue(false);
        Assert.True(DropTileRegistry.IsCellExcluded(21));

        second.TestCleanUp();
        Assert.False(DropTileRegistry.IsCellExcluded(21));
        Assert.Contains(42, DropTileRegistry.DispenserStorageIds);
    }

    [Fact]
    public void FallingDebrisProtectsItsLandingCellAndTracksFloorChanges()
    {
        var (component, _) = CreateDispenser();
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        component.SetCheckboxValue(true);
        component.TestSpawn();

        Assert.False(DropTileRegistry.IsCellExcluded(21));
        Assert.True(DropTileRegistry.IsCellExcluded(11));

        GameScenePartitioner.Instance.ChangeSolid(11, true);
        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.False(DropTileRegistry.IsCellExcluded(11));

        GameScenePartitioner.Instance.ChangeSolid(11, false);
        Assert.True(DropTileRegistry.IsCellExcluded(11));
        GameScenePartitioner.Instance.ChangeSolid(1, false);
        Assert.False(DropTileRegistry.HasExcludedCells);
        GameScenePartitioner.Instance.ChangeSolid(1, true);
        Assert.True(DropTileRegistry.IsCellExcluded(11));

        component.SetCheckboxValue(false);
        GameScenePartitioner.Instance.ChangeSolid(11, true);
        Assert.False(DropTileRegistry.HasExcludedCells);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnchangedAdjacentTerrainDoesNotRecalculateOrReplaceTheWatch(bool solidDropCell)
    {
        var (component, _) = CreateDispenser();
        Grid.Solid[21] = solidDropCell;
        component.SetCheckboxValue(true);
        component.TestSpawn();
        var partitioner = GameScenePartitioner.Instance;
        int calls = Grid.CellBelowCalls;

        partitioner.ChangeSolid(solidDropCell ? 21 : 11, true);

        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.Equal(1, partitioner.AddCount);
        Assert.Equal(0, partitioner.FreeCount);
        Assert.Equal(calls + (solidDropCell ? 0 : 1), Grid.CellBelowCalls);
    }

    [Fact]
    public void UnchangedLongDropKeepsItsWatchAndRegistryEntry()
    {
        var (component, _) = CreateDispenser();
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        component.SetCheckboxValue(true);
        component.TestSpawn();
        var partitioner = GameScenePartitioner.Instance;

        partitioner.ChangeSolid(11, false);

        Assert.True(DropTileRegistry.IsCellExcluded(11));
        Assert.Equal(1, partitioner.AddCount);
        Assert.Equal(0, partitioner.FreeCount);

        partitioner.ChangeSolid(11, true);
        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.False(DropTileRegistry.IsCellExcluded(11));
        Assert.Equal(2, partitioner.AddCount);
        Assert.Equal(1, partitioner.FreeCount);
    }

    [Fact]
    public void ChangingSupportWithoutMovingLandingUpdatesTheWatch()
    {
        var (component, _) = CreateDispenser();
        component.SetCheckboxValue(true);
        component.TestSpawn();
        var partitioner = GameScenePartitioner.Instance;

        partitioner.ChangeSolid(21, false);
        Assert.True(DropTileRegistry.IsCellExcluded(21));
        Assert.Equal(1, DropTileRegistry.DropCellRefCount[21]);

        partitioner.ChangeSolid(11, false);
        Assert.True(DropTileRegistry.IsCellExcluded(11));
        Assert.False(DropTileRegistry.IsCellExcluded(21));
    }

    [Fact]
    public void SharedLandingMovesForBothDispensersWhenFloorChanges()
    {
        var (first, _) = CreateDispenser();
        var (second, secondObject) = CreateDispenser();
        secondObject.Storage.Id = 43;
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        first.SetCheckboxValue(true);
        second.SetCheckboxValue(true);
        first.TestSpawn();
        second.TestSpawn();

        GameScenePartitioner.Instance.ChangeSolid(11, true);

        Assert.False(DropTileRegistry.IsCellExcluded(11));
        Assert.Equal(2, DropTileRegistry.DropCellRefCount[21]);
        first.TestCleanUp();
        Assert.True(DropTileRegistry.IsCellExcluded(21));
        second.TestCleanUp();
        Assert.False(DropTileRegistry.IsCellExcluded(21));
    }

    [Fact]
    public void LandingCellNeverCrossesWorldBoundary()
    {
        var (component, _) = CreateDispenser();
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        Grid.WorldIdx[1] = 1;
        component.SetCheckboxValue(true);
        component.TestSpawn();

        Assert.False(DropTileRegistry.HasExcludedCells);
    }

    [Fact]
    public void RotationMovesProtectionToTheOtherLandingPile()
    {
        var (component, go) = CreateDispenser();
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        Grid.Solid[19] = false;
        Grid.Solid[9] = true;
        component.SetCheckboxValue(true);
        component.TestSpawn();
        Assert.True(DropTileRegistry.IsCellExcluded(11));

        go.Rotatable.Flipped = true;
        component.Trigger((int)GameHashes.Rotated);
        Assert.False(DropTileRegistry.IsCellExcluded(11));
        Assert.True(DropTileRegistry.IsCellExcluded(19));
    }

    [Fact]
    public void HighlightTracksSelectedEnabledDispenserAndItsLandingCell()
    {
        var (component, _) = CreateDispenser();
        Grid.Solid[21] = false;
        Grid.Solid[11] = false;
        component.TestSpawn();
        DropTileHighlight.Select(new KSelectable { Component = component });
        Assert.Empty(RangeVisualizerEffect.Active);

        component.SetCheckboxValue(true);
        Assert.Equal(11, Assert.Single(RangeVisualizerEffect.Active).transform.position.Cell);
        GameScenePartitioner.Instance.ChangeSolid(11, true);
        Assert.Equal(21, Assert.Single(RangeVisualizerEffect.Active).transform.position.Cell);

        component.SetCheckboxValue(false);
        Assert.Empty(RangeVisualizerEffect.Active);
        component.SetCheckboxValue(true);
        DropTileHighlight.Select(null);
        Assert.Empty(RangeVisualizerEffect.Active);
    }
}
