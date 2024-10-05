using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Instructions
{
    public class JumpCallInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public Dictionary<VMRegister, DataType> ArgRegs { get; }
        public VMRegister ReturnReg1 { get; }
        public override byte[] ByteCode { get; set; }
        public int MethodIndexToCall { get; }
        public bool IsInlineCall { get; }

        public JumpCallInstruction(VMCompiler compiler, CilInstruction inst, IMethodDefOrRef target, int methodIndexToCall, bool isInlineCall)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;
            ArgRegs = new Dictionary<VMRegister, DataType>();
            OpCode = compiler.OpCodes.JumpCall;
            MethodIndexToCall = methodIndexToCall;
            IsInlineCall = isInlineCall;

            // Add reference to the method index.
            AddReference(MethodIndexToCall);
            
            var paramCount = target.Signature!.GetTotalParameterCount();
            if (inst.OpCode.Code == CilCode.Newobj)
            {
                // Remove one parameter for newobj .ctor calls.
                paramCount--;
            }
            var paramTypes = target.Signature!.ParameterTypes.Reverse().ToArray();
            for (int i = 0; i < paramCount; i++)
            {
                var paramIndex = inst.OpCode.Code == CilCode.Newobj ? i + 1 : i;
                var tempArg = Registers.PopTemp();
                if (paramIndex < paramTypes.Length)
                {
                    var paramType = paramTypes[paramIndex];

                    // Is not valid data type?
                    var paramDataType = paramType.ToTypeDefOrRef().ToVMDataType();

                    // If it did not succeed to get a datatype, treat the temp args data type as real.
                    if (paramDataType == DataType.Unknown)
                    {
                        ArgRegs.Add(tempArg, tempArg.DataType);
                    }
                    else if (tempArg.DataType != paramDataType)
                    {
                        ArgRegs.Add(tempArg, paramDataType);
                    }
                    else
                    {
                        ArgRegs.Add(tempArg, tempArg.DataType);
                    }
                }
                else
                {
                    ArgRegs.Add(tempArg, tempArg.DataType);
                }

            }
            // Reverse order for proper assignments.
            ArgRegs = ArgRegs.Reverse().ToDictionary();

            if (inst.OpCode.Code == CilCode.Newobj)
            {
                // Newobj always pushes a value to stack.
                ReturnReg1 = Registers.PushTemp();
                ReturnReg1.DataType = DataType.Unknown;
            }
            else if (target.Signature!.ReturnsValue)
            {
                ReturnReg1 = Registers.PushTemp();
                ReturnReg1.DataType = target.Signature!.ToVMDataType();
            }

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"JUMP_CALL Inline:{IsInlineCall} Indx:{MethodIndexToCall} Args:{ArgRegs.Count} Ret?:{ReturnReg1 != null}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(IsInlineCall);
                writer.Write(MethodIndexToCall);
                // Has return value
                writer.Write(ReturnReg1 != null);
                writer.Write(ArgRegs.Count);
                foreach (var arg in ArgRegs)
                {
                    // Writer registers to use in call.
                    writer.Write((byte)arg.Key.DataType);
                    writer.Write((byte)arg.Value);
                    writer.Write(arg.Key.RawName.Length);
                    writer.Write(arg.Key.RawName);
                }
                if (ReturnReg1 != null)
                {
                    //writer.Write((byte)ReturnReg1.DataType);
                    writer.Write(ReturnReg1.RawName.Length);
                    writer.Write(ReturnReg1.RawName);
                }
                return memStream.ToArray();
            }
        }
    }
}
