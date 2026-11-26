using System;
using System.Collections.Generic;
using HarmonyLib;
using PeterHan.PLib.UI;
using UnityEngine;

internal sealed class DispenserOptionsSideScreen : SideScreenContent
{
    private NoMoreDispenserLoopsComponent target;
    private HigherPriorityCheckbox priorityControl;
    private GameObject options;
    private GameObject excludeCheckbox;
    private GameObject priorityCheckbox;
    private LocText headerLabel;
    private bool expanded;

    public override bool IsValidForTarget(GameObject candidate)
    {
        return candidate != null && candidate.GetComponent<NoMoreDispenserLoopsComponent>() != null;
    }

    public override void SetTarget(GameObject candidate)
    {
        target = candidate.GetComponent<NoMoreDispenserLoopsComponent>();
        if (target == null)
            throw new InvalidOperationException("No More Dispenser Loops: options target is missing.");
        priorityControl = new HigherPriorityCheckbox(target);
        EnsureBuilt();
        Refresh();
    }

    public override void ClearTarget()
    {
        target = null;
        priorityControl = null;
        base.ClearTarget();
    }

    public override string GetTitle()
    {
        return "No More Dispenser Loops";
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

        var header = new PButton("DispenserOptionsHeader")
        {
            Text = "No More Dispenser Loops",
            OnClick = ToggleExpanded
        }.SetKleiBlueStyle().AddOnRealize(go => headerLabel = go.GetComponentInChildren<LocText>());
        var exclude = new PCheckBox("ExcludeDropTile")
        {
            Text = target.CheckboxLabel,
            ToolTip = target.CheckboxTooltip,
            OnChecked = ToggleExclusion
        }.SetKleiBlueStyle().AddOnRealize(go => excludeCheckbox = go);
        var priority = new PCheckBox("AllowHigherPriority")
        {
            Text = priorityControl.CheckboxLabel,
            ToolTip = priorityControl.CheckboxTooltip,
            OnChecked = TogglePriority
        }.SetKleiBlueStyle().AddOnRealize(go => priorityCheckbox = go);
        var optionPanel = new PPanel("DispenserOptions")
            .AddChild(exclude)
            .AddChild(priority)
            .AddOnRealize(go => options = go);
        GameObject content = new PPanel("DispenserOptionsContent")
            .AddChild(header)
            .AddChild(optionPanel)
            .Build();
        content.transform.SetParent(transform, false);
        GlobalSettings.Changed += Refresh;
    }

    private void ToggleExpanded(GameObject source)
    {
        expanded = !expanded;
        Refresh();
    }

    private void ToggleExclusion(GameObject source, int state)
    {
        target.SetCheckboxValue(state == PCheckBox.STATE_UNCHECKED);
        Refresh();
    }

    private void TogglePriority(GameObject source, int state)
    {
        priorityControl.SetCheckboxValue(state == PCheckBox.STATE_UNCHECKED);
        Refresh();
    }

    private void Refresh()
    {
        if (target == null || options == null)
            return;
        headerLabel.text = (expanded ? "- " : "+ ") + "No More Dispenser Loops";
        options.SetActive(expanded);
        priorityCheckbox.SetActive(GlobalSettings.EnablePriorityExceptions);
        PCheckBox.SetCheckState(excludeCheckbox, target.GetCheckboxValue() ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
        PCheckBox.SetCheckState(priorityCheckbox, priorityControl.GetCheckboxValue() ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
    }
}

[HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
internal static class DispenserOptionsScreenRegistrationPatch
{
    private static void Postfix(DetailsScreen __instance, List<DetailsScreen.SideScreenRef> ___sideScreens)
    {
        DetailsScreen.SideScreenRef template = null;
        foreach (DetailsScreen.SideScreenRef screen in ___sideScreens)
        {
            if (screen.screenPrefab is SingleCheckboxSideScreen)
            {
                template = screen;
                break;
            }
        }
        if (template == null)
            throw new InvalidOperationException("No More Dispenser Loops: configuration side-screen template not found.");

        GameObject container = new GameObject("No More Dispenser Loops UI Prefab");
        container.SetActive(false);
        container.transform.SetParent(__instance.transform, false);
        GameObject prefab = new PPanel("No More Dispenser Loops").Build();
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
