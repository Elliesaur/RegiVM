using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class DuplicateInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister ToReg1 { get; }
        public VMRegister FromReg1 { get; }
        public override byte[] ByteCode { get; set; }

        public DuplicateInstruction(VMCompiler compiler, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            Inst = inst;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.Duplicate;

            var topStack = Registers.Temporary.Peek();
            var newTop = Registers.ForTemp();
            newTop.TempCopyFrom(topStack);

            ToReg1 = newTop;
            FromReg1 = topStack;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"DUPLICATE {FromReg1} -> {ToReg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                // FROM
                writer.Write(FromReg1.RawName.Length);
                writer.Write(FromReg1.RawName);
                // TO
                writer.Write(ToReg1.RawName.Length);
                writer.Write(ToReg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
