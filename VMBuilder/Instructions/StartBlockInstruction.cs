using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RegiVM.VMRuntime.RegiVMRuntime;

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

    public struct VMExceptionHandler
    {
        public VMBlockType Type;
        public int TryOffsetStart;
        public int TryOffsetEnd;
        public int Id;
        public int HandlerIndexStart;
        public int FilterIndexStart;
        public uint ExceptionTypeMetadataToken;
        public byte[] ExceptionTypeObjectKey;
        public CilInstruction PlaceholderStartInstruction;

        public void WriteBytes(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(HandlerIndexStart);
            writer.Write(FilterIndexStart);
            writer.Write(ExceptionTypeMetadataToken);
            writer.Write(ExceptionTypeObjectKey.Length);
            writer.Write(ExceptionTypeObjectKey);
            writer.Write(Id);
        }
    }
    public struct VMRuntimeExceptionHandler
    {
        public VMBlockType Type;
        public int HandlerOffsetStart;
        public int FilterOffsetStart;
        public Type ExceptionType;
        public byte[] ExceptionTypeObjectKey;
        public int Id;
    }
    public class StartBlockInstruction : VMInstruction
    {
        private readonly VMCompiler compiler;

        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; }
        public VMBlockType BlockType { get; }
        public List<VMExceptionHandler> ExceptionHandlers { get; } = new List<VMExceptionHandler>();

        public StartBlockInstruction(VMCompiler compiler, IList<HandlerClause<CilInstruction>> handlers, VMBlockType blockType)
        {
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.StartBlock;
            this.compiler = compiler;
            BlockType = blockType;

            var cilHandlers = handlers.Select(x => (CilExceptionHandler)x.Tag!).ToList();
            ExceptionHandlers = compiler.CompileExceptionHandlers(cilHandlers);
            
            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"-> START_BLOCK {BlockType}");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)BlockType);
                writer.Write(ExceptionHandlers.Count);
                foreach (var h in ExceptionHandlers)
                {
                    h.WriteBytes(writer);
                }
                return memStream.ToArray();
            }
        }
    }
}
