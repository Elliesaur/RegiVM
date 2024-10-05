using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;

namespace RegiVM.VMBuilder.Instructions
{
    public class ComparatorInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister Reg1 { get; }
        public VMRegister Reg2 { get; }
        public VMRegister ToPushReg { get; }
        public ComparatorType CompType { get; }
        public override byte[] ByteCode { get; set; }

        public ComparatorInstruction(VMCompiler compiler, CilInstruction inst, ComparatorType compType)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;
            CompType = compType;

            // Get last two registers.
            var rawReg2 = Registers.PopTemp();
            var rawReg1 = Registers.PopTemp();

            Reg1 = new VMRegister(rawReg1);
            Reg2 = new VMRegister(rawReg2);

            ToPushReg = Registers.PushTemp();
            ToPushReg.LastOffsetUsed = inst.Offset;
            ToPushReg.OriginalOffset = inst.Offset;
            ToPushReg.DataType = DataType.Boolean;

            Inst = inst;
            OpCode = compiler.OpCodes.Comparator;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"COMPARE {ToPushReg} {Reg1} ({CompType}) {Reg2}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)CompType);
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
