public sealed class PriorityExceptionTests : RegistryTest
{
    private static (NoMoreDispenserLoopsComponent Component, UnityEngine.GameObject Object) Dispenser(
        int id, int cell, int priority, bool exclude = false, bool allowHigher = false)
    {
        var go = new UnityEngine.GameObject
        {
            Cell = cell,
            Storage = new Storage { Id = id },
            Dispenser = new ObjectDispenser { dropOffset = new CellOffset { Value = 1 } },
            Rotatable = new Rotatable(),
            Prioritizable = new Prioritizable { Priority = new PrioritySetting { Value = priority } }
        };
        var component = new NoMoreDispenserLoopsComponent { gameObject = go };
        component.SetCheckboxValue(exclude);
        component.SetAllowHigherPriority(allowHigher);
        component.TestPrefabInit();
        component.TestSpawn();
        return (component, go);
    }

    [Fact]
    public void PriorityCheckboxIsIndependentAndUpdatesRegisteredSource()
    {
        var (source, _) = Dispenser(1, 20, 4, exclude: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        Assert.False(source.AllowHigherPriority);

        source.SetAllowHigherPriority(true);
        Assert.True(source.GetCheckboxValue());
        Assert.True(source.AllowHigherPriority);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        source.SetAllowHigherPriority(false);
        Assert.True(source.GetCheckboxValue());
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
    }

    [Fact]
    public void GlobalOptionDisablesExceptionsWithoutLosingDispenserSettings()
    {
        var (source, sourceObject) = Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        var options = new ModOptions();
        Assert.True(options.EnablePriorityExceptions);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        options.EnablePriorityExceptions = false;
        options.OnOptionsChanged();
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        Assert.False(DropTileRegistry.HasPriorityExceptions);
        Assert.True(source.AllowHigherPriority);
        int sourceReads = sourceObject.Prioritizable.Reads;
        int destinationReads = destination.Prioritizable.Reads;
        for (int i = 0; i < 10; i++)
            Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        Assert.Equal(sourceReads, sourceObject.Prioritizable.Reads);
        Assert.Equal(destinationReads, destination.Prioritizable.Reads);

        options.EnablePriorityExceptions = true;
        options.OnOptionsChanged();
        Assert.True(DropTileRegistry.HasPriorityExceptions);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
    }

    [Fact]
    public void GlobalOptionRoundTripsThroughJson()
    {
        var options = new ModOptions { EnablePriorityExceptions = false };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(options);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<ModOptions>(json);

        Assert.NotNull(restored);
        Assert.False(restored.EnablePriorityExceptions);
        restored.OnOptionsChanged();
        Assert.False(GlobalSettings.EnablePriorityExceptions);
        Assert.True(Newtonsoft.Json.JsonConvert.DeserializeObject<ModOptions>("{}").EnablePriorityExceptions);
    }

    [Fact]
    public void SavingGlobalOptionNotifiesOpenControls()
    {
        var observed = new List<bool>();
        void OnChanged() => observed.Add(GlobalSettings.EnablePriorityExceptions);
        GlobalSettings.Changed += OnChanged;
        try
        {
            var options = new ModOptions { EnablePriorityExceptions = false };
            options.OnOptionsChanged();
            options.OnOptionsChanged();
            options.EnablePriorityExceptions = true;
            options.OnOptionsChanged();
            options.OnOptionsChanged();
            Assert.Equal(new[] { false, true }, observed);
        }
        finally
        {
            GlobalSettings.Changed -= OnChanged;
        }
    }

    [Fact]
    public void GlobalOptionIsDiscoverableByPLib()
    {
        var type = typeof(ModOptions);
        var property = type.GetProperty(nameof(ModOptions.EnablePriorityExceptions));
        Assert.NotNull(property);
        Assert.Single(property.GetCustomAttributes(typeof(PeterHan.PLib.Options.OptionAttribute), false));
        var config = Assert.Single(type.GetCustomAttributes(typeof(PeterHan.PLib.Options.ConfigFileAttribute), false));
        Assert.True(((PeterHan.PLib.Options.ConfigFileAttribute)config).UseSharedConfigLocation);
        Assert.IsAssignableFrom<PeterHan.PLib.Options.IOptions>(new ModOptions());
    }

    [Fact]
    public void ExceptionIsOptInAndRequiresAnotherDispenserWithStrictlyHigherPriority()
    {
        var (source, sourceObject) = Dispenser(1, 20, 4, exclude: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        Assert.False(source.AllowHigherPriority);
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        source.SetAllowHigherPriority(true);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        Assert.True(DispenserFetchFilter.IsExcluded(pile, sourceObject.Storage));
        destination.Prioritizable.Priority = new PrioritySetting { Value = 4 };
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        destination.Prioritizable.Priority = new PrioritySetting { Value = 3 };
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        destination.Prioritizable.Priority = new PrioritySetting { Class = 1, Value = 1 };
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        source.SetAllowHigherPriority(false);
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
    }

    [Fact]
    public void ExceptionStartsAndStopsApplyingAsOtherDispensersSpawnAndCleanUp()
    {
        var (_, source) = Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var pile = new Pickupable { cachedCell = 21 };
        Assert.True(DispenserFetchFilter.IsExcluded(pile, source.Storage));

        var (other, destination) = Dispenser(2, 40, 7);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        other.TestCleanUp();
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        Assert.True(DispenserFetchFilter.IsExcluded(pile, source.Storage));
    }

    [Fact]
    public void OverlappingSourcesMustAllPermitTheDestination()
    {
        var (first, _) = Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (second, secondObject) = Dispenser(2, 20, 6, exclude: true);
        var (_, destination) = Dispenser(3, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        second.SetAllowHigherPriority(true);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        destination.Prioritizable.Priority = new PrioritySetting { Value = 6 };
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        secondObject.Rotatable.Flipped = true;
        second.Trigger((int)GameHashes.Rotated);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        first.TestCleanUp();
        Assert.False(DropTileRegistry.IsCellExcluded(21));
        second.TestCleanUp();
        Assert.False(DropTileRegistry.HasPriorityExceptions);
    }

    [Fact]
    public void ExclusionUsesEachSourcesCurrentFilter()
    {
        var (first, firstObject) = Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (second, secondObject) = Dispenser(2, 20, 6, exclude: true);
        var (_, destination) = Dispenser(3, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        pile.KPrefabID.PrefabTag = 123;

        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        firstObject.Filterable.AcceptedTags.Add(123);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        secondObject.Filterable.AcceptedTags.Add(123);
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        secondObject.Filterable.AcceptedTags.Remove(123);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        first.SetAllowHigherPriority(false);
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        firstObject.Filterable.AcceptedTags.Remove(123);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        first.TestCleanUp();
        second.TestCleanUp();
    }

    [Fact]
    public void FilterChangesImmediatelyAffectSelectionCachedTargetsAndBatchPickups()
    {
        var (_, source) = Dispenser(1, 20, 4, exclude: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        pile.KPrefabID.PrefabTag = 123;
        var alternative = new Pickupable { cachedCell = 22 };
        var representative = FetchScenario.Candidate(pile, 1);
        var group = new FetchManager.FetchablesByPrefabId();
        group.finalPickups.Add(representative);
        group.eligible.AddRange(new[] { representative, FetchScenario.Candidate(alternative, 2) });
        group.costs.Add(21, 1);
        group.costs.Add(22, 2);
        var manager = new FetchManager();
        manager.prefabIdToFetchables.Add(1, group);
        manager.pickups.Add(representative);
        var chore = new FetchChore { destination = destination.Storage };
        var context = new Chore.Precondition.Context { chore = chore, consumerState = Duplicant(), data = pile };
        var result = Util.IterationInstruction.Halt;

        Assert.Same(pile, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Same(pile, context.data);
        DispenserFetchAreaScopePatch.Prefix(chore, context, out var previous);
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(pile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);

        source.Filterable.AcceptedTags.Add(123);
        Assert.Same(alternative, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Null(context.data);
        DispenserFetchAreaScopePatch.Prefix(chore, context, out previous);
        Assert.False(DispenserFetchAreaPickupPatch.Prefix(pile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void CleanupAfterGameRegistryIsClearedDoesNotFail()
    {
        var (source, sourceObject) = Dispenser(1, 20, 4, exclude: true, allowHigher: true);

        DropTileRegistry.Clear();
        source.TestCleanUp();

        Assert.False(DropTileRegistry.HasPriorityExceptions);
        Assert.False(DropTileRegistry.IsDispenser(sourceObject.Storage));
    }

    [Fact]
    public void PriorityExceptionHandlesOverlappingCountsAcrossByteOverflow()
    {
        var sources = new List<NoMoreDispenserLoopsComponent>();
        for (int id = 1; id <= 256; id++)
            sources.Add(Dispenser(id, 20, 4, exclude: true, allowHigher: true).Component);
        var (_, destination) = Dispenser(1000, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };

        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        DropTileRegistry.AddCell(21);
        Assert.True(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        DropTileRegistry.RemoveCell(21);
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));

        sources[255].TestCleanUp();
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
        sources[254].TestCleanUp();
        Assert.False(DispenserFetchFilter.IsExcluded(pile, destination.Storage));
    }

    [Fact]
    public void FetchSelectionAndCachedTargetsUseLiveDestinationPriority()
    {
        Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        var alternative = new Pickupable { cachedCell = 22 };
        var manager = new FetchManager();
        var group = new FetchManager.FetchablesByPrefabId();
        var representative = FetchScenario.Candidate(pile, 1);
        group.finalPickups.Add(representative);
        group.eligible.AddRange(new[] { representative, FetchScenario.Candidate(alternative, 2) });
        group.costs.Add(21, 1);
        group.costs.Add(22, 2);
        manager.prefabIdToFetchables.Add(1, group);
        manager.pickups.Add(representative);
        var chore = new FetchChore { destination = destination.Storage };
        var context = new Chore.Precondition.Context { chore = chore, consumerState = Duplicant(), data = pile };

        Assert.Same(pile, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Same(pile, context.data);

        destination.Prioritizable.Priority = new PrioritySetting { Value = 4 };
        Assert.Same(alternative, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
        DispenserFetchPreconditionPatch.Prefix(ref context);
        Assert.Null(context.data);
    }

    [Fact]
    public void SelectionRecoversAlternativesWhenSomeExcludedGroupsPermitTheDestination()
    {
        Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        Dispenser(2, 30, 5, exclude: true);
        var (_, destination) = Dispenser(3, 40, 7);
        var permitted = new Pickupable { cachedCell = 21 };
        var blocked = new Pickupable { cachedCell = 31 };
        var alternate = new Pickupable { cachedCell = 32 };
        var group = new FetchManager.FetchablesByPrefabId();
        var allowedCandidate = FetchScenario.Candidate(permitted, 10, tag: 1);
        var blockedCandidate = FetchScenario.Candidate(blocked, 1, tag: 2);
        group.finalPickups.AddRange(new[] { allowedCandidate, blockedCandidate });
        group.eligible.Add(FetchScenario.Candidate(alternate, 2, tag: 2));
        group.costs.Add(32, 2);
        var manager = new FetchManager();
        manager.prefabIdToFetchables.Add(1, group);
        manager.pickups.AddRange(new[] { blockedCandidate, allowedCandidate });
        var chore = new FetchChore { destination = destination.Storage };

        Assert.Same(alternate, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
        alternate.Allowed = false;
        Assert.Same(permitted, DispenserFetchFilter.FindTarget(manager, destination.Storage, chore));
    }

    [Fact]
    public void MultiFetchKeepsDifferentDispenserPrioritiesTogetherWhenNoExceptionIsEnabled()
    {
        Dispenser(1, 20, 4, exclude: true);
        var (_, high) = Dispenser(2, 40, 7);
        var (_, low) = Dispenser(3, 60, 3);
        var root = new FetchChore { destination = high.Storage };
        var context = new Chore.Precondition.Context { chore = root, consumerState = Duplicant() };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = root }, new() { chore = new FetchChore { destination = low.Storage } }
        };

        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Equal(2, contexts.Count);
    }

    [Fact]
    public void MultiFetchSeparatesDifferentPrioritiesAndChecksTheRootDestination()
    {
        Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (_, high) = Dispenser(2, 40, 7);
        var (_, same) = Dispenser(3, 50, 7);
        var (_, low) = Dispenser(4, 60, 3);
        var root = new FetchChore { destination = high.Storage };
        var sameChore = new FetchChore { destination = same.Storage };
        var lowChore = new FetchChore { destination = low.Storage };
        var context = new Chore.Precondition.Context { chore = root, consumerState = Duplicant() };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = root }, new() { chore = sameChore }, new() { chore = lowChore }
        };

        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Equal(2, contexts.Count);
        Assert.DoesNotContain(contexts, item => item.chore == lowChore);
        GlobalSettings.EnablePriorityExceptions = false;
        contexts.Add(new Chore.Precondition.Context { chore = lowChore });
        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Equal(3, contexts.Count);
        GlobalSettings.EnablePriorityExceptions = true;

        var pile = new Pickupable { cachedCell = 21 };
        var result = Util.IterationInstruction.Halt;
        DispenserFetchAreaScopePatch.Prefix(root, context, out var previous);
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(pile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);

        DispenserFetchAreaScopePatch.Prefix(lowChore, context, out previous);
        Assert.False(DispenserFetchAreaPickupPatch.Prefix(pile, ref result));
        Assert.Equal(Util.IterationInstruction.Continue, result);
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void MixedPrioritiesBatchOnlyWhenAllDestinationsCanTakeEachPickup()
    {
        Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (_, high) = Dispenser(2, 40, 7);
        var (_, low) = Dispenser(3, 60, 3);
        var root = new FetchChore { destination = high.Storage };
        var other = new FetchChore { destination = low.Storage };
        var safe = new Pickupable { cachedCell = 22 };
        var protectedPile = new Pickupable { cachedCell = 21 };
        var context = new Chore.Precondition.Context { chore = root, consumerState = Duplicant(), data = safe };
        var contexts = new List<Chore.Precondition.Context> { new() { chore = root }, new() { chore = other } };

        DispenserFetchAreaScopePatch.Prefix(root, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Equal(2, contexts.Count);
        var result = Util.IterationInstruction.Halt;
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(protectedPile, ref result));
        DispenserFetchAreaPickupPatch.FilterDeliveries(protectedPile);
        Assert.Same(root, Assert.Single(contexts).chore);
        DispenserFetchAreaScopePatch.Finalizer(null, previous);

        context.data = protectedPile;
        contexts.Add(new Chore.Precondition.Context { chore = other });
        DispenserFetchAreaScopePatch.Prefix(root, context, out previous);
        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Same(root, Assert.Single(contexts).chore);
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(protectedPile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void PriorityExceptionChecksDoNotAllocateAfterWarmup()
    {
        Dispenser(1, 20, 4, exclude: true, allowHigher: true);
        var (_, destination) = Dispenser(2, 40, 7);
        var pile = new Pickupable { cachedCell = 21 };
        for (int i = 0; i < 100; i++)
            DispenserFetchFilter.IsExcluded(pile, destination.Storage);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            DispenserFetchFilter.IsExcluded(pile, destination.Storage);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
