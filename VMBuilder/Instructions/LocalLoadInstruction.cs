using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class LocalLoadInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister Reg1 { get; }
        public VMRegister TempReg1 { get; }
        public override byte[] ByteCode { get; }

        public LocalLoadInstruction(VMCompiler compiler, CilInstruction inst, CilLocalVariable localVar)
        {
            Inst = inst;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.LoadOrStoreRegister;

            Reg1 = Registers.Registers.First(x => x.LocalVar == localVar);
            Reg1.LastOffsetUsed = Inst.Offset;

            TempReg1 = Registers.ForTemp();
            TempReg1.TempCopyFrom(Reg1);

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"LOAD_LOCAL {Reg1} -> {TempReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                // FROM
                writer.Write((byte)Reg1.DataType);
                writer.Write(Reg1.RawName.Length);
                writer.Write(Reg1.RawName);
                // TO
                writer.Write(TempReg1.RawName.Length);
                writer.Write(TempReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
