using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

internal static class DispenserFetchFilter
{
    private static readonly AccessTools.FieldRef<FetchManager, List<FetchManager.Pickup>> Pickups =
        AccessTools.FieldRefAccess<FetchManager, List<FetchManager.Pickup>>("pickups");
    private static readonly AccessTools.FieldRef<FetchManager.FetchablesByPrefabId, List<FetchManager.Pickup>> EligiblePickups =
        AccessTools.FieldRefAccess<FetchManager.FetchablesByPrefabId, List<FetchManager.Pickup>>("pickupsWhichCanBePickedUp");
    private static readonly AccessTools.FieldRef<FetchManager.FetchablesByPrefabId, Dictionary<int, int>> CellCosts =
        AccessTools.FieldRefAccess<FetchManager.FetchablesByPrefabId, Dictionary<int, int>>("cellCosts");

    internal static bool Applies(Storage destination, ChoreConsumerState consumer)
    {
        return DropTileRegistry.HasExcludedCells && IsDispenser(destination) &&
            consumer != null && consumer.resume != null && !consumer.hasSolidTransferArm;
    }

    internal static bool IsDispenser(Storage destination)
    {
        return destination != null && DropTileRegistry.DispenserStorageIds.Contains(destination.GetInstanceID());
    }

    internal static bool IsExcluded(Pickupable pickup, Storage destination)
    {
        return pickup != null && pickup.storage == null &&
            DropTileRegistry.IsCellExcludedFor(pickup.cachedCell, destination);
    }

    private static bool IsBetter(FetchManager.Pickup candidate, FetchManager.Pickup current)
    {
        if (candidate.PathCost != current.PathCost)
            return candidate.PathCost < current.PathCost;
        if (candidate.foodQuality != current.foodQuality)
            return candidate.foodQuality > current.foodQuality;
        return candidate.freshness > current.freshness;
    }

    private static int FindRepresentative(List<FetchManager.Pickup> representatives, FetchManager.Pickup candidate)
    {
        int low = 0;
        int high = representatives.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            FetchManager.Pickup representative = representatives[middle];
            if (representative.tagBitsHash < candidate.tagBitsHash ||
                (representative.tagBitsHash == candidate.tagBitsHash &&
                 representative.masterPriority > candidate.masterPriority))
                low = middle + 1;
            else if (representative.tagBitsHash > candidate.tagBitsHash ||
                representative.masterPriority < candidate.masterPriority)
                high = middle - 1;
            else
                return middle;
        }
        return -1;
    }

    private static void FindSingleGroupAlternative(FetchManager.FetchablesByPrefabId group,
        FetchManager.Pickup representative, Storage destination, FetchChore chore,
        ref FetchManager.Pickup best, ref bool found)
    {
        bool hasAlternative = false;
        FetchManager.Pickup alternative = default(FetchManager.Pickup);
        Dictionary<int, int> costs = CellCosts(group);
        foreach (FetchManager.Pickup candidate in EligiblePickups(group))
        {
            if (candidate.tagBitsHash != representative.tagBitsHash ||
                candidate.masterPriority != representative.masterPriority ||
                IsExcluded(candidate.pickupable, destination))
                continue;

            int cost;
            if (!costs.TryGetValue(candidate.pickupable.cachedCell, out cost) || cost < 0)
                continue;

            FetchManager.Pickup reachable = candidate;
            reachable.PathCost = (ushort)cost;
            if ((!found || IsBetter(reachable, best)) &&
                (!hasAlternative || IsBetter(reachable, alternative)) &&
                FetchManager.IsFetchablePickup(reachable.pickupable, chore, destination))
            {
                alternative = reachable;
                hasAlternative = true;
            }
        }

        if (hasAlternative)
        {
            best = alternative;
            found = true;
        }
    }

    internal static Pickupable FindTarget(FetchManager manager, Storage destination, FetchChore chore)
    {
        bool found = false;
        FetchManager.Pickup best = default(FetchManager.Pickup);

        foreach (FetchManager.Pickup pickup in Pickups(manager))
        {
            if (!IsExcluded(pickup.pickupable, destination) &&
                FetchManager.IsFetchablePickup(pickup.pickupable, chore, destination))
            {
                best = pickup;
                found = true;
                break;
            }
        }

        // The game's representatives are sorted by tag hash, then descending
        // priority. Use that order to find excluded groups without rescanning
        // every eligible pickup for every representative.
        foreach (FetchManager.FetchablesByPrefabId group in manager.prefabIdToFetchables.Values)
        {
            List<FetchManager.Pickup> representatives = group.finalPickups;
            int excludedRepresentative = -1;
            bool multipleExcludedRepresentatives = false;
            for (int i = 0; i < representatives.Count; i++)
            {
                if (!IsExcluded(representatives[i].pickupable, destination))
                    continue;
                if (excludedRepresentative >= 0)
                {
                    multipleExcludedRepresentatives = true;
                    break;
                }
                excludedRepresentative = i;
            }
            if (excludedRepresentative < 0)
                continue;
            if (!multipleExcludedRepresentatives)
            {
                FindSingleGroupAlternative(group, representatives[excludedRepresentative],
                    destination, chore, ref best, ref found);
                continue;
            }

            bool hasAlternative = false;
            FetchManager.Pickup alternative = default(FetchManager.Pickup);
            int alternativeRepresentative = int.MaxValue;
            Dictionary<int, int> costs = CellCosts(group);
            foreach (FetchManager.Pickup candidate in EligiblePickups(group))
            {
                if (IsExcluded(candidate.pickupable, destination))
                    continue;
                int representativeIndex = FindRepresentative(representatives, candidate);
                if (representativeIndex < 0 || !IsExcluded(representatives[representativeIndex].pickupable, destination))
                    continue;

                int cost;
                if (!costs.TryGetValue(candidate.pickupable.cachedCell, out cost) || cost < 0)
                    continue;

                FetchManager.Pickup reachable = candidate;
                reachable.PathCost = (ushort)cost;
                if (found && !IsBetter(reachable, best))
                    continue;
                if (hasAlternative && !IsBetter(reachable, alternative) &&
                    (representativeIndex >= alternativeRepresentative || IsBetter(alternative, reachable)))
                    continue;
                if (FetchManager.IsFetchablePickup(reachable.pickupable, chore, destination))
                {
                    alternative = reachable;
                    alternativeRepresentative = representativeIndex;
                    hasAlternative = true;
                }
            }
            if (hasAlternative)
            {
                best = alternative;
                found = true;
            }
        }
        return found ? best.pickupable : null;
    }
}

