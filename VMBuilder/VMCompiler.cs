using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
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
using RegiVM.VMBuilder.Instructions;
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

        Boolean = 0x12,

        Phi = 0x13
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
        public int MethodIndex { get; set; } = 0;

        public ScopeBlock<CilInstruction> MethodBlocks { get; private set; }
        public ControlFlowGraph<CilInstruction> MethodStaticFlowGraph { get; private set; }
        public DataFlowGraph<CilInstruction> MethodDataFlowGraph { get; private set; }
        public List<VMExceptionHandler> ExceptionHandlers { get; } = new List<VMExceptionHandler>();
        public List<MethodDefinition> ViableInlineTargets { get; } = new List<MethodDefinition>();
        public VMCompiler()
        {
            RegisterHelper = null;
            InstructionBuilder = new InstructionBuilder(this);
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
            // Add itself.
            ViableInlineTargets.Add(method);

            // TODO: Sort out concurrency issues.
            // Pass as param for AST visitor?
            var methodCalls = method.CilMethodBody!.FindAllCalls();
            foreach (var methodCall in methodCalls.Where(x => x is MethodDefinition))
            {
                var allCallsToMethod = ((MethodDefinition)methodCall).FindAllCallsToMethod();

                if (allCallsToMethod.All(x => x == method))
                {
                    // Only called by itself, no other method calls this method.
                    ViableInlineTargets.Add((MethodDefinition)methodCall);
                }
            }
            
            foreach (var processMethod in ViableInlineTargets)
            {
                CurrentMethod = processMethod;

                var sfg = CurrentMethod.CilMethodBody!.ConstructSymbolicFlowGraph(out var dfg);

                MethodBlocks = BlockBuilder.ConstructBlocks(sfg);
                MethodStaticFlowGraph = sfg;
                MethodDataFlowGraph = dfg;

                var astCompUnit = sfg.ToCompilationUnit(new CilPurityClassifier());

                CurrentMethod.CilMethodBody!.Instructions.ExpandMacros();

                // Dry pass without exception handlers.
                var dryPass = new VMNodeVisitorDryPass(DryPass.Regular);
                dryPass.Visit(astCompUnit, this);

                ExceptionHandlers.AddRange(CompileExceptionHandlers(CurrentMethod.CilMethodBody!.ExceptionHandlers.ToList(), MethodIndex));

                // Dry pass with exception handlers.
                dryPass = new VMNodeVisitorDryPass(DryPass.ExceptionHandlers);
                dryPass.Visit(astCompUnit, this);


                var visitor = new VMNodeVisitor();
                visitor.Visit(astCompUnit, this);

                //var walker = new VMAstWalker() { Compiler = this };
                //AstNodeWalker<CilInstruction>.Walk(walker, astCompUnit);

                CurrentMethod.CilMethodBody!.Instructions.OptimizeMacros();
                
                if (CurrentMethod != ViableInlineTargets.Last())
                {
                    // Increment the method index.
                    MethodIndex++;
                    InstructionBuilder.AddMethodDoNotIncrementMethodIndex();
                }
            }

            return InstructionBuilder.ToByteArray(method, true);
        }


        public List<VMExceptionHandler> CompileExceptionHandlers(List<CilExceptionHandler> handlers, int methodIndex)
        {
            var results = new List<VMExceptionHandler>();
            foreach (var handler in handlers)
            {
                var vmHandler = new VMExceptionHandler();
                vmHandler.Type = handler.HandlerType.ToVMBlockHandlerType();
                CilInstruction? handlerStartInst = CurrentMethod.CilMethodBody!.Instructions.GetByOffset(handler.HandlerStart?.Offset ?? -1);
                CilInstruction? filterStartInst = CurrentMethod.CilMethodBody!.Instructions.GetByOffset(handler.FilterStart?.Offset ?? -1);
                if (handlerStartInst != null)
                {
                    var indexOfInstruction = CurrentMethod.CilMethodBody!.Instructions.IndexOf(handlerStartInst);
                    var tries = 0;
                    while (!InstructionBuilder.IsValidOpCode(handlerStartInst.OpCode.Code) && tries++ < 5)
                    {
                        handlerStartInst = CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                    }
                    if (tries >= 5)
                    {
                        throw new Exception("Cannot process handler start. No target found.");
                    }
                    vmHandler.HandlerIndexStart = InstructionBuilder.InstructionToIndex(handlerStartInst, methodIndex);
                    InstructionBuilder.AddUsedMapping(vmHandler.HandlerIndexStart, methodIndex);
                }
                if (filterStartInst != null)
                {
                    var indexOfInstruction = CurrentMethod.CilMethodBody!.Instructions.IndexOf(filterStartInst);
                    var tries = 0;
                    while (!InstructionBuilder.IsValidOpCode(filterStartInst.OpCode.Code) && tries++ < 5)
                    {
                        filterStartInst = CurrentMethod.CilMethodBody!.Instructions[++indexOfInstruction];
                    }
                    if (tries >= 5)
                    {
                        throw new Exception("Cannot process filter start. No target found.");
                    }
                    vmHandler.FilterIndexStart = InstructionBuilder.InstructionToIndex(filterStartInst, methodIndex);
                    InstructionBuilder.AddUsedMapping(vmHandler.FilterIndexStart, methodIndex);
                }
                if (handler.ExceptionType != null)
                {
                    vmHandler.ExceptionTypeMetadataToken = handler.ExceptionType.MetadataToken.ToUInt32();
                }
                vmHandler.TryOffsetStart = handler.TryStart!.Offset;
                vmHandler.TryOffsetEnd = handler.TryEnd!.Offset;

                var sameRegionProtectedHandlers = (ExceptionHandlers.Count == 0 ? results : ExceptionHandlers)
                    .Where(x => x.TryOffsetStart == vmHandler.TryOffsetStart && 
                        x.TryOffsetEnd == vmHandler.TryOffsetEnd);
                if (sameRegionProtectedHandlers.Any())
                {
                    vmHandler.Id = sameRegionProtectedHandlers.First().Id;
                }
                else
                {
                    var highestId = results.OrderByDescending(x => x.Id).FirstOrDefault();
                    vmHandler.Id = highestId.Id + 1;
                }

                vmHandler.PlaceholderStartInstruction = new CilInstruction(CilOpCodes.Prefix7, vmHandler);
                vmHandler.ExceptionTypeObjectKey = Guid.NewGuid().ToByteArray();
                results.Add(vmHandler);
            }
            return results;
        }

    }
}
