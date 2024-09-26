using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using System.Diagnostics;

namespace RegiVM.VMBuilder.Instructions
{
    public class LocalStoreInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public CilLocalVariable LocalVar { get; }
        public VMRegister Reg1 { get; }
        public VMRegister TempReg1 { get; }
        public override byte[] ByteCode { get; set; }

        public LocalStoreInstruction(VMCompiler compiler, CilInstruction inst, CilLocalVariable localVar)
        {
            MethodIndex = compiler.MethodIndex;
            Inst = inst;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.LoadOrStoreRegister;
            LocalVar = localVar;

            TempReg1 = Registers.Temporary.Pop();

            if (Registers.Registers.Any(x => x.LocalVar == localVar))
            {
                //Debugger.Break();
                //throw new Exception("Already an existing register for the local variable!");
                // Load the existing register for that local variable.
                Reg1 = Registers.Registers.First(x => x.LocalVar == localVar);
            }
            else
            {
                Reg1 = Registers.ForPush(compiler.PreviousDepth, compiler.Push, compiler.Pop);
            }
            Reg1.LastOffsetUsed = Inst.Offset;
            Reg1.OriginalOffset = Inst.Offset;
            Reg1.DataType = TempReg1.DataType;
            Reg1.CurrentData = TempReg1.CurrentData;
            Reg1.LocalVar = LocalVar;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"STORE_LOCAL {TempReg1} -> {Reg1}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)TempReg1.DataType);
                writer.Write(TempReg1.RawName.Length);
                writer.Write(TempReg1.RawName);
                writer.Write(Reg1.RawName.Length);
                writer.Write(Reg1.RawName);
                return memStream.ToArray();
            }
        }
    }
}