[HarmonyPatch(typeof(FetchChore), nameof(FetchChore.FindFetchTarget))]
internal static class DispenserFindFetchTargetPatch
{
    internal static bool Prefix(FetchChore __instance, ChoreConsumerState consumer_state, ref Pickupable __result)
    {
        if (!DispenserFetchFilter.Applies(__instance.destination, consumer_state))
            return true;

        __result = DispenserFetchFilter.FindTarget(Game.Instance.fetchManager, __instance.destination, __instance);
        return false;
    }
}

[HarmonyPatch]
internal static class DispenserFetchPreconditionPatch
{
    private static MethodBase TargetMethod()
    {
        return FetchChore.IsFetchTargetAvailable.fn.Method;
    }

    internal static void Prefix(ref Chore.Precondition.Context context)
    {
        FetchChore chore = context.chore as FetchChore;
        if (chore != null && DispenserFetchFilter.Applies(chore.destination, context.consumerState) &&
            DispenserFetchFilter.IsExcluded(context.data as Pickupable, chore.destination))
            context.data = null;
    }
}

[HarmonyPatch(typeof(FetchAreaChore), nameof(FetchAreaChore.GatherNearbyFetchChores))]
internal static class SeparateDispenserDeliveriesPatch
{
    internal static void Postfix(FetchChore root_chore, Chore.Precondition.Context context,
        List<Chore.Precondition.Context> succeeded_contexts)
    {
        ChoreConsumerState consumer = context.consumerState;
        if (!DropTileRegistry.HasExcludedCells || consumer == null ||
            consumer.resume == null || consumer.hasSolidTransferArm)
            return;

        bool rootIsDispenser = DispenserFetchFilter.IsDispenser(root_chore.destination);
        PrioritySetting rootPriority = default(PrioritySetting);
        bool separatePriorities = rootIsDispenser && DropTileRegistry.HasPriorityExceptions;
        bool hasRootPriority = !separatePriorities || DropTileRegistry.TryGetPriority(root_chore.destination, out rootPriority);
        for (int i = succeeded_contexts.Count - 1; i >= 0; i--)
        {
            FetchChore chore = succeeded_contexts[i].chore as FetchChore;
            if (chore == null)
                continue;
            bool isDispenser = DispenserFetchFilter.IsDispenser(chore.destination);
            PrioritySetting priority;
            if (isDispenser != rootIsDispenser ||
                (separatePriorities && (!hasRootPriority ||
                    !DropTileRegistry.TryGetPriority(chore.destination, out priority) || priority != rootPriority)))
                succeeded_contexts.RemoveAt(i);
        }
    }
}

[HarmonyPatch(typeof(FetchAreaChore.StatesInstance), nameof(FetchAreaChore.StatesInstance.Begin))]
internal static class DispenserFetchAreaScopePatch
{
    [ThreadStatic]
    internal static bool Active;
    [ThreadStatic]
    internal static Storage Destination;

    internal struct ScopeState
    {
        internal bool Active;
        internal Storage Destination;
    }

    internal static void Prefix(FetchChore ___rootChore, Chore.Precondition.Context context, out ScopeState __state)
    {
        __state = new ScopeState { Active = Active, Destination = Destination };
        Active = DispenserFetchFilter.Applies(___rootChore.destination, context.consumerState);
        Destination = Active ? ___rootChore.destination : null;
    }

    internal static Exception Finalizer(Exception __exception, ScopeState __state)
    {
        Active = __state.Active;
        Destination = __state.Destination;
        return __exception;
    }
}

[HarmonyPatch]
internal static class DispenserFetchAreaPickupPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo target = null;
        foreach (Type nested in typeof(FetchAreaChore.StatesInstance).GetNestedTypes(BindingFlags.NonPublic))
        {
            foreach (MethodInfo method in nested.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (!method.Name.StartsWith("<Begin>b__", StringComparison.Ordinal) ||
                    method.ReturnType != typeof(Util.IterationInstruction) ||
                    parameters.Length != 2 || parameters[0].ParameterType != typeof(object) ||
                    parameters[1].ParameterType != typeof(object))
                    continue;

                if (target != null)
                    throw new InvalidOperationException("No More Dispenser Loops: multiple fetch-area pickup visitors found.");
                target = method;
            }
        }
        return target ?? throw new MissingMethodException("No More Dispenser Loops: fetch-area pickup visitor not found.");
    }

    internal static bool Prefix(object obj, ref Util.IterationInstruction __result)
    {
        if (DispenserFetchAreaScopePatch.Active &&
            DispenserFetchFilter.IsExcluded(obj as Pickupable, DispenserFetchAreaScopePatch.Destination))
        {
            __result = Util.IterationInstruction.Continue;
            return false;
        }
        return true;
    }
}
