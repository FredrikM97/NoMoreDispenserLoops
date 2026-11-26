global using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

// The game registry and multi-fetch scope are static, so each test starts clean.
public abstract class RegistryTest : IDisposable
{
    protected RegistryTest()
    {
        Array.Fill(Grid.Solid, true);
        Array.Fill(Grid.WorldIdx, (byte)0);
        Grid.CellBelowCalls = 0;
        GameScenePartitioner.Instance = new GameScenePartitioner();
        DropTileHighlight.Reset();
        DropTileRegistry.Initialize(Grid.CellCount);
        GlobalSettings.EnablePriorityExceptions = true;
        DispenserFetchAreaScopePatch.Active = false;
        DispenserFetchAreaScopePatch.Destination = null;
        Game.Instance = new Game();
    }

    public void Dispose()
    {
        DropTileRegistry.Clear();
        DropTileHighlight.Reset();
        GlobalSettings.EnablePriorityExceptions = true;
        DispenserFetchAreaScopePatch.Active = false;
        DispenserFetchAreaScopePatch.Destination = null;
        Game.Instance = new Game();
    }

    protected static ChoreConsumerState Duplicant() => new() { resume = new object() };
    protected static ChoreConsumerState Sweeper() => new() { hasSolidTransferArm = true };
}

internal sealed class FetchScenario
{
    internal readonly Storage Dispenser = new() { Id = 1 };
    internal readonly Storage Bin = new() { Id = 2 };
    internal readonly Pickupable DropPile = new() { cachedCell = 10 };
    internal readonly Pickupable Alternate = new() { cachedCell = 11 };
    internal readonly Pickupable Other = new() { cachedCell = 12 };
    internal readonly FetchManager Manager = new();
    internal readonly FetchManager.FetchablesByPrefabId Group = new();
    internal readonly FetchChore Chore;

    internal FetchScenario()
    {
        DropTileRegistry.AddCell(10);
        DropTileRegistry.DispenserStorageIds.Add(Dispenser.Id);
        Chore = new FetchChore { destination = Dispenser };
        var excluded = Candidate(DropPile, 1);
        Group.finalPickups.Add(excluded);
        Group.eligible.AddRange(new[] { excluded, Candidate(Alternate, 3) });
        Group.costs.Add(10, 1);
        Group.costs.Add(11, 3);
        Manager.prefabIdToFetchables.Add(1, Group);
        Manager.pickups.AddRange(new[] { excluded, Candidate(Other, 5, tag: 2) });
    }

    internal Pickupable FindTarget() => DispenserFetchFilter.FindTarget(Manager, Dispenser, Chore);

    internal static FetchManager.Pickup Candidate(Pickupable item, int cost, int tag = 1,
        int priority = 1, int quality = 1, int freshness = 1) =>
        new()
        {
            pickupable = item,
            PathCost = (ushort)cost,
            tagBitsHash = tag,
            masterPriority = priority,
            foodQuality = quality,
            freshness = freshness
        };
}
