using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;

namespace RegiVM.VMBuilder.Instructions
{
    public class NumLoadInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public object ValueToLoad { get; }
        public DataType NumType { get; }
        public CilInstruction Inst { get; }
        public CilLocalVariable? LocalVar { get; }
        public VMRegister Reg1 { get; }
        public override byte[] ByteCode { get; }

        public NumLoadInstruction(VMCompiler compiler, object valueToLoad, DataType numType, CilInstruction inst, CilLocalVariable? localVar)
        {
            ValueToLoad = valueToLoad;
            NumType = numType;
            Inst = inst;
            LocalVar = localVar;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.NumberLoad;

            Reg1 = Registers.ForPush(compiler.CurrentDepth, compiler.Push, compiler.Pop);
            Reg1.DataType = NumType;
            Reg1.LocalVar = LocalVar;

            var valueAsByte = BitConverter.GetBytes((dynamic)ValueToLoad);
            Reg1.CurrentData = valueAsByte;
            Reg1.LastOffsetUsed = Inst.Offset;
            Reg1.OriginalOffset = Inst.Offset;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"NUMBER {NumType} {Reg1} {ValueToLoad}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)NumType);
                writer.Write(Reg1.RawName.Length);
                writer.Write(Reg1.RawName);
                writer.Write(Reg1.CurrentData);
                return memStream.ToArray();
            }
        }
    }
}
