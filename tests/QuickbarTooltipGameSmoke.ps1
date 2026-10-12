# Bind the new UI hooks against the installed, non-publicized game assemblies.
# Run in a separate Windows PowerShell (.NET Framework) process; no game is started.
param(
    [Parameter(Mandatory = $true)][string]$ManagedPath,
    [Parameter(Mandatory = $true)][string]$ModPath,
    [Parameter(Mandatory = $true)][string]$SettingsPath
)
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies 'System.dll', 'System.Core.dll', 'System.Xml.dll' -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

public static class QuickbarTooltipGameSmoke {
    public static void Run(string managedPath, string modPath, string settingsPath) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            var fileName = new AssemblyName(args.Name).Name + ".dll";
            foreach (var root in new[] { managedPath, Path.Combine(managedPath, "UnityModManager") }) {
                var path = Path.Combine(root, fileName);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        var harmonyAssembly = Assembly.LoadFrom(Path.Combine(managedPath, "0Harmony.dll"));
        var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
        var attributeType = harmonyAssembly.GetType("HarmonyLib.HarmonyPatch", true);
        var harmony = Activator.CreateInstance(harmonyType, "ArdubsQOL.QuickbarTooltipSmoke");
        var mod = Assembly.Load(File.ReadAllBytes(modPath));
        var types = mod.GetTypes();
        var createProcessor = harmonyType.GetMethod("CreateClassProcessor");
        var count = 0;
        var nativeTargets = 0;
        foreach (var type in types.Where(type => type.Name.StartsWith("QuickbarTooltip") && type.IsDefined(attributeType, false))) {
            var processor = createProcessor.Invoke(harmony, new object[] { type });
            try { processor.GetType().GetMethod("Patch").Invoke(processor, null); }
            catch (TargetInvocationException exception) {
                // The target was resolved, but its original body directly calls Unity
                // native methods that CLR cannot JIT outside the game player. Do not
                // pretend this hook has had a live binding/rendering test.
                if (type.Name == "QuickbarTooltipPositionPatch" && exception.ToString().Contains(
                    "ECall methods must be packaged into a system module")) {
                    nativeTargets++;
                    Console.WriteLine("Position hook target resolved; live binding/rendering requires Unity.");
                    continue;
                }
                Console.Error.WriteLine("Failed binding " + type.Name + ": " + exception);
                throw exception.InnerException ?? exception;
            }
            Console.WriteLine("Bound " + type.Name);
            count++;
        }
        if (count != 2 || nativeTargets != 1)
            throw new Exception("Expected 2 bound tracking hooks and 1 resolved Unity positioning target.");

        var settingsType = mod.GetType("ArdubsQOL.Settings", true);
        var serializer = new XmlSerializer(settingsType);
        object settings;
        using (var reader = File.OpenText(settingsPath)) settings = serializer.Deserialize(reader);
        if (!(bool)settingsType.GetField("EnableSideQuickbarTooltips").GetValue(settings))
            throw new Exception("New tooltip option should default on when absent from old settings.");
        if (settingsType.GetField("QuickbarTooltipPlacement").GetValue(settings).ToString() != "Auto")
            throw new Exception("Old settings should receive Auto placement by default.");
        var sideType = mod.GetType("ArdubsQOL.QuickbarTooltipSide", true);
        foreach (var name in new[] { "Auto", "Left", "Right" }) {
            settingsType.GetField("QuickbarTooltipPlacement").SetValue(settings, Enum.Parse(sideType, name));
            using (var writer = new StringWriter()) {
                serializer.Serialize(writer, settings);
                using (var reader = new StringReader(writer.ToString())) {
                    var roundtrip = serializer.Deserialize(reader);
                    if (settingsType.GetField("QuickbarTooltipPlacement").GetValue(roundtrip).ToString() != name)
                        throw new Exception("Placement setting failed XML round-trip: " + name);
                }
            }
        }
        Console.WriteLine("Tracking hooks bind; existing settings load and all placement modes round-trip.");
    }
}
'@
[QuickbarTooltipGameSmoke]::Run($ManagedPath, $ModPath, $SettingsPath)
