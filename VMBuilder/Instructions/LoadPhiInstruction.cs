using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class LoadPhiInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; }
        public VMRegister TempReg1 { get; }

        public LoadPhiInstruction(VMCompiler compiler)
        {
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.StartBlock;

            TempReg1 = Registers.ForTemp();
            TempReg1.DataType = DataType.Unknown;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"LOAD_PHI {TempReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(TempReg1.RawName.Length);
                writer.Write(TempReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
