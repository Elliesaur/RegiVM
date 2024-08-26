using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class ReturnInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public VMRegister Reg1 { get; }
        public bool HasReturnValue { get; }
        public override byte[] ByteCode { get; }

        public ReturnInstruction(VMCompiler compiler, bool hasReturnValue)
        {
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.Ret;
            HasReturnValue = hasReturnValue;

            if (HasReturnValue)
            {
                Reg1 = Registers.GetLastUsed(1)[0];
            }
            else
            {
                Reg1 = null!;
            }

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"RETURN {(Reg1 == null ? "void" : Reg1)}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(HasReturnValue);
                if (HasReturnValue)
                {
                    writer.Write(Reg1.RawName.Length);
                    writer.Write(Reg1.RawName);
                }
                return memStream.ToArray();
            }
        }
    }
}
