using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using ProwlynxNET.Core;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;
using RegiVM.VMBuilder;

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

                // Shuffle methods in types to ensure it is not the same order.
                foreach (var type in types)
                {
                    var originalMethods = type.Methods.Shuffle().ToList();
                    type.Methods.Clear();
                    foreach (var m in originalMethods)
                    {
                        type.Methods.Add(m);
                    }
                }
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
                            

                            Parent.RenameSubTypes(typeDef);
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