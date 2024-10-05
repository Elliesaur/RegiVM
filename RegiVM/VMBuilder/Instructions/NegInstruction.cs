using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class NegInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister Reg1 { get; }
        public VMRegister ToPushReg { get; }
        public override byte[] ByteCode { get; set; }

        public NegInstruction(VMCompiler compiler, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;

            var rawReg1 = Registers.PopTemp();
            Reg1 = new VMRegister(rawReg1);

            ToPushReg = Registers.PushTemp();
            ToPushReg.LastOffsetUsed = inst.Offset;
            ToPushReg.OriginalOffset = inst.Offset;
            ToPushReg.DataType = Reg1.DataType;

            Inst = inst;
            OpCode = compiler.OpCodes.Neg;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"NEG {ToPushReg.DataType}=-{Reg1.DataType} {ToPushReg} {Reg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)Reg1.DataType);

                writer.Write(ToPushReg.RawName.Length);
                writer.Write(ToPushReg.RawName);

                writer.Write(Reg1.RawName.Length);
                writer.Write(Reg1.RawName);

                return memStream.ToArray();
            }
        }
    }
}
