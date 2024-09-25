using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;

namespace RegiVM.VMBuilder.Instructions
{
    public class NumLoadInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public object ValueToLoad { get; }
        public DataType NumType { get; }
        public CilInstruction Inst { get; }
        public VMRegister TempReg1 { get; }
        public override byte[] ByteCode { get; set; }

        public NumLoadInstruction(VMCompiler compiler, object valueToLoad, DataType numType, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            ValueToLoad = valueToLoad;
            NumType = numType;
            Inst = inst;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.NumberLoad;

            TempReg1 = Registers.ForTemp();
            TempReg1.DataType = NumType;

            if (numType == DataType.Boolean)
            {
                TempReg1.CurrentData = new byte[] { (byte)((bool)valueToLoad ? 1 : 0) };
            }
            else
            {
                var valueAsByte = BitConverter.GetBytes((dynamic)ValueToLoad);
                TempReg1.CurrentData = valueAsByte;
            }
            
            TempReg1.LastOffsetUsed = Inst.Offset;
            TempReg1.OriginalOffset = Inst.Offset;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"NUMBER {NumType} {TempReg1} {ValueToLoad}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)NumType);
                writer.Write(TempReg1.RawName.Length);
                writer.Write(TempReg1.RawName);
                writer.Write(TempReg1.CurrentData);
                return memStream.ToArray();
            }
        }
    }
}
