using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Instructions
{
    public enum VMBlockType : byte
    {
        Exception = 0x0,
        Filter = 0x1,
        Finally = 0x2,
        Fault = 0x3,
        Protected = 0x4
    }

    public class StartBlockInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; }
        public VMBlockType BlockType { get; }

        public StartBlockInstruction(VMCompiler compiler, VMBlockType blockType)
        {
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.StartBlock;

            BlockType = blockType;

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"-> START_BLOCK {BlockType}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)BlockType);
                return memStream.ToArray();
            }
        }
    }
}
