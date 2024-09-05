namespace RegiVM.VMBuilder.Instructions
{
    public class EndBlockInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; }
        public VMBlockType BlockType { get; }

        public EndBlockInstruction(VMCompiler compiler, VMBlockType blockType)
        {
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.EndBlock;

            BlockType = blockType;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"-> END_BLOCK {BlockType}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)BlockType);
                return memStream.ToArray();
            }
        }
    }
}
