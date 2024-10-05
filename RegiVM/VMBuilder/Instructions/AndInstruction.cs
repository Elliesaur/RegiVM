using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class AndInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister Reg1 { get; }
        public VMRegister Reg2 { get; }
        public VMRegister ToPushReg { get; }
        public override byte[] ByteCode { get; set; }

        public AndInstruction(VMCompiler compiler, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;

            // Get last two registers.
            var rawReg2 = Registers.PopTemp();
            var rawReg1 = Registers.PopTemp();

            Reg1 = new VMRegister(rawReg1);
            Reg2 = new VMRegister(rawReg2);

            ToPushReg = Registers.PushTemp();
            ToPushReg.LastOffsetUsed = inst.Offset;
            ToPushReg.OriginalOffset = inst.Offset;
            ToPushReg.DataType = Reg1.DataType;

            Inst = inst;
            OpCode = compiler.OpCodes.And;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"AND {ToPushReg.DataType}={Reg1.DataType}|{Reg2.DataType} {ToPushReg} {Reg1} {Reg2}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)ToPushReg.DataType);
                writer.Write((byte)Reg1.DataType);
                writer.Write((byte)Reg2.DataType);

                writer.Write(ToPushReg.RawName.Length);
                writer.Write(ToPushReg.RawName);

                writer.Write(Reg1.RawName.Length);
                writer.Write(Reg1.RawName);

                writer.Write(Reg2.RawName.Length);
                writer.Write(Reg2.RawName);

                return memStream.ToArray();
            }
        }
    }
}
