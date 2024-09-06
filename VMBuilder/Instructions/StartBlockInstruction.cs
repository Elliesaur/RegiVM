using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
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

    public struct VMExceptionHandler
    {
        public VMBlockType Type;
        public int HandlerIndexStart;
        public int FilterIndexStart;
        public uint ExceptionTypeMetadataToken;

        public void WriteBytes(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(HandlerIndexStart);
            writer.Write(FilterIndexStart);
            writer.Write(ExceptionTypeMetadataToken);
        }
    }
    public struct VMRuntimeExceptionHandler
    {
        public VMBlockType Type;
        public int HandlerOffsetStart;
        public int FilterOffsetStart;
        public Type ExceptionType;
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
            CompileExceptionHandlers(cilHandlers);

            ByteCode = ToByteArray();
        }

        private void CompileExceptionHandlers(List<CilExceptionHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                var vmHandler = new VMExceptionHandler();
                vmHandler.Type = handler.HandlerType.ToVMBlockHandlerType();
                CilInstruction? handlerStartInst = compiler.CurrentMethod.CilMethodBody!.Instructions.GetByOffset(handler.HandlerStart?.Offset ?? -1);
                CilInstruction? filterStartInst = compiler.CurrentMethod.CilMethodBody!.Instructions.GetByOffset(handler.FilterStart?.Offset ?? -1);
                if (handlerStartInst != null)
                {
                    var indexOfInstruction = compiler.CurrentMethod.CilMethodBody!.Instructions.IndexOf(handlerStartInst);
                    var tries = 0;
                    while (!compiler.InstructionBuilder.IsValidOpCode(handlerStartInst.OpCode.Code) && tries++ < 5)
                    {
                        handlerStartInst = compiler.CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                    }
                    if (tries >= 5)
                    {
                        throw new Exception("Cannot process handler start. No target found.");
                    }
                    vmHandler.HandlerIndexStart = compiler.InstructionBuilder.InstructionToOffset(handlerStartInst);
                    compiler.InstructionBuilder.AddUsedMapping(vmHandler.HandlerIndexStart);
                }
                if (filterStartInst != null)
                {
                    var indexOfInstruction = compiler.CurrentMethod.CilMethodBody!.Instructions.IndexOf(filterStartInst);
                    var tries = 0;
                    while (!compiler.InstructionBuilder.IsValidOpCode(filterStartInst.OpCode.Code) && tries++ < 5)
                    {
                        filterStartInst = compiler.CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                    }
                    if (tries >= 5)
                    {
                        throw new Exception("Cannot process handler start. No target found.");
                    }
                    vmHandler.FilterIndexStart = compiler.InstructionBuilder.InstructionToOffset(filterStartInst);
                    compiler.InstructionBuilder.AddUsedMapping(vmHandler.FilterIndexStart);
                }
                if (handler.ExceptionType != null)
                {
                    vmHandler.ExceptionTypeMetadataToken = handler.ExceptionType.MetadataToken.ToUInt32();
                }
                ExceptionHandlers.Add(vmHandler);
            }
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
