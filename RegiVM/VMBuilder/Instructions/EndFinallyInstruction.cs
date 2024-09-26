namespace RegiVM.VMBuilder.Instructions
{
    public class EndFinallyInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; set; }

        public EndFinallyInstruction(VMCompiler compiler)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.EndFinally;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"END_FINALLY");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                return memStream.ToArray();
            }
        }
    }
}
