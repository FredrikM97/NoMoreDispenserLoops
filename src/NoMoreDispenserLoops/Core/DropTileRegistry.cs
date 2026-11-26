using System.Collections.Generic;
using System;
using HarmonyLib;

/// <summary>Fast, reference-counted lookup of dispenser drop cells.</summary>
public static class DropTileRegistry
{
    public static byte[] DropCellRefCount;
    public static readonly HashSet<int> DispenserStorageIds = new HashSet<int>();
    private static readonly Dictionary<int, int> OverflowCounts = new Dictionary<int, int>();
    private static readonly Dictionary<int, List<NoMoreDispenserLoopsComponent>> CellOwners =
        new Dictionary<int, List<NoMoreDispenserLoopsComponent>>();
    private static readonly Dictionary<int, NoMoreDispenserLoopsComponent> Dispensers =
        new Dictionary<int, NoMoreDispenserLoopsComponent>();
    private static int registeredCells;
    private static int priorityExceptions;

    public static bool HasExcludedCells { get { return registeredCells != 0; } }
    internal static bool HasPriorityExceptions { get { return GlobalSettings.EnablePriorityExceptions && priorityExceptions != 0; } }

    public static void EnsureInitialized(int cellCount)
    {
        if (cellCount <= 0)
            throw new InvalidOperationException("No More Dispenser Loops: game grid is not initialized.");
        if (DropCellRefCount == null)
            Initialize(cellCount);
        else if (DropCellRefCount.Length != cellCount)
            throw new InvalidOperationException("No More Dispenser Loops: game grid size changed unexpectedly.");
    }

    public static void Initialize(int cellCount)
    {
        DropCellRefCount = new byte[cellCount];
        DispenserStorageIds.Clear();
        OverflowCounts.Clear();
        CellOwners.Clear();
        Dispensers.Clear();
        registeredCells = 0;
        priorityExceptions = 0;
    }

    public static void Clear()
    {
        DropCellRefCount = null;
        DispenserStorageIds.Clear();
        OverflowCounts.Clear();
        CellOwners.Clear();
        Dispensers.Clear();
        registeredCells = 0;
        priorityExceptions = 0;
    }

    public static bool IsCellExcluded(int cell)
    {
        byte[] counts = DropCellRefCount;
        return counts != null && (uint)cell < (uint)counts.Length && counts[cell] != 0;
    }

    public static void AddCell(int cell)
    {
        byte[] counts = DropCellRefCount;
        if (counts == null)
            throw new InvalidOperationException("No More Dispenser Loops: registry is not initialized.");
        if ((uint)cell >= (uint)counts.Length)
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "Dispenser drop cell is outside the game grid.");

        if (counts[cell] == 0)
            registeredCells++;
        if (counts[cell] < byte.MaxValue)
            counts[cell]++;
        else
        {
            int overflow;
            OverflowCounts.TryGetValue(cell, out overflow);
            OverflowCounts[cell] = checked(overflow + 1);
        }
    }

    public static void RemoveCell(int cell)
    {
        byte[] counts = DropCellRefCount;
        if (counts == null || (uint)cell >= (uint)counts.Length || counts[cell] == 0)
            return;

        int overflow;
        if (OverflowCounts.TryGetValue(cell, out overflow))
        {
            if (overflow == 1)
                OverflowCounts.Remove(cell);
            else
                OverflowCounts[cell] = overflow - 1;
        }
        else
        {
            counts[cell]--;
            if (counts[cell] == 0)
                registeredCells--;
        }
    }

    internal static void RegisterDispenser(int storageId, NoMoreDispenserLoopsComponent dispenser)
    {
        DispenserStorageIds.Add(storageId);
        Dispensers.Add(storageId, dispenser);
    }

    internal static void UnregisterDispenser(int storageId)
    {
        DispenserStorageIds.Remove(storageId);
        Dispensers.Remove(storageId);
    }

    internal static void AddCell(int cell, NoMoreDispenserLoopsComponent owner)
    {
        AddCell(cell);
        List<NoMoreDispenserLoopsComponent> owners;
        if (!CellOwners.TryGetValue(cell, out owners))
        {
            owners = new List<NoMoreDispenserLoopsComponent>();
            CellOwners.Add(cell, owners);
        }
        owners.Add(owner);
        if (owner.AllowHigherPriority)
            priorityExceptions++;
    }

    internal static void RemoveCell(int cell, NoMoreDispenserLoopsComponent owner)
    {
        if (DropCellRefCount == null)
            return;
        List<NoMoreDispenserLoopsComponent> owners;
        if (!CellOwners.TryGetValue(cell, out owners) || !owners.Remove(owner))
            throw new InvalidOperationException("No More Dispenser Loops: drop cell owner is not registered.");
        if (owners.Count == 0)
            CellOwners.Remove(cell);
        if (owner.AllowHigherPriority)
            priorityExceptions--;
        RemoveCell(cell);
    }

    internal static void PriorityExceptionChanged(bool enabled)
    {
        priorityExceptions += enabled ? 1 : -1;
    }

    internal static bool TryGetPriority(Storage storage, out PrioritySetting priority)
    {
        if (storage == null)
        {
            priority = default(PrioritySetting);
            return false;
        }
        return TryGetPriority(storage.GetInstanceID(), out priority);
    }

    private static bool TryGetPriority(int storageId, out PrioritySetting priority)
    {
        NoMoreDispenserLoopsComponent dispenser;
        if (Dispensers.TryGetValue(storageId, out dispenser))
        {
            priority = dispenser.Priority;
            return true;
        }
        priority = default(PrioritySetting);
        return false;
    }

    internal static bool IsCellExcludedFor(int cell, Storage destination)
    {
        if (!IsCellExcluded(cell))
            return false;
        if (!HasPriorityExceptions)
            return true;

        List<NoMoreDispenserLoopsComponent> owners;
        if (!CellOwners.TryGetValue(cell, out owners))
            return true;
        int count = DropCellRefCount[cell];
        if (count == byte.MaxValue)
        {
            int overflow;
            OverflowCounts.TryGetValue(cell, out overflow);
            if (owners.Count < count || owners.Count - count != overflow)
                return true;
        }
        else if (owners.Count != count)
            return true;

        if (destination == null)
            return true;
        int destinationId = destination.GetInstanceID();
        PrioritySetting destinationPriority;
        if (!TryGetPriority(destinationId, out destinationPriority))
            return true;

        foreach (NoMoreDispenserLoopsComponent owner in owners)
        {
            if (!owner.AllowHigherPriority || destinationId == owner.StorageId ||
                destinationPriority <= owner.Priority)
                return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(Game), "OnSpawn")]
internal static class GameSpawnPatch
{
    internal static void Prefix()
    {
        DropTileRegistry.EnsureInitialized(Grid.CellCount);
    }
}

[HarmonyPatch(typeof(Game), "OnDestroy")]
internal static class GameDestroyPatch
{
    private static void Postfix()
    {
        DropTileRegistry.Clear();
    }
}
