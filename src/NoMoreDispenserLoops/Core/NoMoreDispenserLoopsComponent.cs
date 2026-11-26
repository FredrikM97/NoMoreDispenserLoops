using System;
using KSerialization;
using UnityEngine;
using HarmonyLib;

[SerializationConfig(MemberSerialization.OptIn)]
public sealed class NoMoreDispenserLoopsComponent : KMonoBehaviour, ISaveLoadable
{
    internal const string TitleKey = "STRINGS.UI.UISIDESCREENS.NOMOREDISPENSERLOOPS.TITLE";

    [Serialize]
    private bool excludeDropTile = false;

    [Serialize]
    private bool allowHigherPriority = false;

    private int registeredCell = Grid.InvalidCell;
    private int dropCell = Grid.InvalidCell;
    private int watchedBottomCell = Grid.InvalidCell;
    private int storageId;
    private bool spawned;
    private Prioritizable prioritizable;
    private HandleVector<int>.Handle terrainChanges;
    private static readonly Action<object, object> TerrainChanged = OnTerrainChanged;

    internal bool AllowHigherPriority { get { return allowHigherPriority; } }
    internal int StorageId { get { return storageId; } }
    internal PrioritySetting Priority { get { return prioritizable.GetMasterPriority(); } }
    internal int RegisteredCell { get { return registeredCell; } }

    public string CheckboxTitleKey { get { return TitleKey; } }
    public string CheckboxLabel { get { return "Exclude Drop Tile"; } }
    public string CheckboxTooltip { get { return "Prevent duplicants from picking up items on this dispenser's drop tile to refill Automatic Dispensers."; } }

    public bool GetCheckboxValue()
    {
        return excludeDropTile;
    }

    public void SetCheckboxValue(bool value)
    {
        if (excludeDropTile == value)
            return;

        UnregisterCell();
        excludeDropTile = value;
        RegisterCell();
        DropTileHighlight.Refresh(this);
    }

    internal void SetAllowHigherPriority(bool value)
    {
        if (allowHigherPriority == value)
            return;
        if (registeredCell != Grid.InvalidCell)
            DropTileRegistry.PriorityExceptionChanged(value);
        allowHigherPriority = value;
    }

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();
        Subscribe((int)GameHashes.Rotated, OnRotated);
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        DropTileRegistry.EnsureInitialized(Grid.CellCount);
        Storage storage = GetComponent<Storage>();
        if (storage == null)
            throw new System.InvalidOperationException("No More Dispenser Loops: dispenser storage is missing.");
        prioritizable = GetComponent<Prioritizable>();
        if (prioritizable == null)
            throw new System.InvalidOperationException("No More Dispenser Loops: dispenser priority is missing.");
        storageId = storage.GetInstanceID();
        DropTileRegistry.RegisterDispenser(storageId, this);
        spawned = true;
        RegisterCell();
    }

    protected override void OnCleanUp()
    {
        UnregisterCell();
        DropTileRegistry.UnregisterDispenser(storageId);
        spawned = false;
        DropTileHighlight.Clear(this);
        base.OnCleanUp();
    }

    private void OnRotated(object data)
    {
        UnregisterCell();
        RegisterCell();
        DropTileHighlight.Refresh(this);
    }

    private static void OnTerrainChanged(object context, object data)
    {
        var component = (NoMoreDispenserLoopsComponent)context;
        int previousCell = component.registeredCell;
        component.UpdateLandingCell();
        if (component.registeredCell != previousCell)
            DropTileHighlight.Refresh(component);
    }

    private void RegisterCell()
    {
        if (!excludeDropTile || !spawned)
            return;

        ObjectDispenser dispenser = GetComponent<ObjectDispenser>();
        if (dispenser == null)
            return;

        Rotatable rotatable = GetComponent<Rotatable>();
        CellOffset offset = rotatable == null
            ? dispenser.dropOffset
            : rotatable.GetRotatedCellOffset(dispenser.dropOffset);
        dropCell = Grid.OffsetCell(Grid.PosToCell(gameObject), offset);
        if (!Grid.IsValidCell(dropCell) || Grid.WorldIdx[dropCell] == byte.MaxValue)
        {
            dropCell = Grid.InvalidCell;
            return;
        }

        UpdateLandingCell();
    }

    private void UpdateLandingCell()
    {
        // Solid-change notifications can also be raised without a change in solidity.
        if (registeredCell == dropCell &&
            ((watchedBottomCell == dropCell && Grid.IsSolidCell(dropCell)) ||
             (watchedBottomCell == Grid.CellBelow(dropCell) &&
              !Grid.IsSolidCell(dropCell) && Grid.IsSolidCell(watchedBottomCell))))
            return;

        int bottomCell = dropCell;
        int landingCell = Grid.InvalidCell;
        if (Grid.IsSolidCell(dropCell))
            landingCell = dropCell;
        else
        {
            int current = dropCell;
            while (true)
            {
                int below = Grid.CellBelow(current);
                if (!Grid.IsValidCell(below) || Grid.WorldIdx[below] != Grid.WorldIdx[dropCell])
                {
                    bottomCell = current;
                    break;
                }
                bottomCell = below;
                if (Grid.IsSolidCell(below))
                {
                    landingCell = current;
                    break;
                }
                current = below;
            }
        }

        if (watchedBottomCell != bottomCell)
        {
            if (terrainChanges.IsValid())
                GameScenePartitioner.Instance.Free(ref terrainChanges);
            Grid.CellToXY(dropCell, out int x, out int topY);
            Grid.CellToXY(bottomCell, out _, out int bottomY);
            terrainChanges = GameScenePartitioner.Instance.Add("No More Dispenser Loops", gameObject,
                x, bottomY, 1, topY - bottomY + 1, GameScenePartitioner.Instance.solidChangedLayer,
                TerrainChanged, this);
            watchedBottomCell = bottomCell;
        }

        if (registeredCell != landingCell)
        {
            if (registeredCell != Grid.InvalidCell)
                DropTileRegistry.RemoveCell(registeredCell, this);
            registeredCell = landingCell;
            if (landingCell != Grid.InvalidCell)
                DropTileRegistry.AddCell(landingCell, this);
        }
    }

    private void UnregisterCell()
    {
        if (terrainChanges.IsValid() && GameScenePartitioner.Instance != null)
            GameScenePartitioner.Instance.Free(ref terrainChanges);
        dropCell = Grid.InvalidCell;
        watchedBottomCell = Grid.InvalidCell;
        if (registeredCell != Grid.InvalidCell)
        {
            DropTileRegistry.RemoveCell(registeredCell, this);
            registeredCell = Grid.InvalidCell;
        }
    }
}

[HarmonyPatch(typeof(ObjectDispenserConfig), "DoPostConfigureComplete")]
internal static class DispenserComponentRegistrationPatch
{
    private static void Postfix(GameObject go)
    {
        go.AddOrGet<NoMoreDispenserLoopsComponent>();
    }
}
