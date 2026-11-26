internal sealed class HigherPriorityCheckbox : ICheckboxControl
{
    private readonly NoMoreDispenserLoopsComponent dispenser;

    internal HigherPriorityCheckbox(NoMoreDispenserLoopsComponent dispenser)
    {
        this.dispenser = dispenser;
    }

    public string CheckboxTitleKey { get { return NoMoreDispenserLoopsComponent.TitleKey; } }
    public string CheckboxLabel { get { return "Allow Higher-Priority Dispensers"; } }
    public string CheckboxTooltip
    {
        get { return "Allow duplicants to pick up from this drop tile for Automatic Dispensers with a strictly higher building priority."; }
    }
    public bool GetCheckboxValue() { return dispenser.AllowHigherPriority; }
    public void SetCheckboxValue(bool value) { dispenser.SetAllowHigherPriority(value); }
}
