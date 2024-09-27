using ProwlynxNET.Core;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;

namespace RegiVM
{
    public class RegiVMAnalyseStage : Stage
    {
        public override int StagePriority { get; set; } = 1;
        public int MaxDepth { get; private set; }
        public int MaxIterations { get; private set; }
        public RegiVMProtection Parent { get; set; }
        public RegiVMAnalyseStage(IProtection parentProtection)
            : base(parentProtection)
        {
            Parent = (RegiVMProtection)parentProtection;
        }

        public override void Process(ObfuscationTask t)
        {
            t.Logger.LogDebug($"[{ProtectionName}] Started Analyse Stage");
            var settings = t.ArgumentProvider.GetService(ProtectionName);
            if (settings != null)
            {
                var maxDepthSetting = settings["maxDepth"];
                var maxIterationsSetting = settings["maxIterations"];
                Parent.MaxDepth = maxDepthSetting != null ? (int)maxDepthSetting : 0;
                Parent.MaxIterations = maxIterationsSetting != null ? (int)maxIterationsSetting : 1;
                t.Logger.LogInfo($"[{ProtectionName}] Setting global maxDepth = {MaxDepth} and maxIterations = {MaxIterations}");
            }
            foreach (var td in t.Module.GetAllTypes().Where(x => x.Methods.Count > 0))
            {

                var toProcess = td.Methods.Where(x =>
                                    x.HasMethodBody &&
                                    t.Marker.CanProtect(ParentProtection, x, false));
                t.Logger.LogDebug($"[{ProtectionName}] Type: {td.FullName} - identified {toProcess.Count()} methods to process.");
                Parent.TargetMethods.AddRange(toProcess);
            }
        }
    }
}