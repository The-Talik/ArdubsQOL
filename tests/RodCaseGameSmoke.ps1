# Run with Windows PowerShell against the installed game's real assemblies.
# This binds the rod patches in an isolated process; it does not start the game.
param(
    [Parameter(Mandatory = $true)][string]$ManagedPath,
    [Parameter(Mandatory = $true)][string]$ModPath,
    [Parameter(Mandatory = $true)][string]$BlueprintZipPath
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;

public static class RodCaseGameSmoke {
    private static Assembly mod;
    public static void BindPatches(string managedPath, string modPath) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var root in new[] { managedPath, Path.Combine(managedPath, "UnityModManager") }) {
                string path = Path.Combine(root, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        var harmonyAssembly = Assembly.LoadFrom(Path.Combine(managedPath, "0Harmony.dll"));
        var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
        var attributeType = harmonyAssembly.GetType("HarmonyLib.HarmonyPatch", true);
        var harmony = Activator.CreateInstance(harmonyType, "ArdubsQOL.RodCaseSmoke");
        mod = Assembly.Load(File.ReadAllBytes(modPath));
        // Loading all types checks the custom slot's overrides against the original,
        // non-publicized assembly the actual game uses.
        var types = mod.GetTypes();
        var createProcessor = harmonyType.GetMethod("CreateClassProcessor");
        var count = 0;
        foreach (var type in types.Where(type => type.Name.StartsWith("RodCase") && type.IsDefined(attributeType, false))) {
            var processor = createProcessor.Invoke(harmony, new object[] { type });
            try { processor.GetType().GetMethod("Patch").Invoke(processor, null); }
            catch (TargetInvocationException exception) {
                Console.Error.WriteLine("Failed binding " + type.Name + ": " + exception);
                throw exception.InnerException ?? exception;
            }
            Console.WriteLine("Bound " + type.Name);
            count++;
        }
        if (count != 11) throw new Exception("Expected 11 shared-rod patches, bound " + count + ".");
        Console.WriteLine("Shared Rods patches bind successfully against the installed game.");
    }
    public static bool IsOrdinary(string name) {
        return (bool)mod.GetType("ArdubsQOL.RodPoolRules", true)
            .GetMethod("IsOrdinaryRod", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { name });
    }
}
'@
[RodCaseGameSmoke]::BindPatches($ManagedPath, $ModPath)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($BlueprintZipPath)
try {
    $supported = 0
    foreach ($entry in $zip.Entries) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($entry.FullName)
        if (-not [RodCaseGameSmoke]::IsOrdinary($name)) { continue }
        $supported++
    }
    if ($supported -ne 30) { throw "Expected 30 ordinary metamagic rod definitions, found $supported." }
    Write-Output "Recognized all 30 ordinary lesser/regular/greater metamagic rod types; unique rods excluded."
}
finally { $zip.Dispose() }
