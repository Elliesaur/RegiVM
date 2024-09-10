using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public enum ComparatorType : byte
    {
        IsEqual = 0x1,
        IsNotEqual = 0x2,
        IsNotEqualUnsignedUnordered = 0x12,

        IsGreaterThan = 0x3,
        IsGreaterThanUnsignedUnordered = 0x13,

        IsLessThan = 0x4,
        IsLessThanUnsignedUnordered = 0x14,

        IsGreaterThanOrEqual = 0x5,
        IsGreaterThanOrEqualUnsignedUnordered = 0x15,

        IsLessThanOrEqual = 0x6,
        IsLessThanOrEqualUnsignedUnordered = 0x16,
    }
    public class ComparatorInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister Reg1 { get; }
        public VMRegister Reg2 { get; }
        public VMRegister ToPushReg { get; }
        public ComparatorType CompType { get; }
        public override byte[] ByteCode { get; }

        public ComparatorInstruction(VMCompiler compiler, CilInstruction inst, ComparatorType compType)
        {
            Registers = compiler.RegisterHelper;
            CompType = compType;

            // Get last two registers.
            var rawReg2 = Registers.Temporary.Pop();
            var rawReg1 = Registers.Temporary.Pop();
            Reg1 = new VMRegister(rawReg1);
            Reg2 = new VMRegister(rawReg2);


            ToPushReg = Registers.ForTemp();
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
