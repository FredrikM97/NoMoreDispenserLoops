public sealed class FetchSelectionTests : RegistryTest
{
    [Fact]
    public void RecoversAlternativeWhenGameSelectedAnExcludedRepresentative()
    {
        var scenario = new FetchScenario();
        Assert.Same(scenario.Alternate, scenario.FindTarget());
    }

    [Fact]
    public void RetainsCloserOriginalCandidate()
    {
        var scenario = new FetchScenario();
        scenario.Group.costs[11] = 8;
        Assert.Same(scenario.Other, scenario.FindTarget());
    }

    [Fact]
    public void IgnoresUnreachableAndUnsuitableAlternatives()
    {
        var scenario = new FetchScenario();
        scenario.Group.costs[11] = -1;
        Assert.Same(scenario.Other, scenario.FindTarget());

        scenario.Group.costs[11] = 3;
        scenario.Alternate.Allowed = false;
        Assert.Same(scenario.Other, scenario.FindTarget());

        var farther = new Pickupable { cachedCell = 13 };
        scenario.Group.eligible.Add(FetchScenario.Candidate(farther, 4));
        scenario.Group.costs.Add(13, 4);
        Assert.Same(farther, scenario.FindTarget());
    }

    [Fact]
    public void ReturnsNoTargetWhenAlternativesAreUnreachable()
    {
        var scenario = new FetchScenario();
        scenario.Manager.pickups.Clear();
        scenario.Group.costs[11] = -1;
        Assert.Null(scenario.FindTarget());
    }

    [Fact]
    public void EqualCostAlternativesPreferQualityThenFreshness()
    {
        var scenario = new FetchScenario();
        var betterQuality = new Pickupable { cachedCell = 13 };
        var fresher = new Pickupable { cachedCell = 14 };
        scenario.Group.eligible.Add(FetchScenario.Candidate(betterQuality, 3, quality: 2, freshness: 1));
        scenario.Group.eligible.Add(FetchScenario.Candidate(fresher, 3, quality: 2, freshness: 2));
        scenario.Group.costs.Add(13, 3);
        scenario.Group.costs.Add(14, 3);

        Assert.Same(fresher, scenario.FindTarget());
    }

    [Fact]
    public void MultipleGroupsMatchTagAndPriorityAndPreserveTieOrder()
    {
        var scenario = new FetchScenario();
        var (manager, priorityThree, priorityOne) = MultipleGroups(scenario);

        Assert.Same(priorityThree, DispenserFetchFilter.FindTarget(manager, scenario.Dispenser, scenario.Chore));
        priorityThree.Allowed = false;
        Assert.Same(priorityOne, DispenserFetchFilter.FindTarget(manager, scenario.Dispenser, scenario.Chore));
        manager.pickups[0] = FetchScenario.Candidate(scenario.Other, 5, tag: 5, priority: 2);
        Assert.Same(scenario.Other, DispenserFetchFilter.FindTarget(manager, scenario.Dispenser, scenario.Chore));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmedUpSelectionDoesNotAllocate(bool multipleGroups)
    {
        var scenario = new FetchScenario();
        var manager = multipleGroups ? MultipleGroups(scenario).Manager : scenario.Manager;
        for (int i = 0; i < 100; i++)
            DispenserFetchFilter.FindTarget(manager, scenario.Dispenser, scenario.Chore);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            DispenserFetchFilter.FindTarget(manager, scenario.Dispenser, scenario.Chore);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static (FetchManager Manager, Pickupable PriorityThree, Pickupable PriorityOne) MultipleGroups(FetchScenario scenario)
    {
        var manager = new FetchManager();
        var group = new FetchManager.FetchablesByPrefabId();
        var priorityThree = new Pickupable { cachedCell = 16 };
        var priorityOne = new Pickupable { cachedCell = 14 };
        var retained = FetchScenario.Candidate(scenario.Other, 10, tag: 5, priority: 2);
        group.finalPickups.Add(FetchScenario.Candidate(scenario.DropPile, 1, tag: int.MinValue));
        group.finalPickups.Add(FetchScenario.Candidate(scenario.DropPile, 1, tag: 5, priority: 3));
        group.finalPickups.Add(retained);
        group.finalPickups.Add(FetchScenario.Candidate(scenario.DropPile, 1, tag: 5, priority: 1));
        group.finalPickups.Add(FetchScenario.Candidate(scenario.DropPile, 1, tag: int.MaxValue));
        group.eligible.Add(FetchScenario.Candidate(priorityOne, 5, tag: 5, priority: 1));
        group.eligible.Add(FetchScenario.Candidate(new Pickupable { cachedCell = 15 }, 1, tag: 5, priority: 2));
        group.eligible.Add(FetchScenario.Candidate(priorityThree, 5, tag: 5, priority: 3));
        group.eligible.Add(FetchScenario.Candidate(new Pickupable { cachedCell = 17 }, 1, tag: 6, priority: 3));
        group.costs.Add(14, 5);
        group.costs.Add(15, 1);
        group.costs.Add(16, 5);
        group.costs.Add(17, 1);
        manager.prefabIdToFetchables.Add(5, group);
        manager.pickups.Add(retained);
        return (manager, priorityThree, priorityOne);
    }
}
