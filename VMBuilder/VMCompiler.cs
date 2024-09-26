using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using Echo.Ast.Analysis;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Regions.Detection;
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
using System.Threading.Tasks.Dataflow;

namespace RegiVM.VMBuilder
{

    public partial class VMCompiler
    {
        public RegisterHelper RegisterHelper { get; private set; }
        
        public InstructionBuilder InstructionBuilder { get; }

        public MethodDefinition CurrentMethod { get; private set; }
        public CilMethodBody CurrentMethodBody { get; private set; }
        public IList<CilInstruction> CurrentInstructions { get; private set; }
        public IList<CilExceptionHandler> CurrentExceptionHandlers { get; private set; }
        public MethodSignature CurrentSignature { get; private set; }

        public VMOpCode OpCodes { get; }

        public int PreviousDepth { get; set; }
        public int ProcessedDepth { get; set; }
        public int Pop { get; set; }
        public int Push { get; set; }
        public int MethodIndex { get; set; } = 0;

        public bool EncryptInstructions { get; private set; } = false;
        public bool CompressInstructions { get; private set; } = true;

        public ScopeBlock<CilInstruction> MethodBlocks { get; private set; }
        public ControlFlowGraph<CilInstruction> MethodStaticFlowGraph { get; private set; }
        public DataFlowGraph<CilInstruction> MethodDataFlowGraph { get; private set; }
        public List<VMExceptionHandler> ExceptionHandlers { get; } = new List<VMExceptionHandler>();
        public List<IMethodDefOrRef> ViableInlineTargets { get; private set; } = new List<IMethodDefOrRef>();
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

        public VMCompiler Encrypt(bool encrypt = true)
        {
            EncryptInstructions = encrypt;
            return this;
        }

        public VMCompiler Compress(bool compress = true)
        {
            CompressInstructions = compress;
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentBody"></param>
        /// <param name="instructionsToCompile">Instructions MUST be expanded already.</param>
        /// <param name="exceptionHandlers"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public byte[] Compile(
            IList<CilInstruction> instructionsToCompile, 
            IList<CilExceptionHandler> exceptionHandlers, 
            CilMethodBody parentBody, 
            MethodDefinition? methodToCheckForInlineTargets = null)
        {
            var parentMethodDefinition = parentBody?.Owner;
            if (parentMethodDefinition == null)
            {
                throw new ArgumentException("Parent body cannot be ownerless!");
            }

            exceptionHandlers = instructionsToCompile.FindRelatedExceptionHandlers(exceptionHandlers);

            // Must add the current parent owner method to ensure no duplicate methods are permitted entry.
            ViableInlineTargets.Add(methodToCheckForInlineTargets ?? parentMethodDefinition);

            var methodCalls = instructionsToCompile.FindAllCalls();
            foreach (var methodCall in methodCalls.Where(x => x is MethodDefinition))
            {
                var allCallsToMethod = ((MethodDefinition)methodCall).FindAllCallsToMethod();
                // We must use the parent method definition again to check that it is called by us ONLY.
                if (allCallsToMethod.All(x => x == (methodToCheckForInlineTargets ?? parentMethodDefinition)) && !ViableInlineTargets.Contains((MethodDefinition)methodCall))
                {
                    // Only called by itself, no other method calls this method.
                    ViableInlineTargets.Add((MethodDefinition)methodCall);
                }
            }

            // Process current instructions.
            CurrentMethodBody = parentBody!;
            CurrentInstructions = instructionsToCompile;
            CurrentSignature = parentMethodDefinition.Signature!;
            CurrentExceptionHandlers = exceptionHandlers;
            
            var sfg = instructionsToCompile.ConstructSymbolicFlowGraph(exceptionHandlers, CurrentMethodBody, out var dfg);
            MethodBlocks = BlockBuilder.ConstructBlocks(sfg);
            MethodStaticFlowGraph = sfg;
            MethodDataFlowGraph = dfg;
            var astCompUnit = sfg.ToCompilationUnit(new CilPurityClassifier());

            var dryPass = new VMNodeVisitorDryPass(DryPass.Regular);
            dryPass.Visit(astCompUnit, this);

            var compiledHandlers = CompileExceptionHandlers(CurrentExceptionHandlers.ToList(), MethodIndex);
            ExceptionHandlers.AddRange(compiledHandlers);

            // Dry pass with exception handlers.
            dryPass = new VMNodeVisitorDryPass(DryPass.ExceptionHandlers);
            dryPass.Visit(astCompUnit, this);

            // The real run begins here.
            var realRun = new VMNodeVisitor();
            realRun.Visit(astCompUnit, this);

            if (ViableInlineTargets.Count > 0)
            {
                // Prepare for call inlining.
                MethodIndex++;
                InstructionBuilder.AddMethodDoNotIncrementMethodIndex();
            }

            // We DO NOT process the parent method definition, ONLY the instructions we have.
            // We CANNOT remove the parent method from this list of inline targets due to the method call inlining using index of method.
            // If it was removed: 1 would become 0 and so on.
            foreach (var processMethod in ViableInlineTargets.Skip(1))
            {
                // Reset exception handlers.
                ExceptionHandlers.Clear();

                CurrentMethodBody = ((MethodDefinition)processMethod).CilMethodBody!;
                CurrentSignature = processMethod.Signature!;
                CurrentMethodBody.Instructions.ExpandMacros();
                CurrentExceptionHandlers = CurrentMethodBody.ExceptionHandlers;
                CurrentInstructions = CurrentMethodBody.Instructions.ToList();

                var innerSfg = CurrentMethodBody.ConstructSymbolicFlowGraph(out var innerDfg);

                MethodBlocks = BlockBuilder.ConstructBlocks(innerSfg);
                MethodStaticFlowGraph = innerSfg;
                MethodDataFlowGraph = innerDfg;

                var innerAstCompUnit = innerSfg.ToCompilationUnit(new CilPurityClassifier());

                // Expand to make it easier to process.
                CurrentMethodBody.Instructions.ExpandMacros();

                // Dry pass without exception handlers.
                var innerDryPass = new VMNodeVisitorDryPass(DryPass.Regular);
                innerDryPass.Visit(innerAstCompUnit, this);

                var innerCompiledHandlers = CompileExceptionHandlers(CurrentExceptionHandlers.ToList(), MethodIndex);
                ExceptionHandlers.AddRange(innerCompiledHandlers);

                // Dry pass with exception handlers.
                innerDryPass = new VMNodeVisitorDryPass(DryPass.ExceptionHandlers);
                innerDryPass.Visit(innerAstCompUnit, this);

                // The real run begins here.
                var innerRealRun = new VMNodeVisitor();
                innerRealRun.Visit(innerAstCompUnit, this);

                // Done processing, restore the instructions to correct optimized version.
                CurrentMethodBody.Instructions.OptimizeMacros();

                if (CurrentMethodBody.Owner != ViableInlineTargets.Skip(1).Last())
                {
                    // Increment the method index.
                    MethodIndex++;
                    InstructionBuilder.AddMethodDoNotIncrementMethodIndex();
                }
            }

            // TODO: Replace this signature with custom signature for the instructions interacted with.
            return InstructionBuilder.ToByteArray(parentMethodDefinition.Signature!, CompressInstructions, EncryptInstructions);
        }

