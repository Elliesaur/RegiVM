using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
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

        public int CurrentDepth { get; set; }
        public int ChangedDepth { get; set; }
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
            method.CilMethodBody!.Instructions.ExpandMacros();
            int processed = 0;
            int paramCount = 0;

            foreach (var param in method.Parameters)
            {
                var opCode = 0x3000UL;
                var typeName = param.ParameterType.ToTypeDefOrRef().Name;
                if (!Enum.TryParse(typeof(DataType), typeName, true, out var dataType))
                {
                    throw new Exception($"CANNOT PROCESS TYPE NAME FOR PARAMETER! {typeName}");
                }

                var paramLoad = new ParamLoadInstruction(this, paramCount++, (DataType)dataType, param);
                InstructionBuilder.Add(paramLoad);
            }

            // We offset the depth by the number of parameters, this way we keep registers separate.
            int depth = paramCount - 1;


            for (int instIndex = 0; instIndex < method.CilMethodBody!.Instructions.Count; instIndex++)
            {
                CilInstruction inst = method.CilMethodBody!.Instructions[instIndex];
                CilInstruction? prevInst = instIndex > 0 ? method.CilMethodBody!.Instructions[instIndex - 1] : null;
                CilInstruction? nextInst = instIndex + 1 < method.CilMethodBody!.Instructions.Count ? method.CilMethodBody!.Instructions[instIndex + 1] : null;

                int push = inst.GetStackPushCount();
                int pop = inst.GetStackPopCount(method.CilMethodBody!);

                Push = push;
                Pop = pop;
                CurrentDepth = depth;
                depth += push - pop;
                ChangedDepth = depth;

                if (inst.IsLdloc())
                {
                    var localVar = inst.Operand as CilLocalVariable;
                    if (localVar != null)
                    {
                        var reg = RegisterHelper.Registers.FirstOrDefault(x => x.LocalVar == localVar);
                        if (reg != null)
                        {
                            // If we simply update the last offset used to the current offset, hopefully when we later add it magically... works?
                            reg.LastOffsetUsed = inst.Offset;
                            Console.WriteLine($"-> Update Last Offset Used for {reg}");
                        }
                    }
                }

                if (inst.IsLdarg())
                {
                    // Arguments need to be loaded into registries.
                    var param = inst.Operand as Parameter;
                    if (param != null)
                    {
                        var reg = RegisterHelper.Registers.FirstOrDefault(x => x.Param == param);
                        if (reg != null)
                        {
                            // If we simply update the last offset used to the current offset, hopefully when we later add it magically... works?
                            reg.LastOffsetUsed = inst.Offset;
                            Console.WriteLine($"-> Update Last Offset Used for {reg}");
                        }
                    }
                }

                if (inst.IsLdcI4() && nextInst != null)
                {
                    var valueToLoad = (int)inst.Operand!;

                    CilLocalVariable? localVar = null;
                    if (nextInst!.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var numType = DataType.Int32;

                    var numLoad = new NumLoadInstruction(this, valueToLoad, numType, inst, localVar);
                    InstructionBuilder.Add(numLoad);

                    processed++;
                }

                if (inst.OpCode.Code == CilCode.Add)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new AddInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Sub)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new SubInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Mul)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new MulInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Div)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new DivInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Xor)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new XorInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Or)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new OrInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);

                    processed++;
                }
                if (inst.OpCode.Code == CilCode.And)
                {
                    // Check if next instruction is storing, and if so, track the variable.
                    CilLocalVariable? localVar = null;
                    if (nextInst != null && nextInst.IsStloc())
                    {
                        localVar = (CilLocalVariable?)nextInst.Operand;
                    }

                    var add = new AndInstruction(this, inst, localVar);
                    InstructionBuilder.Add(add);
                    processed++;
                }
                if (inst.OpCode.Code == CilCode.Ret)
                {
                    var ret = new ReturnInstruction(this, method.Signature!.ReturnsValue);
                    InstructionBuilder.Add(ret);
                    processed++;
                }
            }

            method.CilMethodBody!.Instructions.OptimizeMacros();

            return InstructionBuilder.ToByteArray(true);
        }

    }
}
