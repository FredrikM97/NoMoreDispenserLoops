public sealed class FetchFilterTests : RegistryTest
{
    [Fact]
    public void FilterAppliesOnlyToDuplicantDeliveriesToDispensers()
    {
        var scenario = new FetchScenario();

        Assert.True(DispenserFetchFilter.Applies(scenario.Dispenser, Duplicant()));
        Assert.False(DispenserFetchFilter.Applies(scenario.Bin, Duplicant()));
        Assert.False(DispenserFetchFilter.Applies(scenario.Dispenser, Sweeper()));
        Assert.False(DispenserFetchFilter.Applies(scenario.Dispenser, new ChoreConsumerState()));
        Assert.False(DispenserFetchFilter.Applies(scenario.Dispenser, null));
    }

    [Fact]
    public void OnlyLooseDebrisOnAnExcludedCellIsExcluded()
    {
        var scenario = new FetchScenario();
        var stored = new Pickupable { cachedCell = 10, storage = scenario.Bin };

        Assert.True(DispenserFetchFilter.IsExcluded(scenario.DropPile, scenario.Dispenser));
        Assert.False(DispenserFetchFilter.IsExcluded(scenario.Alternate, scenario.Dispenser));
        Assert.False(DispenserFetchFilter.IsExcluded(stored, scenario.Dispenser));
    }

    [Fact]
    public void FetchPatchFindsAlternativeOnlyForFilteredErrands()
    {
        var scenario = new FetchScenario();
        Game.Instance.fetchManager = scenario.Manager;
        Pickupable result = null;

        Assert.False(DispenserFindFetchTargetPatch.Prefix(scenario.Chore, Duplicant(), ref result));
        Assert.Same(scenario.Alternate, result);

        result = scenario.DropPile;
        Assert.True(DispenserFindFetchTargetPatch.Prefix(scenario.Chore, Sweeper(), ref result));
        Assert.Same(scenario.DropPile, result);

        var binChore = new FetchChore { destination = scenario.Bin };
        Assert.True(DispenserFindFetchTargetPatch.Prefix(binChore, Duplicant(), ref result));
        Assert.Same(scenario.DropPile, result);
    }

    [Fact]
    public void CachedExcludedTargetIsReconsideredOnlyForFilteredErrands()
    {
        var scenario = new FetchScenario();
        var context = new Chore.Precondition.Context
        {
            chore = scenario.Chore,
            consumerState = Duplicant(),
            data = scenario.DropPile
        };

        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Null(context.data);

        context.consumerState = Sweeper();
        context.data = scenario.DropPile;
        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Same(scenario.DropPile, context.data);
    }
}
