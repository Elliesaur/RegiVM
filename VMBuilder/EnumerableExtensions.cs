using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Instructions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var res = new List<IMethodDefOrRef>();
            foreach (var inst in md.Instructions)
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
    }
}
