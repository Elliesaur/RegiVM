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
        UInt16 = 0x10
    }

    public class VMCompiler
    {
        public RegisterHelper RegisterHelper { get; private set; }
        
        public InstructionBuilder InstructionBuilder { get; }

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

        public byte[] Compile(MethodDefinition method)
        {

            var sfg = method.CilMethodBody!.ConstructSymbolicFlowGraph(out var dfg);
            var blocks = BlockBuilder.ConstructBlocks(sfg);
            var astCompUnit = sfg.ToCompilationUnit(new CilPurityClassifier());

            method.CilMethodBody!.Instructions.ExpandMacros();
            
            var walker = new VMAstWalker();
            //AstNodeWalker<CilInstruction>.Walk(walker, astCompUnit);

            var b = blocks.GetAllBlocks().ToList();
            
            for (int i = 0; i < b.Count; i++)
            {
                ProcessBlock(b, b[i], i, sfg, astCompUnit, method);
            }


            //// We offset the depth by the number of parameters, this way we keep registers separate.
            //ProcessedDepth = 0;


            //for (int instIndex = 0; instIndex < method.CilMethodBody!.Instructions.Count; instIndex++)
            //{
            //    CilInstruction inst = method.CilMethodBody!.Instructions[instIndex];
            //    CilInstruction? prevInst = instIndex > 0 ? method.CilMethodBody!.Instructions[instIndex - 1] : null;
            //    CilInstruction? nextInst = instIndex + 1 < method.CilMethodBody!.Instructions.Count ? method.CilMethodBody!.Instructions[instIndex + 1] : null;

            //    int push = inst.GetStackPushCount();
            //    int pop = inst.GetStackPopCount(method.CilMethodBody!);
            //    Push = push;
            //    Pop = pop;
            //    PreviousDepth = ProcessedDepth;
            //    ProcessedDepth += push - pop;
            //    if (inst.IsLdloc())
            //    {
            //        var localVar = inst.Operand as CilLocalVariable;
            //        if (localVar != null)
            //        {
            //            var reg = RegisterHelper.Registers.FirstOrDefault(x => x.LocalVar == localVar);
            //            if (reg != null)
            //            {
            //                // If we simply update the last offset used to the current offset, hopefully when we later add it magically... works?
            //                reg.LastOffsetUsed = inst.Offset;

            //                Console.WriteLine($"-> Prev SP: {reg.StackPosition}, Update to {PreviousDepth}");
            //                reg.StackPosition = PreviousDepth;

            //                Console.WriteLine($"  -> Update SP, LOU for {reg}");
            //            }
            //        }
            //    }

            //    if (inst.IsLdarg())
            //    {
            //        // Arguments need to be loaded into registries.
            //        var param = inst.Operand as Parameter;
            //        if (param != null)
            //        {
            //            var typeName = param.ParameterType.ToTypeDefOrRef().Name;
            //            if (!Enum.TryParse(typeof(DataType), typeName, true, out var dataType))
            //            {
            //                throw new Exception($"CANNOT PROCESS TYPE NAME FOR PARAMETER! {typeName}");
            //            }

            //            var paramLoad = new ParamLoadInstruction(this, paramCount++, (DataType)dataType, param);
            //            InstructionBuilder.Add(paramLoad);
            //        }
            //    }

            //    if (inst.IsLdcI4() && nextInst != null)
            //    {
            //        var valueToLoad = (int)inst.Operand!;

            //        CilLocalVariable? localVar = null;
            //        if (nextInst!.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var numType = DataType.Int32;

            //        var numLoad = new NumLoadInstruction(this, valueToLoad, numType, inst, localVar);
            //        InstructionBuilder.Add(numLoad);

            //        processed++;
            //    }

            //    if (inst.OpCode.Code == CilCode.Add)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new AddInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Sub)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new SubInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Mul)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new MulInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Div)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new DivInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Xor)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new XorInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Or)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new OrInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);

            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.And)
            //    {
            //        // Check if next instruction is storing, and if so, track the variable.
            //        CilLocalVariable? localVar = null;
            //        if (nextInst != null && nextInst.IsStloc())
            //        {
            //            localVar = (CilLocalVariable?)nextInst.Operand;
            //        }

            //        var add = new AndInstruction(this, inst, localVar);
            //        InstructionBuilder.Add(add);
            //        processed++;
            //    }
            //    if (inst.OpCode.Code == CilCode.Ret)
            //    {
            //        var ret = new ReturnInstruction(this, method.Signature!.ReturnsValue);
            //        InstructionBuilder.Add(ret);
            //        processed++;
            //    }
            //}

            method.CilMethodBody!.Instructions.OptimizeMacros();

            return InstructionBuilder.ToByteArray(true);
        }

        private void ProcessBlock(List<BasicBlock<CilInstruction>> basicBlocks, BasicBlock<CilInstruction> block, int blockIndex, ControlFlowGraph<CilInstruction> sfg, CompilationUnit<CilInstruction> ast, MethodDefinition method)
        {
            var sfgNode = sfg.Nodes.GetByOffset(block.Offset);

            for (int instIndex = 0; instIndex < block.Instructions.Count; instIndex++)
            {
                CilInstruction inst = block.Instructions[instIndex];
                CilInstruction? prevInst = instIndex > 0 ? method.CilMethodBody!.Instructions[instIndex - 1] : null;
                CilInstruction? nextInst = instIndex + 1 < method.CilMethodBody!.Instructions.Count ? method.CilMethodBody!.Instructions[instIndex + 1] : null;

                int push = inst.GetStackPushCount();
                int pop = inst.GetStackPopCount(method.CilMethodBody!);

                Push = push;
                Pop = pop;
                PreviousDepth = ProcessedDepth;
                ProcessedDepth += push - pop;
                if (inst.IsLdcI4())
                {
                    var load = new NumLoadInstruction(this, inst.Operand!, DataType.Int32, inst);
                    InstructionBuilder.Add(load);
                }

                if (inst.OpCode.Code == CilCode.Add)
                {
                    var add = new AddInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }
                if (inst.OpCode.Code == CilCode.Sub)
                {
                    var add = new SubInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }
                if (inst.OpCode.Code == CilCode.Mul)
                {
                    var add = new MulInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }
                if (inst.OpCode.Code == CilCode.Div)
                {
                    var add = new DivInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }
                if (inst.OpCode.Code == CilCode.And)
                {
                    var add = new AndInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }
                if (inst.OpCode.Code == CilCode.Xor)
                {
                    var add = new XorInstruction(this, inst);
                    InstructionBuilder.Add(add);
                }

                if (inst.IsStloc())
                {
                    var cilLocal = (CilLocalVariable)inst.Operand!;
                    var store = new LocalStoreInstruction(this, inst, cilLocal);
                    InstructionBuilder.Add(store);
                }

                if (inst.IsLdloc())
                {
                    var cilLocal = (CilLocalVariable)inst.Operand!;
                    var load = new LocalLoadInstruction(this, inst, cilLocal);
                    InstructionBuilder.Add(load);
                }

                if (inst.IsLdarg())
                {
                    var param = (Parameter)inst.Operand!;
                    var typeName = param.ParameterType.ToTypeDefOrRef().Name;
                    if (!Enum.TryParse(typeof(DataType), typeName, true, out var dataType))
                    {
                        throw new Exception($"CANNOT PROCESS TYPE NAME FOR PARAMETER! {typeName}");
                    }
                    var p = new ParamLoadInstruction(this, param.Index, (DataType)dataType, param, inst);
                    InstructionBuilder.Add(p);
                }

                if (inst.OpCode.Code == CilCode.Ret)
                {
                    var ret = new ReturnInstruction(this, method.Signature!.ReturnsValue);
                    InstructionBuilder.Add(ret);
                }
                //if (inst.IsLdloc())
                //{
                //    var localVar = inst.Operand as CilLocalVariable;
                //    if (localVar != null)
                //    {
                //        var reg = RegisterHelper.Registers.FirstOrDefault(x => x.LocalVar == localVar);
                //        if (reg != null)
                //        {
                //            // If we simply update the last offset used to the current offset, hopefully when we later add it magically... works?
                //            reg.LastOffsetUsed = inst.Offset;

                //            Console.WriteLine($"-> Prev SP: {reg.StackPosition}, Update to {PreviousDepth}");
                //            reg.StackPosition = PreviousDepth;

                //            Console.WriteLine($"  -> Update SP, LOU for {reg}");
                //        }
                //    }
                //}



            }
        }
    }
}
