using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class MulInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public CilLocalVariable? LocalVar { get; }
        public VMRegister Reg1 { get; }
        public VMRegister Reg2 { get; }
        public VMRegister ToPushReg { get; }
        public override byte[] ByteCode { get; }

        public MulInstruction(VMCompiler compiler, CilInstruction inst, CilLocalVariable? localVar)
        {
            Registers = compiler.RegisterHelper;
            Reg1 = Registers.ForPop(compiler.ProcessedDepth - 2, compiler.Push, compiler.Pop);
            Reg2 = Registers.ForPop(compiler.ProcessedDepth - 1, compiler.Push, compiler.Pop);

            Registers.Reset([Reg1, Reg2]);

            ToPushReg = Registers.ForPush(compiler.ProcessedDepth, compiler.Push, compiler.Pop);
            ToPushReg.LastOffsetUsed = inst.Offset;
            ToPushReg.OriginalOffset = inst.Offset;
            ToPushReg.DataType = Reg1.DataType;

            // Check if next instruction is storing, and if so, track the variable
            if (LocalVar != null)
            {
                ToPushReg.LocalVar = LocalVar;
            }

            Inst = inst;
            LocalVar = localVar;
            OpCode = compiler.OpCodes.Mul;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"MUL {ToPushReg.DataType}={Reg1.DataType}*{Reg2.DataType} {ToPushReg} {Reg1} {Reg2}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)ToPushReg.DataType);
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
