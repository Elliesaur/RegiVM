using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder
{
    public partial class VMCompiler
    {
        public class VMNodeVisitorDryPass : IAstNodeVisitor<CilInstruction, VMCompiler>
        {
            public void Visit(CompilationUnit<CilInstruction> unit, VMCompiler state)
            {
                unit.Root.Accept(this, state);
            }

            public void Visit(AssignmentStatement<CilInstruction> statement, VMCompiler state)
            {
            }

            public void Visit(ExpressionStatement<CilInstruction> statement, VMCompiler state)
            {
                statement.Expression.Accept(this, state);
            }

            public void Visit(PhiStatement<CilInstruction> statement, VMCompiler state)
            {
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
            }

            public void Visit(HandlerClause<CilInstruction> clause, VMCompiler state)
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
                        state.InstructionBuilder.AddDryPass(state.OpCodes.LocalLoadStore, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Add)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Add, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Sub)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Sub, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Mul)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Mul, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Div)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Div, inst);
                    }
                    if (inst.OpCode.Code == CilCode.And)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.And, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Or)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Or, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Xor)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Xor, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Ret, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Ceq
                        || inst.OpCode.Code == CilCode.Cgt
                        || inst.OpCode.Code == CilCode.Clt)
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.Comparator, inst);
                    }
                    if (inst.IsBranch())
                    {
                        if (inst.IsUnconditionalBranch())
                        {
                            throw new Exception("Unconditional branch with stack values??");
                        }
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpBool, inst);
                    }

                }
                else
                {
                    var inst = expression.Instruction;
                    if (inst.IsLdcI4())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Ldstr)
                    {
                        //state.InstructionBuilder.AddDryPass(state.OpCodes.StringLoad, inst);
                    }
                    if (inst.IsLdloc())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.LocalLoadStore, inst);
                    }
                    if (inst.IsLdarg())
                    {
                        state.InstructionBuilder.AddDryPass(state.OpCodes.ParameterLoad, inst);
                    }
                    if (inst.IsUnconditionalBranch())
                    {
                        // Load num.
                        state.InstructionBuilder.AddDryPass(state.OpCodes.NumberLoad, inst);
                        // Jump.
                        state.InstructionBuilder.AddDryPass(state.OpCodes.JumpBool, inst);
                    }
                }

            }
            public void Visit(VariableExpression<CilInstruction> expression, VMCompiler state)
            {
            }
        }

        public class VMNodeVisitor : IAstNodeVisitor<CilInstruction, VMCompiler, VMRegister>
        {
            public VMRegister Visit(CompilationUnit<CilInstruction> unit, VMCompiler state)
            {
                return unit.Root.Accept(this, state);
                //unit.Accept(this, state);
            }

            public VMRegister Visit(AssignmentStatement<CilInstruction> statement, VMCompiler state)
            {
                //statement.Accept(this, state);
                return null!;
            }

            public VMRegister Visit(ExpressionStatement<CilInstruction> statement, VMCompiler state)
            {
                return statement.Expression.Accept(this, state);
            }

            public VMRegister Visit(PhiStatement<CilInstruction> statement, VMCompiler state)
            {
                return null!;
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
                return null!;
            }

            public VMRegister Visit(HandlerClause<CilInstruction> clause, VMCompiler state)
            {
                return null!;
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
                        state.InstructionBuilder.Add(storeInst, inst);

                        // Return the register that it is stored to.
                        // But why would I return anything when the stack is empty after stloc??
                        //return storeInst.Reg1;
                    }
                    // TODO: Combine into one math instruction, or use some reflection hacking to get the type name.
                    if (inst.OpCode.Code == CilCode.Add)
                    {
                        var addInst = new AddInstruction(state, inst);
                        state.InstructionBuilder.Add(addInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Sub)
                    {
                        var subInst = new SubInstruction(state, inst);
                        state.InstructionBuilder.Add(subInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Mul)
                    {
                        var mulInst = new MulInstruction(state, inst);
                        state.InstructionBuilder.Add(mulInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Div)
                    {
                        var divInst = new DivInstruction(state, inst);
                        state.InstructionBuilder.Add(divInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.And)
                    {
                        var andInst = new AndInstruction(state, inst);
                        state.InstructionBuilder.Add(andInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Or)
                    {
                        var orInst = new OrInstruction(state, inst);
                        state.InstructionBuilder.Add(orInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Xor)
                    {
                        var xorInst = new XorInstruction(state, inst);
                        state.InstructionBuilder.Add(xorInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        var retInst = new ReturnInstruction(state, state.CurrentMethod.Signature!.ReturnsValue);
                        state.InstructionBuilder.Add(retInst, inst);
                        // Lol, actually return something?
                        return retInst.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ceq)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsEqual);
                        state.InstructionBuilder.Add(ceqInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Clt)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsLessThan);
                        state.InstructionBuilder.Add(ceqInst, inst);
                    }
                    if (inst.OpCode.Code == CilCode.Cgt)
                    {
                        var ceqInst = new ComparatorInstruction(state, inst, ComparatorType.IsGreaterThan);
                        state.InstructionBuilder.Add(ceqInst, inst);
                    }
                    if (inst.IsBranch())
                    {
                        // Calculate instruction offset based on instruction builder's total instruction count???
                        // Add offset for it.
                        var cilLabel = (CilInstructionLabel)inst.Operand!;
                        var instTarget = cilLabel.Instruction!;
                        var indexOfInstruction = state.CurrentMethod.CilMethodBody!.Instructions.IndexOf(instTarget);
                        var tries = 0;
                        while (!state.InstructionBuilder.IsValidOpCode(instTarget.OpCode.Code) && tries++ < 5)
                        {
                            instTarget = state.CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                        }
                        if (tries >= 5)
                        {
                            throw new Exception("Cannot process branch target. No target found.");
                        }

                        if (inst.IsUnconditionalBranch())
                        {
                            throw new Exception("Unconditional branch with stack values??");
                        }
                        else if (inst.IsConditionalBranch())
                        {
                            // Technically there should be something already on the stack??

                        }

                        int position = state.InstructionBuilder.InstructionToOffset(instTarget);
                        var brInst = new JumpBoolInstruction(state, position, inst);
                        state.InstructionBuilder.Add(brInst, inst);

                        // There is no register for this operation, leave it null.
                    }

                    return null!;
                }
                else
                {
                    var inst = expression.Instruction;
                    VMRegister reg = null!;
                    if (inst.IsLdcI4())
                    {
                        var numLoad = new NumLoadInstruction(state, (int)inst.Operand!, DataType.Int32, inst);
                        state.InstructionBuilder.Add(numLoad, inst);

                        // Return the caller the temp reg for loading num. Used above with add/sub/whatever.
                        reg = numLoad.TempReg1;
                    }
                    if (inst.OpCode.Code == CilCode.Ldstr)
                    {
                        // TODO: Ldstr support.
                        // Shouldn't be too hard.
                    }
                    if (inst.IsLdloc())
                    {
                        var loadInst = new LocalLoadInstruction(state, inst, (CilLocalVariable)inst.Operand!);
                        state.InstructionBuilder.Add(loadInst, inst);
                        reg = loadInst.TempReg1;
                    }
                    if (inst.IsLdarg())
                    {
                        // If this fails we're pretty fucked.
                        var param = (Parameter)inst.Operand!;
                        var typeName = param.ParameterType.ToTypeDefOrRef().Name;
                        if (!Enum.TryParse(typeof(DataType), typeName, true, out var dataType))
                        {
                            throw new Exception($"CANNOT PROCESS TYPE NAME FOR PARAMETER! {typeName}");
                        }
                        var ldargInst = new ParamLoadInstruction(state, param.Index, (DataType)dataType, param, inst);
                        state.InstructionBuilder.Add(ldargInst, inst);
                        
                        // Load the tempreg where the param is existing.
                        reg = ldargInst.TempReg1;

                    }
                    if (inst.IsBranch())
                    {
                        var cilLabel = (CilInstructionLabel)inst.Operand!;
                        var instTarget = cilLabel.Instruction!;
                        var indexOfInstruction = state.CurrentMethod.CilMethodBody!.Instructions.IndexOf(instTarget);
                        var tries = 0;
                        while (!state.InstructionBuilder.IsValidOpCode(instTarget.OpCode.Code) && tries++ < 5)
                        {
                            instTarget = state.CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                        }
                        if (tries >= 5)
                        {
                            throw new Exception("Cannot process branch target. No target found.");
                        }

                        if (inst.IsUnconditionalBranch())
                        {
                            var numLoadInst = new NumLoadInstruction(state, true, DataType.Boolean, inst);
                            state.InstructionBuilder.Add(numLoadInst, inst);
                        }
                        else if (inst.IsConditionalBranch())
                        {
                            // Technically there should be something already on the stack??
                        }
                        
                        int position = state.InstructionBuilder.InstructionToOffset(instTarget);
                        var brInst = new JumpBoolInstruction(state, position, inst);
                        state.InstructionBuilder.Add(brInst, inst);

                        // There is no register for this operation, leave it null.
                        reg = null!;
                    }
                    // TODO: Null check? But nah... caller does it.
                    return reg;
                }
                
            }

            public VMRegister Visit(VariableExpression<CilInstruction> expression, VMCompiler state)
            {
                return null!;
                //expression.Accept(this, state);
            }
        }
    }
}
