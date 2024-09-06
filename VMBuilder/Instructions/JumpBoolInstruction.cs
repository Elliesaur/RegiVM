using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class JumpBoolInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public int JumpOffset { get; }
        public bool ShouldInvert { get; }
        public bool IsLeave { get; }
        public override byte[] ByteCode { get; }
        public VMRegister TempReg1 { get; }

        public JumpBoolInstruction(VMCompiler compiler, int jumpOffset, CilInstruction inst)
        {
            JumpOffset = jumpOffset;

            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.JumpBool;

            // TODO: Change ShouldJump to registry entry and pull from that instead.
            // Load a number (boolean) into registry prior if it is unconditional branching.
            //ShouldJump = shouldJump;
            TempReg1 = Registers.Temporary.Pop();

            if (inst.OpCode.Code == CilCode.Brfalse) 
            {
                ShouldInvert = true;
            }
            if (inst.OpCode.Code == CilCode.Leave)
            {
                IsLeave = true;
            }

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"JUMP_BOOL {JumpOffset} {ShouldInvert} {TempReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(JumpOffset);
                writer.Write(ShouldInvert);
                writer.Write(IsLeave);
                writer.Write(TempReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
