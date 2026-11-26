public sealed class MultiFetchTests : RegistryTest
{
    [Fact]
    public void DuplicantMultiFetchSeparatesDispenserAndBinDeliveries()
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        var context = new Chore.Precondition.Context { consumerState = Duplicant() };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = scenario.Chore },
            new() { chore = binChore }
        };

        SeparateDispenserDeliveriesPatch.Postfix(binChore, context, contexts);
        Assert.Same(binChore, Assert.Single(contexts).chore);

        contexts.Add(new Chore.Precondition.Context { chore = scenario.Chore });
        SeparateDispenserDeliveriesPatch.Postfix(scenario.Chore, context, contexts);
        Assert.Same(scenario.Chore, Assert.Single(contexts).chore);
    }

    [Fact]
    public void SweeperMultiFetchDoesNotSeparateDeliveries()
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        var context = new Chore.Precondition.Context { consumerState = Sweeper() };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = scenario.Chore },
            new() { chore = binChore }
        };

        SeparateDispenserDeliveriesPatch.Postfix(scenario.Chore, context, contexts);
        Assert.Equal(2, contexts.Count);
    }

    [Fact]
    public void ScopedPickupSkipsDropDebrisAndRestoresStateAfterException()
    {
        var scenario = new FetchScenario();
        var context = new Chore.Precondition.Context { consumerState = Duplicant() };
        DispenserFetchAreaScopePatch.Prefix(scenario.Chore, context, out var previousState);
        var result = Util.IterationInstruction.Halt;

        Assert.False(DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
        Assert.Equal(Util.IterationInstruction.Continue, result);
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.Alternate, ref result));

        var error = new Exception("test");
        Assert.Same(error, DispenserFetchAreaScopePatch.Finalizer(error, previousState));
        Assert.False(DispenserFetchAreaScopePatch.Active);
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
    }

    [Fact]
    public void NestedScopesRestoreThePreviousState()
    {
        var scenario = new FetchScenario();
        var dupe = new Chore.Precondition.Context { consumerState = Duplicant() };
        var sweeper = new Chore.Precondition.Context { consumerState = Sweeper() };

        DispenserFetchAreaScopePatch.Prefix(scenario.Chore, dupe, out var outerState);
        DispenserFetchAreaScopePatch.Prefix(scenario.Chore, sweeper, out var innerState);
        Assert.False(DispenserFetchAreaScopePatch.Active);
        Assert.Null(DispenserFetchAreaScopePatch.Destination);

        DispenserFetchAreaScopePatch.Finalizer(null, innerState);
        Assert.True(DispenserFetchAreaScopePatch.Active);
        Assert.Same(scenario.Dispenser, DispenserFetchAreaScopePatch.Destination);
        DispenserFetchAreaScopePatch.Finalizer(null, outerState);
        Assert.False(DispenserFetchAreaScopePatch.Active);
        Assert.Null(DispenserFetchAreaScopePatch.Destination);
    }
}
