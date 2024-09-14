using RegiVM.VMBuilder;
using RegiVM.VMBuilder.Instructions;
using System.Diagnostics;

namespace RegiVM.VMRuntime.Handlers
{
    internal static partial class InstructionHandlers
    {
        internal static int Comparator(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                       Dictionary<int, object> _)
        {
            // CEQ/CLT/CGT and branch equivs.
            int tracker = 0;

            ComparatorType compareType = t.ReadComparatorType(d, ref tracker);
            DataType pop1DataType = t.ReadDataType(d, ref tracker);
            DataType pop2DataType = t.ReadDataType(d, ref tracker);

            byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
            byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
            byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

            byte[] val1 = h[new ByteArrayKey(pop1)];
            byte[] val2 = h[new ByteArrayKey(pop2)];

            byte[] endResult = t.PerformComparison(compareType, pop1DataType, pop2DataType, val1, val2);

            ByteArrayKey result = new ByteArrayKey(push1);
            if (!h.ContainsKey(result))
            {
                h.Add(result, endResult);
            }
            else
            {
                h[result] = endResult.ToArray();
            }
            return tracker;
        }

        internal static int Endfinally(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                       Dictionary<int, object> _)
        {
            // TODO: stacked/nested finally clauses...?

            // Always use the leave offset that entered the finally, to leave the finally.
            int offsetToLeaveTo = t.ActiveExceptionHandler.LeaveInstOffset;

            // Clean up current handler.
            t.ActiveExceptionHandler = default;

            return offsetToLeaveTo;
        }

