using AsmResolver.DotNet;
using ProwlynxNET.Core;
using ProwlynxNET.Core.Extensions;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;
using RegiVM.VMBuilder;

namespace RegiVM
{
    public class RegiVMCompileStage : Stage
    {
        public override int StagePriority { get; set; } = 2;

        public RegiVMProtection Parent { get; set; }
        public RegiVMCompileStage(IProtection parentProtection)
            : base(parentProtection)
        {
            Parent = (RegiVMProtection)parentProtection;
        }

        public override void Process(ObfuscationTask t)
        {
            t.Logger.LogDebug($"[{ProtectionName}] Started Compile Stage");
            foreach (var method in Parent.TargetMethods)
            {
                ProcessMethod(method, t);
            }
        }

        private void ProcessMethod(MethodDefinition method, ObfuscationTask t)
        {
            var dupes = Parent.CompiledMethods.Where(x => x.Method == method || x.Compiler.ViableInlineTargets.Contains(method));
            if (dupes.Any())
            {
                t.Logger.LogWarn($"[{ProtectionName}] Method {method.FullName} has already been compiled previously.");
                Parent.CompiledMethods.RemoveWhere(x => x.Method == method || x.Compiler.ViableInlineTargets.Contains(method));
                return;
            }

            // Compile.
            VMCompiler compiler = new VMCompiler()
                .Compress()
                .Encrypt(true, VMEncryptionType.MultiPathOnly)
                .RegisterLimit(100000)
                .InlineCallDepth(2)
                .WithTask(t, Parent)
                .RandomizeOpCodes()
                .RandomizeRegisterNames();

            byte[] data = compiler.Compile(method);
            ulong[] opcodesUsed = compiler.GetUsedOpCodes();

            Parent.CompiledMethods.Add(new RegiVMProtection.CompiledMethodDefinition
            {
                ByteCode = data,
                Compiler = compiler,
                Method = method,
                OpCodes = opcodesUsed.Shuffle().ToArray()
            });
        }
    }
}