using HarmonyLib;
using UnityEngine;

internal static class DropTileHighlight
{
    private static NoMoreDispenserLoopsComponent selected;
    private static RangeVisualizer visualizer;

    internal static void Select(KSelectable selectable)
    {
        selected = selectable != null ? selectable.GetComponent<NoMoreDispenserLoopsComponent>() : null;
        Update();
    }

    internal static void Refresh(NoMoreDispenserLoopsComponent component)
    {
        if (selected == component)
            Update();
    }

    internal static void Clear(NoMoreDispenserLoopsComponent component)
    {
        if (selected == component)
            Reset();
    }

    internal static void Reset()
    {
        selected = null;
        if (visualizer != null)
        {
            RangeVisualizerEffect.RemoveExtraRangeVisualizer(visualizer);
            UnityEngine.Object.Destroy(visualizer.gameObject);
            visualizer = null;
        }
    }

    private static void Update()
    {
        if (selected == null || selected.RegisteredCell == Grid.InvalidCell)
        {
            if (visualizer != null)
                RangeVisualizerEffect.RemoveExtraRangeVisualizer(visualizer);
            return;
        }

        if (visualizer == null)
        {
            var marker = new GameObject("No More Dispenser Loops Drop Tile");
            visualizer = marker.AddComponent<RangeVisualizer>();
            visualizer.RangeMin = new Vector2I(0, 0);
            visualizer.RangeMax = new Vector2I(0, 0);
            visualizer.TestLineOfSight = false;
            visualizer.BlockingCb = NeverBlocked;
        }
        visualizer.transform.SetPosition(Grid.CellToPosCCC(selected.RegisteredCell, Grid.SceneLayer.Ore));
        RangeVisualizerEffect.AddExtraRangeVisualizer(visualizer);
    }

    private static bool NeverBlocked(int cell)
    {
        return false;
    }
}

[HarmonyPatch(typeof(SelectTool), nameof(SelectTool.Select))]
internal static class DropTileSelectionPatch
{
    private static void Postfix(SelectTool __instance)
    {
        DropTileHighlight.Select(__instance.selected);
    }
}

[HarmonyPatch(typeof(Game), "OnDestroy")]
internal static class DropTileHighlightCleanupPatch
{
    private static void Prefix()
    {
        DropTileHighlight.Reset();
    }
}