        public byte[] Compile(BasicBlock<CilInstruction> block, MethodDefinition parentMethod)
        {
            var sigForBlock = block.GetMethodSignatureForBlock(parentMethod, false);

            var insts = block.Instructions;
            if (insts.Last().OpCode.Code != CilCode.Ret)
            {
                var retInst = new CilInstruction(CilOpCodes.Ret);
                insts.Add(retInst);
            }

            // Fake a new method.
            var newMethodDef = new MethodDefinition(Guid.NewGuid().ToString(),
                parentMethod.Attributes,
                sigForBlock);
            var newMethodBody = new CilMethodBody(newMethodDef);
            foreach (var lv in parentMethod.CilMethodBody!.LocalVariables)
            {
                newMethodBody.LocalVariables.Add(new CilLocalVariable(lv.VariableType));
            }
            newMethodBody.Instructions.AddRange(insts);
            newMethodBody.Instructions.CalculateOffsets();

            return Compile(newMethodBody.Instructions, parentMethod.CilMethodBody!.ExceptionHandlers, newMethodBody, parentMethod);
        }

        public byte[] Compile(MethodDefinition startMethod)
        {
            var body = startMethod.CilMethodBody!;
            body.Instructions.ExpandMacros();
            var byteCode = Compile(body.Instructions, body.ExceptionHandlers, body);
            body.Instructions.OptimizeMacros();
            return byteCode;
        }

        public List<VMExceptionHandler> CompileExceptionHandlers(List<CilExceptionHandler> handlers, int methodIndex)
        {
            var results = new List<VMExceptionHandler>();
            foreach (var handler in handlers)
            {
                var vmHandler = new VMExceptionHandler();
                vmHandler.Type = handler.HandlerType.ToVMBlockHandlerType();
                CilInstruction? handlerStartInst = CurrentInstructions.GetByOffset(handler.HandlerStart?.Offset ?? -1);
                CilInstruction? filterStartInst = CurrentInstructions.GetByOffset(handler.FilterStart?.Offset ?? -1);
                if (handlerStartInst != null)
                {
                    var indexOfInstruction = CurrentInstructions.IndexOf(handlerStartInst);
                    var tries = 0;
                    while (!InstructionBuilder.IsValidOpCode(handlerStartInst.OpCode.Code) && tries++ < 5)
                    {
                        handlerStartInst = CurrentInstructions[++indexOfInstruction];
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
                    var indexOfInstruction = CurrentInstructions.IndexOf(filterStartInst);
                    var tries = 0;
                    while (!InstructionBuilder.IsValidOpCode(filterStartInst.OpCode.Code) && tries++ < 5)
                    {
                        filterStartInst = CurrentInstructions[++indexOfInstruction];
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

        public ulong[] GetUsedOpCodes()
        {
            return InstructionBuilder.GetUsedOpCodes();
        }
    }
}
