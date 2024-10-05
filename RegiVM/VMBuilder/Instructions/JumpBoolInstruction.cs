using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class JumpBoolInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public int[] JumpOffsets { get; }
        public bool ShouldInvert { get; }
        public bool IsLeave { get; }
        public override byte[] ByteCode { get; set; }
        public VMRegister TempReg1 { get; }

        public JumpBoolInstruction(VMCompiler compiler, CilInstruction inst, params int[] jumpOffsets)
        {
            MethodIndex = compiler.MethodIndex;
            JumpOffsets = jumpOffsets;

            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.JumpBool;

            // TODO: Change ShouldJump to registry entry and pull from that instead.
            // Load a number (boolean) into registry prior if it is unconditional branching.
            TempReg1 = Registers.PopTemp();

            if (inst.OpCode.Code == CilCode.Brfalse) 
            {
                ShouldInvert = true;
            }
            if (inst.OpCode.Code == CilCode.Leave)
            {
                IsLeave = true;
            }
            // Add references to the indexes. They aren't "offsets" yet.
            References.AddRange(jumpOffsets);

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"JUMP_BOOL {JumpOffsets.Length} ({string.Join(", ", JumpOffsets)}) {ShouldInvert} {TempReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(JumpOffsets.Length);
                foreach (var offset in JumpOffsets)
                {
                    writer.Write(offset);
                }
                writer.Write(ShouldInvert);
                writer.Write(IsLeave);
                writer.Write(TempReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
