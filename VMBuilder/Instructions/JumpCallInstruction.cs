using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
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
        public List<VMRegister> ArgRegs { get; }
        public VMRegister ReturnReg1 { get; }
        public override byte[] ByteCode { get; }
        public int MethodIndexToCall { get; }
        public bool IsInlineCall { get; }

        public JumpCallInstruction(VMCompiler compiler, CilInstruction inst, IMethodDefOrRef target, int methodIndexToCall, bool isInlineCall)
        {
            Registers = compiler.RegisterHelper;
            ArgRegs = new List<VMRegister>();
            OpCode = compiler.OpCodes.JumpCall;
            MethodIndexToCall = methodIndexToCall;
            IsInlineCall = isInlineCall;

            var paramCount = target.Signature!.GetTotalParameterCount();
            for (int i = 0; i < paramCount; i++)
            {
                ArgRegs.Add((Registers.Temporary.Pop()));
            }
            // Reverse order for proper assignments.
            ArgRegs.Reverse();

            if (target.Signature!.ReturnsValue)
            {
                ReturnReg1 = (Registers.ForTemp());
                var typeName = target.Signature!.ReturnType.ToTypeDefOrRef().Name;
                if (!Enum.TryParse(typeof(DataType), typeName, true, out var dataType))
                {
                    throw new Exception($"CANNOT PROCESS TYPE NAME FOR CALL! {typeName}");
                }
                ReturnReg1.DataType = (DataType)dataType;
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
                    writer.Write((byte)arg.DataType);
                    writer.Write(arg.RawName.Length);
                    writer.Write(arg.RawName);
                }
                if (ReturnReg1 != null)
                {
                    writer.Write((byte)ReturnReg1.DataType);
                    writer.Write(ReturnReg1.RawName.Length);
                    writer.Write(ReturnReg1.RawName);
                }
                return memStream.ToArray();
            }
        }
    }
}
