using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;

namespace RegiVM.VMBuilder.Instructions
{
    public class ParamLoadInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public int ParamOffset { get; }
        public VMRegister TempReg1 { get; }
        public override byte[] ByteCode { get; set; }

        public ParamLoadInstruction(VMCompiler compiler, int paramOffset, DataType paramDataType, Parameter param, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            ParamOffset = paramOffset;
            
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.ParameterLoad;
            TempReg1 = Registers.ForTemp();
            TempReg1.Param = param;
            TempReg1.LastOffsetUsed = inst.Offset;
            TempReg1.OriginalOffset = inst.Offset;
            TempReg1.DataType = paramDataType;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"PARAMETER {TempReg1.DataType} {TempReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {

                writer.Write(ParamOffset);
                writer.Write((byte)TempReg1.DataType);
                writer.Write(TempReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
