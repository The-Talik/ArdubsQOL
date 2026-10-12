# Run with Windows PowerShell so assemblies use the game's .NET Framework runtime.
param(
    [Parameter(Mandatory = $true)][string]$ManagedPath,
    [Parameter(Mandatory = $true)][string]$TttPath,
    [Parameter(Mandatory = $true)][string]$ModPath
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;

public static class AccursedHexCompatibilitySmoke {
    public static void Run(string managedPath, string tttPath, string modPath) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var root in new[] { managedPath, Path.Combine(managedPath, "UnityModManager"), Path.GetDirectoryName(tttPath) }) {
                string path = Path.Combine(root, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        var ttt = Assembly.LoadFrom(tttPath);
        if (ttt.GetType("TabletopTweaks.Core.NewUnitParts.UnitPartAccursedHexTTT", false) == null)
            throw new Exception("Installed Tabletop Tweaks assembly does not contain UnitPartAccursedHexTTT.");
        const string restrictionTypeName = "TabletopTweaks.Core.NewComponents.AbilitySpecific.AbilityTargetHasNoFactUnlessAccursedHex";
        if (ttt.GetType(restrictionTypeName, false) == null)
            throw new Exception("Installed Tabletop Tweaks assembly does not contain the Accursed Hex target restriction.");
        Console.WriteLine("Found installed Tabletop Tweaks Accursed Hex type.");
        var mod = Assembly.Load(File.ReadAllBytes(modPath));
        var integration = mod.GetType("ArdubsQOL.AccursedHexIntegration", true);
        if (integration == null) throw new Exception("Accursed Hex integration type was not found in Ardub's QOL.");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var logged = integration.GetField("loggedResolution", flags);
        var resolve = integration.GetMethod("Resolve", flags);
        var restrictionName = integration.GetField("RetryRestrictionTypeName", flags);
        if (logged == null || resolve == null || restrictionName == null ||
            !restrictionTypeName.Equals(restrictionName.GetRawConstantValue()))
            throw new Exception("Accursed Hex integration contract was not found.");
        Console.WriteLine("Found Ardub's QOL Accursed Hex integration contract.");
        logged.SetValue(null, true);
        bool resolved;
        try { resolved = (bool)resolve.Invoke(null, null); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        if (!resolved) throw new Exception("Installed Tabletop Tweaks Accursed Hex state was not recognized.");
        foreach (var field in new[] { "getPart", "trackedHexes", "entryUnit", "entryGuid", "entryPassedSave", "entityRefId" })
            if (integration.GetField(field, flags) == null || integration.GetField(field, flags).GetValue(null) == null)
                throw new Exception("Accursed Hex integration did not resolve " + field + ".");
        Console.WriteLine("Installed Tabletop Tweaks Accursed Hex internals resolved successfully.");
    }
}
'@
try { [AccursedHexCompatibilitySmoke]::Run($ManagedPath, $TttPath, $ModPath) }
catch { [Console]::Error.WriteLine($_.Exception.ToString()); throw }
