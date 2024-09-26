using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using Echo.ControlFlow;
using Echo.DataFlow.Construction;
using Echo.DataFlow;
using Echo.Platforms.AsmResolver;
using Echo.ControlFlow.Construction;
using RegiVM.VMBuilder.Instructions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Echo.ControlFlow.Regions.Detection;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Serialization.Blocks;

namespace RegiVM.VMBuilder
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.Shuffle(new Random());
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            return source.ShuffleIterator(rng);
        }

        private static IEnumerable<T> ShuffleIterator<T>(
            this IEnumerable<T> source, Random rng)
        {
            var buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = rng.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }

        public static VMBlockType ToVMBlockHandlerType(this CilExceptionHandlerType exceptionHandlerType)
        {
            return (VMBlockType)exceptionHandlerType;
        }

        public static List<(CilExceptionHandler, VMExceptionHandler)> GetProtectedRegionForInstruction(this IList<CilExceptionHandler> handlers, List<VMExceptionHandler> vmHandlers, CilInstruction instTarget)
        {
            var results = new List<(CilExceptionHandler, VMExceptionHandler)>();
            foreach (var ex in handlers
                                .OrderByDescending(x => x.HandlerType)
                                .ThenBy(x => x.TryStart?.Offset))
            {
                if (ex.TryStart!.Offset <= instTarget.Offset && ex.TryEnd!.Offset >= instTarget.Offset)
                {
                    var vmHandler = vmHandlers.FirstOrDefault(x => x.TryOffsetStart == ex.TryStart!.Offset &&
                        x.TryOffsetEnd == ex.TryEnd!.Offset &&
                        x.Type == ex.HandlerType.ToVMBlockHandlerType());

                    if (vmHandler.Id != 0)
                    {
                        // Exists and is not default.
                        results.Add((ex, vmHandler));
                    }
                }
            }
            return results;
        }

        public static bool IsInSameProtectedRegion(this IList<CilExceptionHandler> handlers, List<VMExceptionHandler> vmHandlers, CilInstruction currentInst, CilInstruction instTarget)
        {
            var current = handlers.GetProtectedRegionForInstruction(vmHandlers, currentInst);
            var other = handlers.GetProtectedRegionForInstruction(vmHandlers, instTarget);

            var closestCurrent = FindClosest(current, currentInst);
            var closestOther = FindClosest(other, instTarget);

            if (closestCurrent.Item1 == closestOther.Item1)
            {
                return true;
            }
            // Find closest to the current instruction in each.

            //var firstCurrent = current.First();
            //var firstOther = other.First();
            //if (firstCurrent.Item2.Id == firstOther.Item2.Id)
            //{
            //    return true;
            //}
            return false;
        }

        public static (CilExceptionHandler, VMExceptionHandler) FindClosest(this List<(CilExceptionHandler, VMExceptionHandler)> data, CilInstruction inst)
        {
            int offset = inst.Offset;
            int minDistance = int.MaxValue;

            (CilExceptionHandler, VMExceptionHandler) closestRange = default;

            foreach (var handler in data)
            {
                var distanceToStart = offset - handler.Item1.TryStart!.Offset;
                var distanceToEnd = handler.Item1.TryEnd!.Offset - offset;

                var distance = distanceToStart;//Math.Min(distanceToStart, distanceToEnd);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestRange = handler;
                }
            }
            return closestRange;
        }
        
        public static IList<IMethodDefOrRef> FindAllCalls(this CilMethodBody md)
        {
            return md.Instructions.FindAllCalls();
        }

        public static IList<IMethodDefOrRef> FindAllCalls(this CilInstructionCollection instructions)
        {
            return instructions.ToList().FindAllCalls();
        }

        public static IList<IMethodDefOrRef> FindAllCalls(this IList<CilInstruction> instructions)
        {
            var res = new List<IMethodDefOrRef>();
            foreach (var inst in instructions)
            {
                if (inst.OpCode.FlowControl != CilFlowControl.Call)
                {
                    continue;
                }

                if ((inst.OpCode.Code == CilCode.Call || inst.OpCode.Code == CilCode.Callvirt)
                    && inst.Operand is IMethodDefOrRef)
                {
                    res.Add((IMethodDefOrRef)inst.Operand!);
                }
            }
            return res;
        }

        public static IList<IMethodDefOrRef> FindAllCallsToMethod(this MethodDefinition md)
        {
            var res = new List<IMethodDefOrRef>();
            foreach (var td in md.Module!.GetAllTypes())
            {
                foreach (var method in td.Methods.Where(x => x.HasMethodBody))
                {
                    var calls = method.CilMethodBody!.FindAllCalls();
                    if (calls.Any(x => x == md))
                    {
                        res.Add(method);
                    }
                }
            }
            return res;
        }

        public static ControlFlowGraph<CilInstruction> ConstructSymbolicFlowGraph(
            this IList<CilInstruction> instructions, 
            IList<CilExceptionHandler> exceptionHandlers,
            CilMethodBody methodBody,
            out DataFlowGraph<CilInstruction> dataFlowGraph)
        {
            var architecture = new CilArchitecture(methodBody);
            var dfgBuilder = new CilStateTransitioner(architecture);
            var cfgBuilder = new SymbolicFlowGraphBuilder<CilInstruction>(
                architecture,
                instructions,
                dfgBuilder
            );

            var ehRanges = exceptionHandlers
                .ToEchoRanges()
                .ToArray();

            var cfg = cfgBuilder.ConstructFlowGraph(0, ehRanges);
            if (ehRanges.Length > 0)
                cfg.DetectExceptionHandlerRegions(ehRanges);

            dataFlowGraph = dfgBuilder.DataFlowGraph;
            return cfg;
        }

        public static IList<CilExceptionHandler> FindRelatedExceptionHandlers(this IList<CilInstruction> instructions, IList<CilExceptionHandler> exceptionHandlers)
        {
            var res = new List<CilExceptionHandler>();
            foreach (var eh in exceptionHandlers)
            {
                var handlerOffset = eh.HandlerStart?.Offset ?? -1;
                var filterOffset = eh.FilterStart?.Offset ?? -1;
                var tryStart = eh.TryStart?.Offset ?? -1;

                // If the handler, filter, or try start itself is within the instruction range then the exception handler is relevant.
                if (instructions.Any(x => x.Offset == handlerOffset))
                {
                    res.Add(eh);
                }
                else if (instructions.Any(x => x.Offset == filterOffset))
                {
                    res.Add(eh);
                }
                else if (instructions.Any(x => x.Offset == tryStart))
                {
                    res.Add(eh);
                }
            }
            return res;
        }

        public static (ScopeBlock<CilInstruction>, ControlFlowGraph<CilInstruction>, DataFlowGraph<CilInstruction>)
            GetGraphsAndBlocks(this MethodDefinition method)
        {
            var sfg = method.CilMethodBody!.ConstructSymbolicFlowGraph(out var dfg);
            var blocks = BlockBuilder.ConstructBlocks(sfg);
            return (blocks, sfg, dfg);
        }
    }
}
