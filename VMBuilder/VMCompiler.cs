using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using Echo.Ast.Analysis;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.DataFlow;
using Echo.DataFlow.Construction;
using Echo.Platforms.AsmResolver;
using Microsoft.Win32;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMBuilder.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder
{
    public enum DataType : byte
    {
        Unknown = 0,

        Int32 = 0x1,
        UInt32 = 0x2,

        Int64 = 0x3,
        UInt64 = 0x4,

        Single = 0x5,
        Double = 0x6,

        Int8 = 0x7,
        UInt8 = 0x8,

        Int16 = 0x9,
        UInt16 = 0x10,

        String = 0x11
    }

    public class VMCompiler
    {
        public RegisterHelper RegisterHelper { get; private set; }
        
        public InstructionBuilder InstructionBuilder { get; }

        public MethodDefinition CurrentMethod { get; private set; }

        public VMOpCode OpCodes { get; }

        public int PreviousDepth { get; set; }
        public int ProcessedDepth { get; set; }
        public int Pop { get; set; }
        public int Push { get; set; }

        public VMCompiler()
        {
            RegisterHelper = null;
            InstructionBuilder = new InstructionBuilder();
            OpCodes = new VMOpCode();
        }

        public VMCompiler RandomizeOpCodes()
        {
            OpCodes.RandomizeAll();
            return this;
        }

        public VMCompiler RegisterLimit(int numRegisters)
        {
            if (RegisterHelper == null)
            {
                RegisterHelper = new RegisterHelper(numRegisters);
            }
            return this;
        }

        public VMCompiler RandomizeRegisterNames()
        {
            RegisterHelper.RandomizeRegisterNames();
            return this;
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
                VMRegister reg = null;
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
                        state.InstructionBuilder.Add(storeInst);

                        // Return the register that it is stored to.
                        // But why would I return anything when the stack is empty after stloc??
                        //return storeInst.Reg1;
                    }
                    // TODO: Combine into one math instruction, or use some reflection hacking to get the type name.
                    if (inst.OpCode.Code == CilCode.Add)
                    {
                        var addInst = new AddInstruction(state, inst);
                        state.InstructionBuilder.Add(addInst);
                    }
                    if (inst.OpCode.Code == CilCode.Sub)
                    {
                        var subInst = new SubInstruction(state, inst);
                        state.InstructionBuilder.Add(subInst);
                    }
                    if (inst.OpCode.Code == CilCode.Mul)
                    {
                        var mulInst = new MulInstruction(state, inst);
                        state.InstructionBuilder.Add(mulInst);
                    }
                    if (inst.OpCode.Code == CilCode.Div)
                    {
                        var divInst = new DivInstruction(state, inst);
                        state.InstructionBuilder.Add(divInst);
                    }
                    if (inst.OpCode.Code == CilCode.And)
                    {
                        var andInst = new AndInstruction(state, inst);
                        state.InstructionBuilder.Add(andInst);
                    }
                    if (inst.OpCode.Code == CilCode.Or)
                    {
                        var orInst = new OrInstruction(state, inst);
                        state.InstructionBuilder.Add(orInst);
                    }
                    if (inst.OpCode.Code == CilCode.Xor)
                    {
                        var xorInst = new XorInstruction(state, inst);
                        state.InstructionBuilder.Add(xorInst);
                    }
                    if (inst.OpCode.Code == CilCode.Ret)
                    {
                        var retInst = new ReturnInstruction(state, state.CurrentMethod.Signature!.ReturnsValue);
                        state.InstructionBuilder.Add(retInst);
                        // Lol, actually return something?
                        return retInst.TempReg1;
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
                        state.InstructionBuilder.Add(numLoad);

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
                        state.InstructionBuilder.Add(loadInst);
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
                        state.InstructionBuilder.Add(ldargInst);
                        
                        // Load the tempreg where the param is existing.
                        reg = ldargInst.TempReg1;

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

        public byte[] Compile(MethodDefinition method)
        {
            // TODO: Sort out concurrency issues.
            // Pass as param for AST visitor?
            CurrentMethod = method;

            var sfg = method.CilMethodBody!.ConstructSymbolicFlowGraph(out var dfg);
            var blocks = BlockBuilder.ConstructBlocks(sfg);
            var astCompUnit = sfg.ToCompilationUnit(new CilPurityClassifier());

            method.CilMethodBody!.Instructions.ExpandMacros();

            var visitor = new VMNodeVisitor();
            visitor.Visit(astCompUnit, this);

            //var walker = new VMAstWalker() { Compiler = this };
            //AstNodeWalker<CilInstruction>.Walk(walker, astCompUnit);

            method.CilMethodBody!.Instructions.OptimizeMacros();

            return InstructionBuilder.ToByteArray(true);
        }
    }
}
