using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using ProwlynxNET.Core;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;

namespace RegiVM
{
    public class RegiVMInjectStage : Stage
    {
        public override int StagePriority { get; set; } = 3;

        public RegiVMProtection Parent { get; set; }
        public RegiVMInjectStage(IProtection parentProtection)
            : base(parentProtection)
        {
            Parent = (RegiVMProtection)parentProtection;
        }

        public override void Process(ObfuscationTask t)
        {
            t.Logger.LogDebug($"[{ProtectionName}] Started Inject Stage");
            if (Parent.CompiledMethods.Count == 0)
            {
                t.Logger.LogDebug($"[{ProtectionName}] No methods compiled.");
                return;
            }

            t.Logger.LogDebug($"[{ProtectionName}] {Parent.CompiledMethods.Count} methods compiled.");

            foreach (var comp in Parent.CompiledMethods)
            {
                var thisModule = ModuleDefinition.FromFile(typeof(RegiVMInjectStage).Assembly.Location);
                var allTypes = thisModule.GetAllTypes();
                var types = allTypes
                    .Where(x => x.Namespace is not null)
                    .Where(x => x.Namespace!.Contains("VMRuntime")).ToList();
                // Private implementation details.
                types.AddRange(allTypes.Where(x => x.Name == "<PrivateImplementationDetails>"));
                
                var vmRuntimeType = types.Single(x => x.Name == "RegiVMRuntime");
                
                var memberDescriptors = t.Injector.Inject(types, t.Module);
                foreach (var memberDescriptor in memberDescriptors)
                {
                    try
                    {
                        if (memberDescriptor is TypeDefinition { IsNested: false })
                        {
                            var typeDef = (TypeDefinition)memberDescriptor;

                            if (typeDef.Name == "RegiVMRuntime")
                            {
                                comp.InjectedRegiVM = typeDef;
                                comp.InjectedRunMethod = comp.InjectedRegiVM.Methods.Single(x => x.Name == "Run");
                                comp.InjectedRegiVMConstructor = comp.InjectedRegiVM.Methods.Single(x => x.Name == ".ctor");
                                comp.InjectedReturnValMethod = comp.InjectedRegiVM.Methods.Single(x => x.Name == "GetReturnValue");
                                comp.InjectedOpCodeHandlerProperty = comp.InjectedRegiVM.Properties.Single(x => x.Name == "OpCodeHandlers");
                            }
                            else if (typeDef.Name.Contains("FuncDictionary"))
                            {
                                comp.InjectedHandlerAddMethod = typeDef.Methods.Single(x => x.Name == "Add");
                                comp.InjectedFuncDict = typeDef;
                            }
                            else if (typeDef.Name == "FuncDelegate")
                            {
                                comp.InjectedFuncDelegate = typeDef;
                                typeDef.Name = Guid.NewGuid().ToString();
                                typeDef.Namespace = "";
                                // Do NOT rename methods.
                                t.Module.TopLevelTypes.Add(typeDef);
                                continue;
                            }
                            else if (typeDef.Name == "InstructionHandlers")
                            {
                                comp.InjectedRegiVMInstructionHandlersType = typeDef;

                                typeDef.Name = Guid.NewGuid().ToString();
                                typeDef.Namespace = "";

                                // Do NOT rename methods.
                                t.Module.TopLevelTypes.Add(typeDef);
                                continue;
                            }
                            else if (typeDef.Name == "<PrivateImplementationDetails>")
                            {
                                typeDef.Name = Guid.NewGuid().ToString();
                                typeDef.Namespace = "";

                                // Do NOT rename methods.
                                t.Module.TopLevelTypes.Add(typeDef);
                                continue;
                            }
                            void RenameSubTypes(TypeDefinition typeDef2)
                            {
                                if (!typeDef2.IsCompilerGenerated())
                                {

                                    typeDef2.Name = Guid.NewGuid().ToString();
                                    typeDef2.Namespace = "";

                                    foreach (var method in typeDef2.Methods)
                                    {
                                        if (method.IsSpecialName || method.IsConstructor || method.GenericParameters.Count > 0
                                            || method.Signature!.IsGenericInstance || method.IsCompilerGenerated()
                                            || method.DeclaringType!.GenericParameters.Count > 0)
                                        {
                                            continue;
                                        }

                                        //method.Name = Guid.NewGuid().ToString();
                                        foreach (var para in method.Parameters)
                                        {
                                            para.GetOrCreateDefinition().Name = Guid.NewGuid().ToString();
                                        }
                                    }
                                    foreach (var field in typeDef2.Fields)
                                    {
                                        if (field.IsCompilerGenerated() || field.IsSpecialName || field.IsRuntimeSpecialName
                                            || field.Signature.FieldType is GenericInstanceTypeSignature)
                                        {
                                            continue;
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

                            RenameSubTypes(typeDef);
                            t.Module.TopLevelTypes.Add(typeDef);
                        }
                        else if (memberDescriptor is MethodDefinition)
                        {
                            //throw new Exception("WTF");
                        }
                    }
                    catch (Exception e)
                    {

                    }

                }
            }
        }
    }
}