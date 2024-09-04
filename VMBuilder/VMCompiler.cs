using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using Echo.Ast.Analysis;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.DataFlow;
using Echo.DataFlow.Construction;
using Echo.Platforms.AsmResolver;
using Microsoft.Win32;
using RegiVM.VMBuilder.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder
{
    public enum DataType : byte
    {
        Unknown = 0,

        Int32 = 0x1,
        UInt32 = 0x2,

        Int64 = 0x3,
        UInt64 = 0x4,

        Single = 0x5,
        Double = 0x6,

        Int8 = 0x7,
        UInt8 = 0x8,

        Int16 = 0x9,
        UInt16 = 0x10,

        String = 0x11,

        Boolean = 0x12
    }

    public partial class VMCompiler
    {
        public RegisterHelper RegisterHelper { get; private set; }
        
        public InstructionBuilder InstructionBuilder { get; }

        public MethodDefinition CurrentMethod { get; private set; }

        public VMOpCode OpCodes { get; }

        public int PreviousDepth { get; set; }
        public int ProcessedDepth { get; set; }
        public int Pop { get; set; }
        public int Push { get; set; }

        public VMCompiler()
        {
            RegisterHelper = null;
            InstructionBuilder = new InstructionBuilder();
            OpCodes = new VMOpCode();
        }

        public VMCompiler RandomizeOpCodes()
        {
            OpCodes.RandomizeAll();
            return this;
        }

        public VMCompiler RegisterLimit(int numRegisters)
        {
            if (RegisterHelper == null)
            {
                RegisterHelper = new RegisterHelper(numRegisters);
            }
            return this;
        }

        public VMCompiler RandomizeRegisterNames()
        {
            RegisterHelper.RandomizeRegisterNames();
            return this;
        }

        

        public byte[] Compile(MethodDefinition method)
        {
            // TODO: Sort out concurrency issues.
            // Pass as param for AST visitor?
            CurrentMethod = method;

            var sfg = method.CilMethodBody!.ConstructSymbolicFlowGraph(out var dfg);
            var blocks = BlockBuilder.ConstructBlocks(sfg);
            var astCompUnit = sfg.ToCompilationUnit(new CilPurityClassifier());

            method.CilMethodBody!.Instructions.ExpandMacros();

            var dryPass = new VMNodeVisitorDryPass();
            dryPass.Visit(astCompUnit, this);

            var visitor = new VMNodeVisitor();
            visitor.Visit(astCompUnit, this);

            //var walker = new VMAstWalker() { Compiler = this };
            //AstNodeWalker<CilInstruction>.Walk(walker, astCompUnit);

            method.CilMethodBody!.Instructions.OptimizeMacros();

            return InstructionBuilder.ToByteArray(true);
        }
    }
}
