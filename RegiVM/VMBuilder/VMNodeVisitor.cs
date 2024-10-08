﻿using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using Echo.ControlFlow.Regions;
using Microsoft.Win32;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;
using System.Reflection.Emit;

namespace RegiVM.VMBuilder
{
    public partial class VMCompiler
    {
        public enum DryPass
        {
            ExceptionHandlers = 1,
            Phi = 2,
            Regular = 3
        }
        public class VMNodeVisitorDryPass : IAstNodeVisitor<CilInstruction, VMCompiler>
        {
            private DryPass dryPassType;

            public VMNodeVisitorDryPass(DryPass dryPass)
            {
                this.dryPassType = dryPass;
            }

            public void Visit(CompilationUnit<CilInstruction> unit, VMCompiler state)
            {
                unit.Root.Accept(this, state);
            }

            public void Visit(AssignmentStatement<CilInstruction> statement, VMCompiler state)
            {
                statement.Expression.Accept(this, state);
            }

            public void Visit(ExpressionStatement<CilInstruction> statement, VMCompiler state)
            {
                statement.Expression.Accept(this, state);
            }
            
            public void Visit(BlockStatement<CilInstruction> statement, VMCompiler state)
            {
                foreach (var s in statement.Statements)
                {
                    s.Accept(this, state);
                }
            }

            public void Visit(ExceptionHandlerStatement<CilInstruction> statement, VMCompiler state)
            {
                if (dryPassType == DryPass.ExceptionHandlers)
                {
                    VMExceptionHandler vmHandler = new VMExceptionHandler();
                    foreach (var handler in statement.Handlers.Select(x => (CilExceptionHandler)x.Tag))
                    {
                        vmHandler = state.ExceptionHandlers.FirstOrDefault(x => x.TryOffsetStart == handler.TryStart!.Offset
                            && x.TryOffsetEnd == handler.TryEnd!.Offset
                            && x.Type == handler.HandlerType.ToVMBlockHandlerType()
                            );
                        if (vmHandler.PlaceholderStartInstruction != null)
                        {
                            break;
                        }
                        // May need to add more search params to this.
                    }
                    if (vmHandler.PlaceholderStartInstruction == null)
                    {
                        throw new Exception("Could not find associated vmHandler for the exception types.");
                    }
                    var indx = state.InstructionBuilder.FindIndexForObject(statement, state.MethodIndex);
                    // Instead of adding, look up the previous block.
                    state.InstructionBuilder.AddDryPass(state.OpCodes.StartRegionBlock, vmHandler.PlaceholderStartInstruction!, state.MethodIndex, indx);
                }
                else
                {
                    state.InstructionBuilder.AddDryPass(state.OpCodes.StartRegionBlock, statement, state.MethodIndex);
                }

                foreach (var s in statement.ProtectedBlock.Statements)
                {
                     s.Accept(this, state);
                }
                foreach (var s in statement.Handlers)
                {
                    s.Accept(this, state);
                }
            }

            public void Visit(HandlerClause<CilInstruction> clause, VMCompiler state)
            {
                foreach (var s in clause.Contents.Statements)
                {
                    s.Accept(this, state);
                }
            }

            public void Visit(PhiStatement<CilInstruction> statement, VMCompiler state)
            {
                // Create a compile time struct for loading phi
                // do a pass where I make placeholder vars
                // do another where I create the associated instruction to refer to later.
                if (dryPassType == DryPass.Phi)
                {

                }
                else
                {

                }
            }