        internal static int JumpBool(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                     Dictionary<int, object> _)
        {
            int tracker = 0;
            // OFFSET?
            int branchToOffset = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            bool shouldInvert = d.Skip(tracker++).Take(1).ToArray()[0] == 1 ? true : false;
            bool isLeaveProtected = d.Skip(tracker++).Take(1).ToArray()[0] == 1 ? true : false;

            byte[] shouldSkipTrackerRegName = d.Skip(tracker).ToArray();
            tracker += shouldSkipTrackerRegName.Length;

            ByteArrayKey shouldBranchReg = new ByteArrayKey(shouldSkipTrackerRegName);

            // Should this branch happen?
            bool shouldBranch = h[shouldBranchReg][0] == 1 ? true : false;
            bool isZero = false;
            bool isNull = false; // TODO: Support objects.
            bool isBool = false;
            try
            {
                switch (h[shouldBranchReg].Length)
                {
                    case 1:
                        isZero = h[shouldBranchReg][0] == 0;
                        isBool = true;
                        break;
                    case 2:
                        Int16 tmp3 = (Int16)t.GetNumberObject(DataType.Int16, h[shouldBranchReg]);
                        isZero = tmp3 == (Int16)0;
                        break;
                    case 4:
                        Double tmp1 = Convert.ToDouble(t.GetNumberObject(DataType.Int32, h[shouldBranchReg]));
                        isZero = tmp1 == 0.0d;
                        break;
                    case 8:
                        Double tmp2 = Convert.ToDouble(t.GetNumberObject(DataType.Int64, h[shouldBranchReg]));
                        isZero = tmp2 == 0.0d;
                        break;
                }
            }
            catch (OverflowException)
            {
                switch (h[shouldBranchReg].Length)
                {
                    case 4:
                        UInt32 tmp1 = (UInt32)t.GetNumberObject(DataType.UInt32, h[shouldBranchReg]);
                        isZero = tmp1 == 0U;
                        break;
                    case 8:
                        UInt64 tmp2 = (UInt64)t.GetNumberObject(DataType.UInt64, h[shouldBranchReg]);
                        isZero = tmp2 == 0UL;
                        break;
                }
            }

            if (isLeaveProtected && t.ActiveExceptionHandler != null && t.ActiveExceptionHandler.Type != VMBlockType.Finally && t.ActiveExceptionHandler.Id != 0)
            {
                // Make sure we clear the exception handlers for the same protected block...
                var sameRegionHandlers = t.ExceptionHandlers.items.Where(x => x.Id == t.ActiveExceptionHandler.Id && x.Type != VMBlockType.Finally);
                foreach (var sameRegionHandler in sameRegionHandlers.ToList())
                {
                    t.ExceptionHandlers.Remove(sameRegionHandler);
                }
                t.ActiveExceptionHandler = default;

                if (shouldBranch)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
            }

            if (isLeaveProtected && t.ExceptionHandlers.Count > 0 && t.ExceptionHandlers.Peek().Type == VMBlockType.Finally)
            {
                // Is finally instruction.
                var finallyClause = t.ExceptionHandlers.Pop();
                t.ActiveExceptionHandler = finallyClause;
                tracker = finallyClause.HandlerOffsetStart;

                // Store the active leave inst offset so we know where to go after the endfinally instruction.
                t.ActiveExceptionHandler.LeaveInstOffset = t.InstructionOffsetMappings[branchToOffset].Item1;
            }
            else if (isLeaveProtected)
            {
                if (shouldBranch)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                // TODO: Investigate why there is a leave when the finally has already been executed...
                // - (hi weirdo person creeping github) C:\Users\ellie\Documents\ShareX\Screenshots\2024-09\dnSpy_WiQn3NKZsu.png
                //Debugger.Break();
                //throw new Exception("Is leave protected instruction jump, but there is no handler...?");
            }
            else
            {
                if (!shouldInvert && !isZero)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                else if (!shouldInvert && shouldBranch && isBool)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                // TODO: Add is "not null" for brtrue support. 

                if (shouldInvert && isZero)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                else if (shouldInvert && !shouldBranch && isBool)
                {
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                // TODO: Add is "null" for brfalse support.
            }

            return tracker;
        }

        internal static int LoadOrStoreRegister(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                                Dictionary<int, object> _)
        {
            // This covers loading and storing.
            int tracker = 0;
            DataType fromDataType = (DataType)d.Skip(tracker++).Take(1).ToArray()[0];

            byte[] from = t.ReadBytes(d, ref tracker, out int fromLength);
            byte[] to = t.ReadBytes(d, ref tracker, out int toLength);

            ByteArrayKey fromReg = new ByteArrayKey(from);
            ByteArrayKey toReg = new ByteArrayKey(to);
            byte[] valueFrom = new byte[0];
            if (fromDataType == DataType.Phi)
            {
                // Load exception....?
                // We know we are in a stloc.
                valueFrom = t.ActiveExceptionHandler.ExceptionTypeObjectKey;
            }
            else
            {
                valueFrom = h[fromReg];
            }

            if (!h.ContainsKey(toReg))
            {
                h.Add(toReg, valueFrom);
            }
            else
            {
                h[toReg] = valueFrom;
            }
            return tracker;
        }
        
        internal static int NumberLoad(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                       Dictionary<int, object> _)
        {
            DataType numType = (DataType)d[0];
            int tracker = 1;
            int numByteToReadForValue = t.GetByteCountForDataType(numType);

            int registerLength = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            byte[] register = d.Skip(tracker).Take(registerLength).ToArray();
            tracker += registerLength;

            byte[] value = d.Skip(tracker).Take(numByteToReadForValue).ToArray();
            tracker += numByteToReadForValue;

            ByteArrayKey regKey = new ByteArrayKey(register);

            if (!h.ContainsKey(regKey))
            {
                h.Add(regKey, value);
            }
            else
            {
                h[regKey] = value;
            }
            return tracker;
        }
        
        internal static int ParameterLoad(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                          Dictionary<int, object> p)
        {
            // PARAMETER LOAD
            int tracker = 0;
            int paramOffset = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            DataType paramDataType = (DataType)d[tracker++];
            object paramData = p[paramOffset];

            byte[] endResult = t.ConvertParameter(paramDataType, paramData);

            ByteArrayKey regName = new ByteArrayKey(d.Skip(tracker).ToArray());
            tracker += regName.Bytes.Length;

            if (!h.ContainsKey(regName))
            {
                h.Add(regName, endResult);
            }
            else
            {
                h[regName] = endResult;
            }
            return tracker;
        }

        internal static int Return(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                   Dictionary<int, object> _)
        {
            // RETURN 
            int tracker = 0;

            bool hasValue = d[tracker++] == 0 ? false : true;
            if (hasValue)
            {
                byte[] retValueReg = t.ReadBytes(d, ref tracker, out int _);
                ByteArrayKey result = new ByteArrayKey(retValueReg);
                t.RETURN_REGISTER = result;
            }
            return tracker;
        }

        internal static int StartRegionBlock(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                             Dictionary<int, object> _)
        {
            // Exception Handlers
            int tracker = 0;

            byte blockType = d.Skip(tracker).Take(1).ToArray()[0];
            tracker++;

            int handlerCount = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;
            List<VMRuntimeExceptionHandler> handlers = new List<VMRuntimeExceptionHandler>();
            for (int i = 0; i < handlerCount; i++)
            {
                var handler = new VMRuntimeExceptionHandler();
                handler.Type = (VMBlockType)d.Skip(tracker).Take(1).ToArray()[0];
                tracker++;

                int handlerOffsetStartIndex = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
                tracker += 4;
                if (handlerOffsetStartIndex > 0)
                {
                    handler.HandlerOffsetStart = t.InstructionOffsetMappings[handlerOffsetStartIndex].Item1;
                }

                int filterOffsetStartIndex = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
                tracker += 4;
                if (filterOffsetStartIndex > 0)
                {
                    handler.FilterOffsetStart = t.InstructionOffsetMappings[filterOffsetStartIndex].Item1;
                }

                uint exceptionTypeToken = BitConverter.ToUInt32(d.Skip(tracker).Take(4).ToArray());
                tracker += 4;
                if (exceptionTypeToken != 0)
                {
                    handler.ExceptionType = typeof(VMRuntimeExceptionHandler).Module.ResolveType((int)exceptionTypeToken);
                }

                byte[] exceptionObjectKey = t.ReadBytes(d, ref tracker, out var exceptionObjectKeyLength);
                handler.ExceptionTypeObjectKey = exceptionObjectKey;

                int id = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
                tracker += 4;
                handler.Id = id;
                t.ExceptionHandlers.Push(handler);
            }

            return tracker;
        }
    }
}