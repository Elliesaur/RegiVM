using AsmResolver.DotNet;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;
using RegiVM.VMBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM
{
    public class RegiVMProtection : ProtectionBase
    {
        public override string ProtectionName { get; set; } = "RegiVM";
        public override int ProtectionPriority { get; set; } = 99;

        public List<MethodDefinition> TargetMethods { get; set; } = new List<MethodDefinition>();
        public List<CompiledMethodDefinition> CompiledMethods { get; set; } = new List<CompiledMethodDefinition>();
        public int MaxDepth { get; set; }
        public int MaxIterations { get; set; }
        public RegiVMProtection()
        {
            Stages = new List<IProtectionStage>
            {
                new RegiVMAnalyseStage(this),
                new RegiVMCompileStage(this),
                new RegiVMInjectStage(this),
                new RegiVMStubMethodsStage(this),
            };
        }

        public class CompiledMethodDefinition
        {
            public MethodDefinition Method { get; set; }
            public ulong[] OpCodes { get; set; }
            public byte[] ByteCode { get; set; }
            public VMCompiler Compiler { get; set; }
            public TypeDefinition InjectedRegiVM { get; set; }
            public MethodDefinition InjectedRunMethod { get; set; }
            public MethodDefinition InjectedReturnValMethod { get; set; }
            public MethodDefinition InjectedRegiVMConstructor { get; set; }
            public PropertyDefinition InjectedOpCodeHandlerProperty { get; internal set; }
            public MethodDefinition InjectedHandlerAddMethod { get; internal set; }
            public TypeDefinition InjectedRegiVMInstructionHandlersType { get; internal set; }
            public TypeDefinition InjectedFuncDict { get; internal set; }
            public TypeDefinition InjectedFuncDelegate { get; internal set; }
        }
    }
}
