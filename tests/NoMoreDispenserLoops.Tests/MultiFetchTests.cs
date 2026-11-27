using System.Reflection.Emit;
using HarmonyLib;

public sealed class MultiFetchTests : RegistryTest
{
    [Fact]
    public void PickupVisitorHookRunsOnlyAtTheAcceptedPickupAddition()
    {
        var add = new CodeInstruction(OpCodes.Callvirt, typeof(List<Pickupable>).GetMethod(nameof(List<Pickupable>.Add)));
        var label = new DynamicMethod("label", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        add.labels.Add(label);
        var code = DispenserFetchAreaPickupPatch.Transpiler(new[]
        {
            new CodeInstruction(OpCodes.Nop), add, new CodeInstruction(OpCodes.Ret)
        }).ToList();

        Assert.Equal(OpCodes.Dup, code[1].opcode);
        Assert.Equal(label, Assert.Single(code[1].labels));
        Assert.Empty(add.labels);
        Assert.Equal(OpCodes.Call, code[2].opcode);
        Assert.Equal(nameof(DispenserFetchAreaPickupPatch.FilterDeliveries), ((System.Reflection.MethodInfo)code[2].operand).Name);
        Assert.Same(add, code[3]);
        Assert.Throws<InvalidOperationException>(() => DispenserFetchAreaPickupPatch.Transpiler(new[] { new CodeInstruction(OpCodes.Ret) }));
        Assert.Throws<InvalidOperationException>(() => DispenserFetchAreaPickupPatch.Transpiler(new[] { add, add }));
    }

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MixedDeliveriesBatchWhenRootPickupIsSafe(bool dispenserRoot)
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        var root = dispenserRoot ? scenario.Chore : binChore;
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = scenario.Alternate };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = scenario.Chore }, new() { chore = binChore }
        };
        DispenserFetchAreaScopePatch.Prefix(root, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(root, context, contexts);
        Assert.Equal(2, contexts.Count);

        var result = Util.IterationInstruction.Halt;
        Assert.Equal(!dispenserRoot, DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
        if (!dispenserRoot)
        {
            DispenserFetchAreaPickupPatch.FilterDeliveries(scenario.DropPile);
            Assert.Same(binChore, Assert.Single(contexts).chore);
        }
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.Alternate, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void BinRootWithProtectedPickupDoesNotBatchDispenserDelivery()
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = scenario.DropPile };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = scenario.Chore }, new() { chore = binChore }
        };
        DispenserFetchAreaScopePatch.Prefix(binChore, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(binChore, context, contexts);
        Assert.Same(binChore, Assert.Single(contexts).chore);
        var result = Util.IterationInstruction.Halt;
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void FilteringDeliveriesPreservesTheOrderOfCompatibleChores()
    {
        var scenario = new FetchScenario();
        var binRoot = new FetchChore { destination = scenario.Bin };
        var firstBin = new FetchChore { destination = scenario.Bin };
        var secondBin = new FetchChore { destination = scenario.Bin };
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = scenario.DropPile };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = scenario.Chore }, new() { chore = firstBin },
            new() { chore = scenario.Chore }, new() { chore = secondBin }
        };

        SeparateDispenserDeliveriesPatch.Postfix(binRoot, context, contexts);
        Assert.Equal(new Chore[] { firstBin, secondBin }, contexts.Select(item => item.chore));
    }

    [Theory]
    [InlineData(1, 0, 0, false, false)]
    [InlineData(0, 1, 0, false, false)]
    [InlineData(0, 0, 1, false, false)]
    [InlineData(0, 0, 0, true, false)]
    [InlineData(0, 0, 0, false, true)]
    public void IncompatibleDispenserChoresDoNotRestrictBinPickups(
        int tagsHash, int requiredTag, int forbidHash, bool overrideTarget, bool driver)
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        scenario.Chore.tagsHash = tagsHash;
        scenario.Chore.requiredTag = requiredTag;
        scenario.Chore.forbidHash = forbidHash;
        scenario.Chore.overrideTarget = overrideTarget ? new object() : null;
        scenario.Chore.driver = driver ? new object() : null;
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = scenario.DropPile };
        var contexts = new List<Chore.Precondition.Context> { new() { chore = scenario.Chore } };

        DispenserFetchAreaScopePatch.Prefix(binChore, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(binChore, context, contexts);
        Assert.Single(contexts);
        var result = Util.IterationInstruction.Halt;
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void IncompatibleChoresDoNotMaskACompatibleDispenserDelivery()
    {
        var scenario = new FetchScenario();
        var binChore = new FetchChore { destination = scenario.Bin };
        var unrelated = new FetchChore { destination = scenario.Dispenser, tagsHash = 1 };
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = scenario.Alternate };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = unrelated }, new() { chore = scenario.Chore }
        };

        DispenserFetchAreaScopePatch.Prefix(binChore, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(binChore, context, contexts);
        var result = Util.IterationInstruction.Halt;
        Assert.True(DispenserFetchAreaPickupPatch.Prefix(scenario.DropPile, ref result));
        DispenserFetchAreaPickupPatch.FilterDeliveries(scenario.DropPile);
        Assert.Same(unrelated, Assert.Single(contexts).chore);
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
    }

    [Fact]
    public void AcceptedProtectedPileKeepsBinsAndSafeDispenserDeliveriesInOrder()
    {
        var source = new UnityEngine.GameObject
        {
            Cell = 20,
            Storage = new Storage { Id = 20 },
            Dispenser = new ObjectDispenser { dropOffset = new CellOffset { Value = 1 } },
            Prioritizable = new Prioritizable()
        };
        var component = new NoMoreDispenserLoopsComponent { gameObject = source };
        component.SetCheckboxValue(true);
        component.SetAllowHigherPriority(true);
        component.TestPrefabInit();
        component.TestSpawn();

        var blocked = new FetchScenario();
        var safe = new UnityEngine.GameObject
        {
            Storage = new Storage { Id = 30 },
            Dispenser = new ObjectDispenser(),
            Prioritizable = new Prioritizable { Priority = new PrioritySetting { Value = 9 } }
        };
        var safeComponent = new NoMoreDispenserLoopsComponent { gameObject = safe };
        safeComponent.TestPrefabInit();
        safeComponent.TestSpawn();
        var safeChore = new FetchChore { destination = safe.Storage };
        var bin = new FetchChore { destination = blocked.Bin };
        var pile = new Pickupable { cachedCell = 21 };
        source.Filterable.AcceptedTags.Clear();
        var context = new Chore.Precondition.Context { consumerState = Duplicant(), data = blocked.Alternate };
        var contexts = new List<Chore.Precondition.Context>
        {
            new() { chore = blocked.Chore }, new() { chore = bin }, new() { chore = safeChore }
        };

        DispenserFetchAreaScopePatch.Prefix(bin, context, out var previous);
        SeparateDispenserDeliveriesPatch.Postfix(bin, context, contexts);
        DispenserFetchAreaPickupPatch.FilterDeliveries(pile);
        Assert.Equal(3, contexts.Count);

        source.Filterable.AcceptedTags.Add(0);
        DispenserFetchAreaPickupPatch.FilterDeliveries(pile);
        Assert.Equal(new Chore[] { bin, safeChore }, contexts.Select(item => item.chore));
        DispenserFetchAreaScopePatch.Finalizer(null, previous);
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
        var contexts = new List<Chore.Precondition.Context>();
        DispenserFetchAreaScopePatch.Destinations = contexts;
        DispenserFetchAreaScopePatch.Prefix(scenario.Chore, sweeper, out var innerState);
        Assert.False(DispenserFetchAreaScopePatch.Active);
        Assert.Null(DispenserFetchAreaScopePatch.Destination);
        Assert.Null(DispenserFetchAreaScopePatch.RootChore);
        Assert.Null(DispenserFetchAreaScopePatch.Destinations);

        DispenserFetchAreaScopePatch.Finalizer(null, innerState);
        Assert.True(DispenserFetchAreaScopePatch.Active);
        Assert.Same(scenario.Dispenser, DispenserFetchAreaScopePatch.Destination);
        Assert.Same(scenario.Chore, DispenserFetchAreaScopePatch.RootChore);
        Assert.Same(contexts, DispenserFetchAreaScopePatch.Destinations);
        DispenserFetchAreaScopePatch.Finalizer(null, outerState);
        Assert.False(DispenserFetchAreaScopePatch.Active);
        Assert.Null(DispenserFetchAreaScopePatch.Destination);
    }
}
