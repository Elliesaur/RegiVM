using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class ReturnInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public VMRegister TempReg1 { get; }
        public bool HasReturnValue { get; }
        public override byte[] ByteCode { get; set; }

        public ReturnInstruction(VMCompiler compiler, bool hasReturnValue)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.Ret;
            HasReturnValue = hasReturnValue;

            if (HasReturnValue)
            {
                TempReg1 = Registers.Temporary.Pop();
            }
            else
            {
                TempReg1 = null!;
            }

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"RETURN {(TempReg1 == null ? "void" : TempReg1)}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(HasReturnValue);
                if (HasReturnValue)
                {
                    writer.Write(TempReg1!.RawName.Length);
                    writer.Write(TempReg1!.RawName);
                }
                return memStream.ToArray();
            }
        }
    }
}
