using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using System.ComponentModel.DataAnnotations;

namespace RegiVM.VMBuilder.Instructions
{
    public class ParamLoadInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public int ParamOffset { get; }
        public VMRegister Reg1 { get; }
        public override byte[] ByteCode { get; }

        public ParamLoadInstruction(VMCompiler compiler, int paramOffset, DataType paramDataType, Parameter param)
        {
            ParamOffset = paramOffset;
            
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.ParameterLoad;
            Reg1 = Registers.GetFree();

            Reg1.Param = param;
            Reg1.LastOffsetUsed = 0;
            Reg1.OriginalOffset = 0;
            Reg1.DataType = paramDataType;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"PARAMETER {Reg1.DataType} {Reg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {

                writer.Write(ParamOffset);
                writer.Write((byte)Reg1.DataType);
                writer.Write(Reg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
