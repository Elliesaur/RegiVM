using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
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

        public void RenameSubTypes(TypeDefinition typeDef2)
        {
            if (!typeDef2.IsCompilerGenerated() || typeDef2.Name.Contains("<>c") || typeDef2.Name.Contains("<>o"))
            {
                typeDef2.Name = Guid.NewGuid().ToString();
                typeDef2.Namespace = "";

                foreach (var method in typeDef2.Methods)
                {
                    if (method.IsSpecialName || method.IsConstructor || method.GenericParameters.Count > 0
                        || method.Signature!.IsGenericInstance || method.IsCompilerGenerated()
                        || method.DeclaringType!.GenericParameters.Count > 0
                        || method.Name == "GetHashCode" || method.Name == "Equals")
                    {
                        continue;
                    }
                    else
                    {
                        method.Name = Guid.NewGuid().ToString();
                    }
                    foreach (var para in method.Parameters)
                    {
                        para.GetOrCreateDefinition().Name = Guid.NewGuid().ToString();
                    }
                }
                foreach (var field in typeDef2.Fields)
                {
                    if (!field.Name.Contains("k__BackingField"))
                    {
                        if (field.IsCompilerGenerated() || field.IsSpecialName || field.IsRuntimeSpecialName || field.Signature.FieldType is GenericInstanceTypeSignature)
                        {
                            continue;
                        }
                    }
                    field.Name = Guid.NewGuid().ToString();
                }
                foreach (var prop in typeDef2.Properties)
                {
                    if (prop.IsCompilerGenerated() || prop.IsSpecialName || prop.IsRuntimeSpecialName)
                    {
                        continue;
                    }
                    prop.Name = Guid.NewGuid().ToString();
                    if (prop.GetMethod != null && prop.GetMethod.DeclaringType!.GenericParameters.Count == 0)
                    {
                        prop.GetMethod.Name = Guid.NewGuid().ToString();
                    }
                    if (prop.SetMethod != null && prop.SetMethod.DeclaringType!.GenericParameters.Count == 0)
                    {
                        prop.SetMethod.Name = Guid.NewGuid().ToString();
                    }
                }
            }
            if (typeDef2.NestedTypes.Count > 0)
            {
                foreach (var subType in typeDef2.NestedTypes)
                {
                    RenameSubTypes(subType);
                }
            }
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