            public void Visit(VariableExpression<CilInstruction> expression, VMCompiler state)
            {
            }
            public void Visit(InstructionExpression<CilInstruction> expression, VMCompiler state)
            {
                if (expression.Arguments.Count > 0)
                {
                    foreach (var a in expression.Arguments)
                    {
                        a.Accept(this, state);
                    }
                    var inst = expression.Instruction;
                    if (inst.IsStloc())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.LoadOrStoreRegister, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code.ToString().StartsWith("Conv_"))
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.ConvertNumber, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Neg)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Neg, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Not)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Not, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Add || inst.OpCode.Code == CilCode.Add_Ovf || inst.OpCode.Code == CilCode.Add_Ovf_Un)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Add, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Sub || inst.OpCode.Code == CilCode.Sub_Ovf || inst.OpCode.Code == CilCode.Sub_Ovf_Un)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Sub, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Mul || inst.OpCode.Code == CilCode.Mul_Ovf || inst.OpCode.Code == CilCode.Mul_Ovf_Un)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Mul, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Div || inst.OpCode.Code == CilCode.Div_Un)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Div, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.And)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.And, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Or)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Or, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Xor)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Xor, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Dup)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Duplicate, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Ret, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ceq
                        || inst.OpCode.Code == CilCode.Cgt
                        || inst.OpCode.Code == CilCode.Clt)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Comparator, inst, state.MethodIndex);
                    }
                    if (inst.IsBranch() && inst.OpCode.Code != CilCode.Switch)
                    {
                        if (inst.IsUnconditionalBranch())
                        {
                            throw new Exception("Unconditional branch with stack values??");
                        }
                        switch (inst.OpCode.Code)
                        {
                            case CilCode.Beq:
                            case CilCode.Bne_Un:
                            case CilCode.Bge:
                            case CilCode.Bgt:
                            case CilCode.Bgt_Un:
                            case CilCode.Bge_Un:
                            case CilCode.Ble:
                            case CilCode.Blt:
                            case CilCode.Blt_Un:
                            case CilCode.Ble_Un:
                                state.InstructionBuilder.AddDryPass(state.OpCodes.Comparator, inst, state.MethodIndex);
                                break;
                        }
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpBool, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Switch)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpBool, inst, state.MethodIndex);
                    }
                    if ((inst.OpCode.Code == CilCode.Call ||
                        inst.OpCode.Code == CilCode.Callvirt ||
                        inst.OpCode.Code == CilCode.Newobj) && inst.Operand is IMethodDefOrRef)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpCall, inst, state.MethodIndex);
                    }
                }
                else
                {
                    var inst = expression.Instruction;
                    if (inst.IsLdcI4())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_I8)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_R4)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_R8)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ldstr)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Ret, inst, state.MethodIndex);
                    }
                    if (inst.IsLdloc())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.LoadOrStoreRegister, inst, state.MethodIndex);
                    }
                    if (inst.IsLdarg())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.ParameterLoad, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Endfinally)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.EndFinally, inst, state.MethodIndex);
                    }
                    if (inst.IsUnconditionalBranch())
                    {
                        // Load num.
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst, state.MethodIndex);
                        // Jump.
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpBool, inst, state.MethodIndex);
                    }
                    if ((inst.OpCode.Code == CilCode.Call || inst.OpCode.Code == CilCode.Newobj) && inst.Operand is IMethodDefOrRef)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpCall, inst, state.MethodIndex);
                    }
                }

            }
            
        }

        public class VMNodeVisitor : IAstNodeVisitor<CilInstruction, VMCompiler, VMRegister>
        {

            public VMNodeVisitor() 
            {
            }

            public VMRegister Visit(CompilationUnit<CilInstruction> unit, VMCompiler state)
            {
                return unit.Root.Accept(this, state);
                //unit.Accept(this, state);
            }

            public VMRegister Visit(AssignmentStatement<CilInstruction> statement, VMCompiler state)
            {
                //statement.Accept(this, state);
                var x = statement.Expression.Accept(this, state);
                return x;
            }

            public VMRegister Visit(ExpressionStatement<CilInstruction> statement, VMCompiler state)
            {
                return statement.Expression.Accept(this, state);
            }

            public VMRegister Visit(PhiStatement<CilInstruction> statement, VMCompiler state)
            {
                // We have to push something, otherwise things will break!
                // A phi statement is used when it isn't quite clear what the value will be.
                // This often happens in a catch statement when the first instruction will be "stloc".
                // The AST has no idea how to handle the stack because technically something is pushed to the stack in runtime!!
                //var phiInst = new LoadPhiInstruction(state);
                //state.InstructionBuilder.Add(phiInst);
                var temp = state.RegisterHelper.PushTemp();
                temp.DataType = DataType.Phi;
                return temp;
            }

            public VMRegister Visit(BlockStatement<CilInstruction> statement, VMCompiler state)
            {
                VMRegister reg = null!;
                foreach (var s in statement.Statements)
                {
                    reg = s.Accept(this, state);
                }
                return reg!;
            }

            public VMRegister Visit(ExceptionHandlerStatement<CilInstruction> statement, VMCompiler state)
            {
                List<VMRegister> registers = new List<VMRegister>();

                // Visit all statements in the protected block.
                var startBlockInst = new StartBlockInstruction(state, statement.Handlers, VMBlockType.Protected);
                state.InstructionBuilder.Add(startBlockInst, null!, state.MethodIndex);

                foreach (var s in statement.ProtectedBlock.Statements)
                {
                    var rFromInner = s.Accept(this, state);
                    if (rFromInner != null)
                        registers.Add(rFromInner);
                }

                // Then visit all statements in the handlers for the exception block.
                foreach (var s in statement.Handlers)
                {
                    var rFromInner = s.Accept(this, state);
                    if (rFromInner != null)
                        registers.Add(rFromInner);
                }

                return null!;
            }

            public VMRegister Visit(HandlerClause<CilInstruction> clause, VMCompiler state)
            {
                List<VMRegister> registers = new List<VMRegister>();

                foreach (var s in clause.Contents.Statements)
                {
                    var rFromInner = s.Accept(this, state);
                    if (rFromInner != null)
                        registers.Add(rFromInner);
                }

                return null!;
            }

            public VMRegister Visit(VariableExpression<CilInstruction> expression, VMCompiler state)
            {
                
                return null!;
                //expression.Accept(this, state);
            }

            public VMRegister Visit(InstructionExpression<CilInstruction> expression, VMCompiler state)
            {
                if (expression.Arguments.Count > 0)
                {
                    foreach (var a in expression.Arguments)
                    {
                        var r = a.Accept(this, state);
                        if (r != null && !state.RegisterHelper.Temporary.Contains(r))
                            // Push the register to the temp stack.
                            state.RegisterHelper.Temporary.Push(r);
                    }
                    var inst = expression.Instruction;
                    if (inst.IsStloc())
                    {
                        var storeInst = new LocalStoreInstruction(state, inst, (CilLocalVariable)inst.Operand!);
                        state.InstructionBuilder.Add(storeInst, inst, state.MethodIndex);

                        // Return the register that it is stored to.
                        // But why would I return anything when the stack is empty after stloc??
                        //return storeInst.Reg1;
                    }
                    if (inst.OpCode.Code.ToString().StartsWith("Conv_"))
                    {
                        var convInst = new ConvertNumberInstruction(state, inst);
                        state.InstructionBuilder.Add(convInst, inst, state.MethodIndex);
                    }
                    // TODO: Combine into one math instruction, or use some reflection hacking to get the type name.
                    if (inst.OpCode.Code == CilCode.Neg)
                    {
                        var negInst = new NegInstruction(state, inst);
                        state.InstructionBuilder.Add(negInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Not)
                    {
                        var negInst = new NotInstruction(state, inst);
                        state.InstructionBuilder.Add(negInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Add || inst.OpCode.Code == CilCode.Add_Ovf || inst.OpCode.Code == CilCode.Add_Ovf_Un)
                    {
                        var addInst = new AddInstruction(state, inst);
                        state.InstructionBuilder.Add(addInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Sub || inst.OpCode.Code == CilCode.Sub_Ovf || inst.OpCode.Code == CilCode.Sub_Ovf_Un)
                    {
                        var subInst = new SubInstruction(state, inst);
                        state.InstructionBuilder.Add(subInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Mul || inst.OpCode.Code == CilCode.Mul_Ovf || inst.OpCode.Code == CilCode.Mul_Ovf_Un)
                    {
                        var mulInst = new MulInstruction(state, inst);
                        state.InstructionBuilder.Add(mulInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Div || inst.OpCode.Code == CilCode.Div_Un)
                    {
                        var divInst = new DivInstruction(state, inst);
                        state.InstructionBuilder.Add(divInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.And)
                    {
                        var andInst = new AndInstruction(state, inst);
                        state.InstructionBuilder.Add(andInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Or)
                    {
                        var orInst = new OrInstruction(state, inst);
                        state.InstructionBuilder.Add(orInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Xor)
                    {
                        var xorInst = new XorInstruction(state, inst);
                        state.InstructionBuilder.Add(xorInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Dup)
                    {
                        var dupInst = new DuplicateInstruction(state, inst);
                        state.InstructionBuilder.Add(dupInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        var retInst = new ReturnInstruction(state, state.CurrentSignature.ReturnsValue);
                        state.InstructionBuilder.Add(retInst, inst, state.MethodIndex);
                        // Lol, actually return something?
                        return retInst.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ceq)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsEqual);
                        state.InstructionBuilder.Add(ceqInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Clt)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsLessThan);
                        state.InstructionBuilder.Add(ceqInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Cgt)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThan);
                        state.InstructionBuilder.Add(ceqInst, inst, state.MethodIndex);
                    }
                    if (inst.IsBranch() && inst.OpCode.Code != CilCode.Switch)
                    {
                        // Calculate instruction offset based on instruction builder's total instruction count???
                        // Add offset for it.
                        var cilLabel = (CilInstructionLabel)inst.Operand!;
                        var instTarget = cilLabel.Instruction!;
                        var indexOfInstruction = state.CurrentInstructions.IndexOf(instTarget);
                        var tries = 0;
                        while (!state.InstructionBuilder.IsValidOpCode(instTarget.OpCode.Code) && tries++ < 5)
                        {
                            instTarget = state.CurrentInstructions[++indexOfInstruction];
                        }
                        if (tries >= 5)
                        {
                            throw new Exception("Cannot process branch target. No target found.");
                        }
                        
                        // TODO: Check Beq/Ble/Blt/Bge/bgt/Bne...
                        // Release compiles will use it.

                        if (inst.IsUnconditionalBranch())
                        {
                            throw new Exception("Unconditional branch with stack values??");
                        }
                        
                        switch (inst.OpCode.Code)
                        {
                            case CilCode.Beq:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsEqual), inst, state.MethodIndex);
                                break;
                            case CilCode.Bne_Un:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsNotEqualUnsignedUnordered), inst, state.MethodIndex);
                                break;
                            case CilCode.Bge:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThanOrEqual), inst, state.MethodIndex);
                                break;
                            case CilCode.Bgt:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThan), inst, state.MethodIndex);
                                break;
                            case CilCode.Bgt_Un:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThanUnsignedUnordered), inst, state.MethodIndex);
                                break;
                            case CilCode.Bge_Un:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThanOrEqualUnsignedUnordered), inst, state.MethodIndex);
                                break;

                            case CilCode.Ble:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsLessThanOrEqual), inst, state.MethodIndex);
                                break;
                            case CilCode.Blt:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsLessThan), inst, state.MethodIndex);
                                break;
                            case CilCode.Blt_Un:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsLessThanUnsignedUnordered), inst, state.MethodIndex);
                                break;
                            case CilCode.Ble_Un:
                                state.InstructionBuilder.Add(new ComparatorInstruction(state, inst, ComparatorType.IsLessThanOrEqualUnsignedUnordered), inst, state.MethodIndex);
                                break;
                        }


                        int position = state.InstructionBuilder.InstructionToIndex(instTarget, state.MethodIndex);

                        // Always add as used mapping.
                        if (inst.OpCode.Code != CilCode.Leave)
                        {
                            var handlersForThis = state.CurrentExceptionHandlers.GetProtectedRegionForInstruction(state.ExceptionHandlers, instTarget);
                            var isInSame = state.CurrentExceptionHandlers.IsInSameProtectedRegion(state.ExceptionHandlers, inst, instTarget);
                            if (!isInSame)
                            {
                                var closest = handlersForThis.FindClosest(instTarget);
                                // Just use closest.
                                position = state.InstructionBuilder.InstructionToIndex(closest.Item2.PlaceholderStartInstruction, state.MethodIndex);
                            }
                            else
                            {
                                // No need to compute, in same handler.
                            }
                        }
                        if (position < 0)
                        {
                            throw new Exception("Position cannot be below zero.");
                        }
                        state.InstructionBuilder.AddUsedMapping(position, state.MethodIndex);

                        var brInst = new JumpBoolInstruction(state, inst, position);
                        state.InstructionBuilder.Add(brInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Switch)
                    {
                        var switchLabels = (List<ICilLabel>)inst.Operand!;
                        var offsets = new List<int>();
                        foreach (var sw in switchLabels.Cast<CilInstructionLabel>())
                        {
                            // Add each offset position.
                            var instTarget = sw.Instruction!;
                            var indexOfInstruction = state.CurrentInstructions.IndexOf(instTarget);
                            var tries = 0;
                            while (!state.InstructionBuilder.IsValidOpCode(instTarget.OpCode.Code) && tries++ < 5)
                            {
                                instTarget = state.CurrentInstructions[++indexOfInstruction];
                            }
                            if (tries >= 5)
                            {
                                throw new Exception("Cannot process branch target. No target found.");
                            }
                            int position = state.InstructionBuilder.InstructionToIndex(instTarget, state.MethodIndex);
                            if (position < 0)
                            {
                                throw new Exception("Position cannot be below zero.");
                            }
                            state.InstructionBuilder.AddUsedMapping(position, state.MethodIndex);
                            offsets.Add(position);
                        }
                        var switchInst = new JumpBoolInstruction(state, inst, offsets.ToArray());
                        state.InstructionBuilder.Add(switchInst, inst, state.MethodIndex);
                    }
                    if ((inst.OpCode.Code == CilCode.Call ||
                        inst.OpCode.Code == CilCode.Callvirt || 
                        inst.OpCode.Code == CilCode.Newobj) && inst.Operand is IMethodDefOrRef)
                    {
                        var methodRef = (IMethodDefOrRef)inst.Operand;

                        var canInline = state.ViableInlineTargets.Contains(methodRef);
                        int methodIndexToCall = -1;
                        if (!canInline)
                        {
                            methodIndexToCall = methodRef.MetadataToken.ToInt32();
                        }
                        else
                        {
                            // We find the method index to call based on the processing "line" of the methods to inline.
                            methodIndexToCall = state.ViableInlineTargets.IndexOf(methodRef);
                            // Make sure the first instruction of every method index is added as a used mapping.
                            // At runtime, the call to the method index can then happen based on the instruction mapping.
                            state.InstructionBuilder.AddUsedMapping(0, methodIndexToCall);
                        }

                        // We make a new jump call and specify the method index to call.
                        var jumpCallInst = new JumpCallInstruction(state, inst, methodRef, methodIndexToCall, canInline);
                        state.InstructionBuilder.Add(jumpCallInst, inst, state.MethodIndex);
                    }
                    return null!;
                }
                else
                {
                    var inst = expression.Instruction;
                    VMRegister reg = null!;
                    if (inst.IsLdcI4())
                    {
                        var numLoad = new ConstantLoadInstruction(state, (int)inst.Operand!, DataType.Int32, inst);
                        state.InstructionBuilder.Add(numLoad, inst, state.MethodIndex);

                        // Return the caller the temp reg for loading num. Used above with add/sub/whatever.
                        reg = numLoad.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_I8)
                    {
                        var numLoad = new ConstantLoadInstruction(state, (long)inst.Operand!, DataType.Int64, inst);
                        state.InstructionBuilder.Add(numLoad, inst, state.MethodIndex);

                        // Return the caller the temp reg for loading num. Used above with add/sub/whatever.
                        reg = numLoad.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_R4)
                    {
                        var numLoad = new ConstantLoadInstruction(state, (float)inst.Operand!, DataType.Single, inst);
                        state.InstructionBuilder.Add(numLoad, inst, state.MethodIndex);

                        // Return the caller the temp reg for loading num. Used above with add/sub/whatever.
                        reg = numLoad.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ldc_R8)
                    {
                        var numLoad = new ConstantLoadInstruction(state, (double)inst.Operand!, DataType.Double, inst);
                        state.InstructionBuilder.Add(numLoad, inst, state.MethodIndex);

                        // Return the caller the temp reg for loading num. Used above with add/sub/whatever.
                        reg = numLoad.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ldstr)
                    {
                        var constLoad = new ConstantLoadInstruction(state, (string)inst.Operand!, DataType.String, inst);
                        state.InstructionBuilder.Add(constLoad, inst, state.MethodIndex);
                        reg = constLoad.TempReg1;
                    }
                    if (inst.IsLdloc())
                    {
                        var loadInst = new LocalLoadInstruction(state, inst, (CilLocalVariable)inst.Operand!);
                        state.InstructionBuilder.Add(loadInst, inst, state.MethodIndex);
                        reg = loadInst.TempReg1;
                    }
                    if (inst.IsLdarg())
                    {
                        // If this fails we're pretty fucked.
                        var param = (Parameter)inst.Operand!;
                        DataType dataType = param.ParameterType.ToTypeDefOrRef().ToVMDataType();
                        var ldargInst = new ParamLoadInstruction(state, param.MethodSignatureIndex, dataType, param, inst);
                        state.InstructionBuilder.Add(ldargInst, inst, state.MethodIndex);
                        
                        // Load the tempreg where the param is existing.
                        reg = ldargInst.TempReg1;

                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        var retInst = new ReturnInstruction(state, state.CurrentSignature.ReturnsValue);
                        state.InstructionBuilder.Add(retInst, inst, state.MethodIndex);
                    }
                    if (inst.OpCode.Code == CilCode.Dup)
                    {
                        var dupInst = new DuplicateInstruction(state, inst);
                        state.InstructionBuilder.Add(dupInst, inst, state.MethodIndex);
                    }
                    // TODO: Endfilter at some point?
                    if (inst.OpCode.Code == CilCode.Endfinally)
                    {
                        var endFinallyInst = new EndFinallyInstruction(state);
                        state.InstructionBuilder.Add(endFinallyInst, inst, state.MethodIndex);
                    }
                    if (inst.IsBranch() && inst.OpCode.Code != CilCode.Switch)
                    {
                        var cilLabel = (CilInstructionLabel)inst.Operand!;
                        var instTarget = cilLabel.Instruction!;
                        var indexOfInstruction = state.CurrentInstructions.IndexOf(instTarget);
                        var tries = 0;
                        while (!state.InstructionBuilder.IsValidOpCode(instTarget.OpCode.Code) && tries++ < 5)
                        {
                            instTarget = state.CurrentInstructions[++indexOfInstruction];
                        }
                        if (tries >= 5)
                        {
                            throw new Exception("Cannot process branch target. No target found.");
                        }

                        if (inst.IsUnconditionalBranch())
                        {
                            var numLoadInst = new ConstantLoadInstruction(state, true, DataType.Boolean, inst);
                            state.InstructionBuilder.Add(numLoadInst, inst, state.MethodIndex);
                        }
                        else if (inst.IsConditionalBranch())
                        {
                            // Technically there should be something already on the stack??
                        }

                        int position = state.InstructionBuilder.InstructionToIndex(instTarget, state.MethodIndex);

                        // Always add as used mapping.
                        if (inst.OpCode.Code != CilCode.Leave)
                        {
                            var handlersForThis = state.CurrentExceptionHandlers.GetProtectedRegionForInstruction(state.ExceptionHandlers, instTarget);
                            var isInSame = state.CurrentExceptionHandlers.IsInSameProtectedRegion(state.ExceptionHandlers, inst, instTarget);
                            if (!isInSame)
                            {
                                var closest = handlersForThis.FindClosest(instTarget);
                                // Just use closest.
                                position = state.InstructionBuilder.InstructionToIndex(closest.Item2.PlaceholderStartInstruction, state.MethodIndex);
                            }
                            else
                            {
                                // Same area, no need to compute.
                            }

                        }
                        if (position < 0)
                        {
                            throw new Exception("Position cannot be below zero.");
                        }
                        state.InstructionBuilder.AddUsedMapping(position, state.MethodIndex);

                        var brInst = new JumpBoolInstruction(state, inst, position);
                        state.InstructionBuilder.Add(brInst, inst, state.MethodIndex);

                        // There is no register for this operation, leave it null.
                        reg = null!;
                    }

                    // Cannot have Callvirt without arguments...
                    if ((inst.OpCode.Code == CilCode.Call ||
                        inst.OpCode.Code == CilCode.Newobj) && inst.Operand is IMethodDefOrRef)
                    {
                        var methodRef = (IMethodDefOrRef)inst.Operand;

                        var canInline = state.ViableInlineTargets.Contains(methodRef);
                        int methodIndexToCall = -1;
                        if (!canInline)
                        {
                            methodIndexToCall = methodRef.MetadataToken.ToInt32();
                        }
                        else
                        {
                            // We find the method index to call based on the processing "line" of the methods to inline.
                            methodIndexToCall = state.ViableInlineTargets.IndexOf(methodRef);
                            // Make sure the first instruction of every method index is added as a used mapping.
                            // At runtime, the call to the method index can then happen based on the instruction mapping.
                            state.InstructionBuilder.AddUsedMapping(0, methodIndexToCall);
                        }

                        // We make a new jump call and specify the method index to call.
                        var jumpCallInst = new JumpCallInstruction(state, inst, methodRef, methodIndexToCall, canInline);
                        state.InstructionBuilder.Add(jumpCallInst, inst, state.MethodIndex);
                    }
                    return reg;
                }
                
            }

        }
    }
}
