using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using ProwlynxNET.Core;
using ProwlynxNET.Core.Models;
using ProwlynxNET.Core.Protections;
using RegiVM.VMBuilder;

namespace RegiVM
{
    public class RegiVMStubMethodsStage : Stage
    {
        public override int StagePriority { get; set; } = 4;

        public RegiVMProtection Parent { get; set; }
        public RegiVMStubMethodsStage(IProtection parentProtection)
            : base(parentProtection)
        {
            Parent = (RegiVMProtection)parentProtection;
        }

        public override void Process(ObfuscationTask t)
        {
            t.Logger.LogDebug($"[{ProtectionName}] Started Stub Methods Stage");
            if (Parent.CompiledMethods.Count == 0)
            {
                t.Logger.LogDebug($"[{ProtectionName}] No methods compiled.");
                return;
            }

            t.Logger.LogDebug($"[{ProtectionName}] {Parent.CompiledMethods.Count} methods compiled.");

            foreach (var comp in Parent.CompiledMethods)
            {
                var inlinedStubs = comp.Compiler.ViableInlineTargets;
                // Must skip the first, which is the current method.
                foreach (var m in inlinedStubs.Skip(1))
                {
                    // Remove method.
                    m.DeclaringType!.ToTypeDefOrRef().Resolve()!.Methods.Remove((MethodDefinition)m);
                }

                var numParams = comp.Method.Parameters.Count;
                var newArray = new CilInstruction(CilOpCodes.Newarr, t.Module.CorLibTypeFactory.Object.ToTypeDefOrRef());
                var body = comp.Method.CilMethodBody!;
                var oldInsts = body.Instructions;

                // Clear body of instructions.
                body.Instructions.Clear();
                body.ExceptionHandlers.Clear();
                body.LocalVariables.Clear();
                body.InitializeLocals = true;


                // Add the regivm local var.
                body.LocalVariables.Add(new CilLocalVariable(comp.InjectedRegiVM.ToTypeSignature()));
                body.LocalVariables.Add(new CilLocalVariable(t.Module.CorLibTypeFactory.Byte.MakeArrayType(1)));
                body.LocalVariables.Add(new CilLocalVariable(t.Module.CorLibTypeFactory.Object.MakeArrayType(1)));

                // Bytecode.
                body.Instructions.AddRange(
                    CreateArray(t.Module.CorLibTypeFactory.Byte.ToTypeDefOrRef(), comp.ByteCode.Length, comp.ByteCode));

                // Params
                body.Instructions.AddRange(
                    CreateArray(t.Module.CorLibTypeFactory.Object.ToTypeDefOrRef(), comp.Method.Parameters.Count, comp.Method.Parameters));

                // Is compressed.
                body.Instructions.Add(CilInstruction.CreateLdcI4(1));
                // Bytes.
                body.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc_1));
                // Objects.
                body.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc_2));

                // Calls
                body.Instructions.Add(new CilInstruction(CilOpCodes.Newobj, comp.InjectedRegiVMConstructor));
                // Store the regivm as an object.
                body.Instructions.Add(new CilInstruction(CilOpCodes.Stloc_0));


                // Add opcode handlers.
                // ldloc.0
                // callvirt <get opcode handlers>
                // ldc.i8 <opcode>
                // ldnull (for object instance)
                // ldftn <handler method>
                // newobj <func delegate ctor>(object, native int)
                // callvirt <funcdict add>
                var opCodeNames = comp.Compiler.OpCodes.GetAllOpCodesWithNames();

                //var shit = comp.InjectedHandlerAddMethod.DeclaringType.MakeGenericInstanceType(t.Module.CorLibTypeFactory.UInt64);
                //var memberRef = shit.GenericType.CreateMemberReference(comp.InjectedHandlerAddMethod.Name, comp.InjectedHandlerAddMethod.Signature);

                //var typeDeff = typeSig.Resolve();

                // Move methods over to current type.
                var handlerMethodsUsed = comp.OpCodes.ToDictionary(k => k, v => comp.InjectedRegiVMInstructionHandlersType.Methods.Single(x => x.Name == opCodeNames[v]));
                //var parentType = comp.Method.DeclaringType;
                //foreach (var m in handlerMethodsUsed)
                //{
                //    m.Value.DeclaringType = parentType;
                //}

                foreach (var usedOpCode in comp.OpCodes)
                {
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc_0));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Callvirt, comp.InjectedOpCodeHandlerProperty.GetMethod));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I8, (long)usedOpCode));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Ldnull));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Ldftn,
                        handlerMethodsUsed[usedOpCode]));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Newobj, comp.InjectedFuncDelegate
                        .Methods.Single(x => x.Name == ".ctor")));
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Callvirt, comp.InjectedHandlerAddMethod));
                }
                
                // Call the run method and result.
                body.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc_0));
                body.Instructions.Add(new CilInstruction(CilOpCodes.Callvirt, comp.InjectedRunMethod));
                body.Instructions.Add(new CilInstruction(CilOpCodes.Callvirt, comp.InjectedReturnValMethod));

                // Return unboxing/castclassing.
                var hasReturnType = comp.Method.Signature!.ReturnsValue;
                if (hasReturnType)
                {
                    var isPrimitiveReturn = comp.Method.Signature!.ReturnType.IsValueType;
                    // if primitive = unbox.
                    // if not, castclass to return value.
                    if (!isPrimitiveReturn)
                    {
                        body.Instructions.Add(new CilInstruction(CilOpCodes.Castclass, comp.Method.Signature.ReturnType.ToTypeDefOrRef()));
                    }
                    else
                    {
                        body.Instructions.Add(new CilInstruction(CilOpCodes.Unbox_Any, comp.Method.Signature.ReturnType.ToTypeDefOrRef()));
                    }
                }
                else
                {
                    // Pop.
                    body.Instructions.Add(new CilInstruction(CilOpCodes.Pop));
                }
                body.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
                body.ComputeMaxStack();

                // Remove unused handler methods.
                foreach (var meth in new List<MethodDefinition>(comp.InjectedRegiVMInstructionHandlersType.Methods))
                {
                    if (!handlerMethodsUsed.Values.Contains(meth))
                    {
                        comp.InjectedRegiVMInstructionHandlersType.Methods.Remove(meth);
                    }
                }

                // Just rename type again.
                Parent.RenameSubTypes(comp.InjectedRegiVMInstructionHandlersType);

                //t.Module.TopLevelTypes.Remove(comp.InjectedRegiVMInstructionHandlersType);
            }
        }

        private List<CilInstruction> CreateArray(ITypeDefOrRef reference, int numItems, byte[] items)
        {
            var res = new List<CilInstruction>
            {
                CilInstruction.CreateLdcI4(numItems),
                new CilInstruction(CilOpCodes.Newarr, reference),
                new CilInstruction(CilOpCodes.Stloc_1),
                new CilInstruction(CilOpCodes.Ldloc_1),
            };
            var index = 0;
            foreach (var b in items)
            {
                res.Add(CilInstruction.CreateLdcI4(index));

                res.Add(new CilInstruction(CilOpCodes.Ldc_I4, (int)b));
                res.Add(new CilInstruction(CilOpCodes.Stelem_I1));

                if (index < items.Length - 1)
                    res.Add(new CilInstruction(CilOpCodes.Ldloc_1));

                index++;
            }
            return res;
        }

        private List<CilInstruction> CreateArray(ITypeDefOrRef reference, int numItems, ParameterCollection parameters)
        {
            var res = new List<CilInstruction>
            {
                CilInstruction.CreateLdcI4(numItems),
                new CilInstruction(CilOpCodes.Newarr, reference),
                new CilInstruction(CilOpCodes.Stloc_2),
                new CilInstruction(CilOpCodes.Ldloc_2),
            };
            var index = 0;
            foreach (var pd in parameters)
            {
                res.Add(CilInstruction.CreateLdcI4(index));
                res.Add(new CilInstruction(CilOpCodes.Ldarg, pd));
                if (pd.ParameterType.IsValueType)
                {
                    res.Add(new CilInstruction(CilOpCodes.Box, pd.ParameterType.ToTypeDefOrRef()));
                }
                res.Add(new CilInstruction(CilOpCodes.Stelem_Ref));

                if (pd != parameters.Last())
                    res.Add(new CilInstruction(CilOpCodes.Ldloc_2));

                index++;
            }

            return res;
        }
    }
}