﻿using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using RegiVM.VMRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RegiVM.VMRuntime.RegiVMRuntime;

namespace RegiVM.VMBuilder.Instructions
{

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
    public class StartBlockInstruction : VMInstruction
    {
        private readonly VMCompiler compiler;

        public override ulong OpCode { get; }
        public override byte[] ByteCode { get; set; }
        public VMBlockType BlockType { get; }
        public List<VMExceptionHandler> ExceptionHandlers { get; } = new List<VMExceptionHandler>();

        public static int ExceptionHandlerIdentifier = 0;

        public StartBlockInstruction(VMCompiler compiler, IList<HandlerClause<CilInstruction>> handlers, VMBlockType blockType)
        {
            MethodIndex = compiler.MethodIndex;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.StartRegionBlock;
            this.compiler = compiler;
            BlockType = blockType;

            var cilHandlers = handlers.Select(x => (CilExceptionHandler)x.Tag!).ToList();
            ExceptionHandlers = compiler.CompileExceptionHandlers(cilHandlers, compiler.MethodIndex);
            
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
