using System.Collections.Generic;
using Newtonsoft.Json;
using PeterHan.PLib.Options;

internal static class GlobalSettings
{
    internal static bool EnablePriorityExceptions = true;
    internal static event System.Action Changed;

    internal static void Apply(bool enabled)
    {
        if (EnablePriorityExceptions == enabled)
            return;
        EnablePriorityExceptions = enabled;
        Changed?.Invoke();
    }
}

[JsonObject(MemberSerialization.OptIn)]
[ConfigFile(IndentOutput: true, SharedConfigLocation: true)]
public sealed class ModOptions : IOptions
{
    [Option("Enable higher-priority exceptions",
        "Allow individual dispensers to permit pickups by higher-priority Automatic Dispensers.")]
    [JsonProperty]
    public bool EnablePriorityExceptions { get; set; } = true;

    public IEnumerable<IOptionsEntry> CreateOptions()
    {
        return null;
    }

    public void OnOptionsChanged()
    {
        GlobalSettings.Apply(EnablePriorityExceptions);
    }
}
