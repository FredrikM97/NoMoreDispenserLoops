using System;
using System.Collections.Generic;
using HarmonyLib;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

internal sealed class DispenserOptionsSideScreen : SideScreenContent
{
    private NoMoreDispenserLoopsComponent target;
    private GameObject options;
    private GameObject excludeCheckbox;
    private GameObject priorityCheckbox;
    private TreeFilterableSideScreenRow row;

    internal static TreeFilterableSideScreenRow RowPrefab;
    private static readonly AccessTools.FieldRef<TreeFilterableSideScreenRow, LocText> RowLabel =
        AccessTools.FieldRefAccess<TreeFilterableSideScreenRow, LocText>("elementName");
    private static readonly AccessTools.FieldRef<TreeFilterableSideScreenRow, GameObject> RowContent =
        AccessTools.FieldRefAccess<TreeFilterableSideScreenRow, GameObject>("elementGroup");
    private static readonly AccessTools.FieldRef<TreeFilterableSideScreenRow, MultiToggle> RowArrow =
        AccessTools.FieldRefAccess<TreeFilterableSideScreenRow, MultiToggle>("arrowToggle");

    public override bool IsValidForTarget(GameObject candidate)
    {
        return candidate != null && candidate.GetComponent<NoMoreDispenserLoopsComponent>() != null;
    }

    public override void SetTarget(GameObject candidate)
    {
        target = candidate.GetComponent<NoMoreDispenserLoopsComponent>();
        if (target == null)
            throw new InvalidOperationException("No More Dispenser Loops: options target is missing.");
        EnsureBuilt();
        Refresh();
    }

    public override void ClearTarget()
    {
        target = null;
        base.ClearTarget();
    }

    public override string GetTitle()
    {
        return "No More Dispenser Loops";
    }

    public override int GetSideScreenSortOrder()
    {
        return int.MinValue;
    }

    protected override void OnCleanUp()
    {
        GlobalSettings.Changed -= Refresh;
        base.OnCleanUp();
    }

    private void EnsureBuilt()
    {
        if (options != null)
            return;

        row = Util.KInstantiateUI<TreeFilterableSideScreenRow>(RowPrefab.gameObject, gameObject, true);
        row.gameObject.name = "No More Dispenser Loops";
        row.gameObject.AddOrGet<LayoutElement>().flexibleWidth = 1;
        // The native category checkbox requires a filter-screen parent; only reuse its foldout.
        row.ShowToggleBox(false);
        RowLabel(row).SetText("No More Dispenser Loops");
        RowArrow(row).onClick = ToggleExpanded;
        row.SetArrowToggleState(false);

        var exclude = new PCheckBox("ExcludeDropTile")
        {
            Text = "Exclude Drop Tile",
            ToolTip = "Prevent duplicants from picking up items selected in this dispenser's filter from its drop tile to refill Automatic Dispensers.",
            OnChecked = ToggleExclusion,
            DynamicSize = true,
            FlexSize = new Vector2(1, 0),
            TextAlignment = TextAnchor.MiddleLeft
        }.SetKleiBlueStyle().AddOnRealize(go => excludeCheckbox = go);
        var priority = new PCheckBox("AllowHigherPriority")
        {
            Text = "Allow Higher-Priority Dispensers",
            ToolTip = "Allow duplicants to pick up from this drop tile for Automatic Dispensers with a strictly higher building priority.",
            OnChecked = TogglePriority,
            DynamicSize = true,
            FlexSize = new Vector2(1, 0),
            TextAlignment = TextAnchor.MiddleLeft
        }.SetKleiBlueStyle().AddOnRealize(go => priorityCheckbox = go);
        var optionPanel = new PPanel("DispenserOptions")
        {
            Alignment = TextAnchor.UpperLeft,
            FlexSize = new Vector2(1, 0),
            Margin = new RectOffset(8, 8, 4, 4),
            Spacing = 4
        }
            .AddChild(exclude)
            .AddChild(priority)
            .AddOnRealize(go => options = go);
        GameObject content = optionPanel.Build();
        content.transform.SetParent(RowContent(row).transform, false);
        PUIElements.SetAnchors(content, PUIAnchoring.Stretch, PUIAnchoring.Stretch);
        GlobalSettings.Changed += Refresh;
    }

    private void ToggleExpanded()
    {
        row.SetArrowToggleState(!row.ArrowExpanded);
        Refresh();
    }

    private void ToggleExclusion(GameObject source, int state)
    {
        target.SetCheckboxValue(state == PCheckBox.STATE_UNCHECKED);
        Refresh();
    }

    private void TogglePriority(GameObject source, int state)
    {
        target.SetAllowHigherPriority(state == PCheckBox.STATE_UNCHECKED);
        Refresh();
    }

    private void Refresh()
    {
        if (target == null || options == null)
            return;
        priorityCheckbox.SetActive(GlobalSettings.EnablePriorityExceptions);
        PCheckBox.SetCheckState(excludeCheckbox, target.GetCheckboxValue() ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
        PCheckBox.SetCheckState(priorityCheckbox, target.AllowHigherPriority ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
    }
}

[HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
internal static class DispenserOptionsScreenRegistrationPatch
{
    private static readonly AccessTools.FieldRef<TreeFilterableSideScreen, TreeFilterableSideScreenRow> RowPrefab =
        AccessTools.FieldRefAccess<TreeFilterableSideScreen, TreeFilterableSideScreenRow>("rowPrefab");

    private static void Postfix(DetailsScreen __instance, List<DetailsScreen.SideScreenRef> ___sideScreens)
    {
        DetailsScreen.SideScreenRef template = null;
        TreeFilterableSideScreen filterScreen = null;
        foreach (DetailsScreen.SideScreenRef screen in ___sideScreens)
        {
            if (screen.screenPrefab is SingleCheckboxSideScreen)
                template = screen;
            if (screen.screenPrefab is TreeFilterableSideScreen treeFilter)
                filterScreen = treeFilter;
        }
        if (template == null)
            throw new InvalidOperationException("No More Dispenser Loops: configuration side-screen template not found.");
        if (filterScreen == null || RowPrefab(filterScreen) == null)
            throw new InvalidOperationException("No More Dispenser Loops: filter row template not found.");
        DispenserOptionsSideScreen.RowPrefab = RowPrefab(filterScreen);

        GameObject container = new GameObject("No More Dispenser Loops UI Prefab");
        container.SetActive(false);
        container.transform.SetParent(__instance.transform, false);
        GameObject prefab = new PPanel("No More Dispenser Loops")
        {
            FlexSize = new Vector2(1, 0)
        }.Build();
        prefab.transform.SetParent(container.transform, false);
        var screenContent = prefab.AddComponent<DispenserOptionsSideScreen>();
        ___sideScreens.Add(new DetailsScreen.SideScreenRef
        {
            name = "No More Dispenser Loops",
            screenPrefab = screenContent,
            offset = template.offset,
            tab = template.tab
        });
    }
}
