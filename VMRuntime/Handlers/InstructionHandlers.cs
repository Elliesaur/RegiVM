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
            Console.WriteLine("- [COMPARATOR]");

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
            Console.WriteLine("- [END FINALLY]");

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
            Console.WriteLine("- [JUMP_BOOL]");

            int branchOffsetLength = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            int[] jumpOffsets = new int[branchOffsetLength];
            for (int i = 0; i < branchOffsetLength; i++)
            {
                jumpOffsets[i] = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
                tracker += 4;
            }


            //int branchToOffset = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            //tracker += 4;

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

            
            if (jumpOffsets.Length > 1)
            {
                Console.WriteLine("- > Switch Jump");

                // Switch statement is possibly in the stack.
                // We do not need to worry about control transfers into protected region blocks.
                uint value = (uint)t.GetNumberObject(DataType.UInt32, h[shouldBranchReg]);
                if (value < jumpOffsets.Length)
                {
                    // ECMA: if value is less than n execution is transferred to the valueths target.
                    // value of 1 takes the second target, 0 takes the first target.
                    var option = jumpOffsets[value];
                    tracker = t.InstructionOffsetMappings[option].Item1;
                    return tracker;
                }
                else
                {
                    // Fallthrough if not less than n.
                    return tracker;
                }
            }
            else
            {
                Console.WriteLine("- > Regular Jump");

                int branchToOffset = jumpOffsets[0];
                int branchToOffsetValue = t.InstructionOffsetMappings[branchToOffset].Item1;

                if (isLeaveProtected && t.ActiveExceptionHandler != null && t.ActiveExceptionHandler.Type != VMBlockType.Finally && t.ActiveExceptionHandler.Id != 0)
                {
                    Console.WriteLine("- > Leave protected (catch)");

                    // Make sure we clear the exception handlers for the same protected block...
                    var sameRegionHandlers = t.ExceptionHandlers.items.Where(x => x.Id == t.ActiveExceptionHandler.Id && x.Type != VMBlockType.Finally);
                    foreach (var sameRegionHandler in sameRegionHandlers.ToList())
                    {
                        t.ExceptionHandlers.Remove(sameRegionHandler);
                    }
                    t.ActiveExceptionHandler = default;

                    if (shouldBranch)
                    {
                        tracker = branchToOffsetValue;
                    }
                }

                if (isLeaveProtected && t.ExceptionHandlers.Count > 0 && t.ExceptionHandlers.Peek().Type == VMBlockType.Finally)
                {
                    Console.WriteLine("- > Leave protected (finally)");

                    // Is finally instruction.
                    var finallyClause = t.ExceptionHandlers.Pop();
                    t.ActiveExceptionHandler = finallyClause;
                    tracker = finallyClause.HandlerOffsetStart;

                    // Store the active leave inst offset so we know where to go after the endfinally instruction.
                    t.ActiveExceptionHandler.LeaveInstOffset = branchToOffsetValue;
                }
                else if (isLeaveProtected)
                {
                    Console.WriteLine("- > Leave protected (no catch executed, check finally)");
                    if (t.ExceptionHandlers.Count > 0)
                    {
                        // Exiting from a protected block without throwing exception
                        //   wipe the exception handlers for the current id.
                        var last = t.ExceptionHandlers.Pop();
                        var sameRegionHandlers = t.ExceptionHandlers.items.Where(x => x.Id == last.Id && x.Type != VMBlockType.Finally);
                        foreach (var sameRegionHandler in sameRegionHandlers.ToList())
                        {
                            t.ExceptionHandlers.Remove(sameRegionHandler);
                        }
                        if (t.ExceptionHandlers.Peek().Type == VMBlockType.Finally)
                        {
                            // Treat as finally.
                            var finallyClause = t.ExceptionHandlers.Pop();
                            t.ActiveExceptionHandler = finallyClause;
                            tracker = finallyClause.HandlerOffsetStart;

                            // Store the active leave inst offset so we know where to go after the endfinally instruction.
                            t.ActiveExceptionHandler.LeaveInstOffset = branchToOffsetValue;
                        }
                    }
                    else if (shouldBranch)
                    {
                        tracker = branchToOffsetValue;
                    }
                    // TODO: Investigate why there is a leave when the finally has already been executed...
                    // - (hi weirdo person creeping github) C:\Users\ellie\Documents\ShareX\Screenshots\2024-09\dnSpy_WiQn3NKZsu.png
                    //throw new Exception("Is leave protected instruction jump, but there is no handler...?");
                }
                else
                {
                    if (!shouldInvert && !isZero)
                    {
                        tracker = branchToOffsetValue;
                    }
                    else if (!shouldInvert && shouldBranch && isBool)
                    {
                        tracker = branchToOffsetValue;
                    }
                    // TODO: Add is "not null" for brtrue support. 

                    if (shouldInvert && isZero)
                    {
                        tracker = branchToOffsetValue;
                    }
                    else if (shouldInvert && !shouldBranch && isBool)
                    {
                        tracker = branchToOffsetValue;
                    }
                    // TODO: Add is "null" for brfalse support.
                }
            }

            return tracker;
        }

        internal static int JumpCall(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> p)
        {
            int tracker = 0;
            Console.WriteLine("- [JUMP CALL!!!!]");

            // Crying, laughing... Why are you doing this Ellie?

            bool isInline = d.Skip(tracker).Take(1).ToArray()[0] == 1 ? true : false;
            tracker++;

            if (!isInline)
            {
                throw new NotImplementedException("Non-inline call not implemented.");
            }

            int methodOffsetToCall = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            bool hasReturnValue = d.Skip(tracker).Take(1).ToArray()[0] == 1 ? true : false;
            tracker++;

            int numParams = BitConverter.ToInt32(d.Skip(tracker).Take(4).ToArray());
            tracker += 4;

            // Add param values.
            List<byte[]> paramValues = new List<byte[]>();
            for (int i = 0; i < numParams; i++)
            {
                byte[] paramRegName = t.ReadBytes(d, ref tracker, out int _);
                paramValues.Add(h[new ByteArrayKey(paramRegName)]);
            }

            Dictionary<int, object> parameters = new Dictionary<int, object>();
            for (int i = 0; i < paramValues.Count; i++)
            {
                byte[] paramVal = paramValues[i];
                parameters.Add(i, paramVal);
            }

            ByteArrayKey returnRegKey = default;
            if (hasReturnValue)
            {
                DataType dt = t.ReadDataType(d, ref tracker);
                byte[] returnRegName = t.ReadBytes(d, ref tracker, out int _);
                returnRegKey = new ByteArrayKey(returnRegName);
            }

            t.MethodSignatures.Push(new VMMethodSig()
            {
                PreviousIP = BitConverter.ToInt32(h[t.INSTRUCTION_POINTER]) + tracker + 12 /* 12 is for 8 + 4 opcode + operand length */,
                ParamCount = numParams,
                ParamValues = parameters,
                HasReturnValue = hasReturnValue,
                ReturnRegister = returnRegKey
            });
            
            t.MethodIndex++;

            tracker = methodOffsetToCall;
            
            return tracker;
        }

        internal static int LoadOrStoreRegister(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                                Dictionary<int, object> _)
        {
            // This covers loading and storing.
            int tracker = 0;
            DataType fromDataType = (DataType)d.Skip(tracker++).Take(1).ToArray()[0];
            Console.WriteLine("- [Load/Store]");

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

        internal static int DuplicateRegister(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                                Dictionary<int, object> _)
        {
            // This covers loading and storing.
            int tracker = 0;
            Console.WriteLine("- [Duplicate]");

            byte[] from = t.ReadBytes(d, ref tracker, out int fromLength);
            byte[] to = t.ReadBytes(d, ref tracker, out int toLength);

            ByteArrayKey fromReg = new ByteArrayKey(from);
            ByteArrayKey toReg = new ByteArrayKey(to);
            byte[] valueFrom = h[fromReg];

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
            Console.WriteLine("- [NUMBER LOAD]");

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
            Console.WriteLine($"- [PARAMETER LOAD] - > Param OFFSET {paramOffset}");

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
            Console.WriteLine("- [R E T U R N]");

            bool hasValue = d[tracker++] == 0 ? false : true;
            ByteArrayKey result = default;
            if (hasValue)
            {
                byte[] retValueReg = t.ReadBytes(d, ref tracker, out int _);
                result = new ByteArrayKey(retValueReg);
            }

            var last = t.MethodSignatures.Pop();
            
            // Decrement method index.
            t.MethodIndex--;

            // If we have a value, and we don't have any further methods on stack, set the return register.
            if (hasValue)
            {
                // We simply assign the return register for the last method signature to the result.
                if (!h.ContainsKey(last.ReturnRegister))
                {
                    h.Add(last.ReturnRegister, h[result]);
                }
                else
                {
                    h[last.ReturnRegister] = h[result];
                }
            }

            // Set to the previous IP, for the last method call it will be int.MinValue which breaks from the while loop.
            tracker = last.PreviousIP;

            return tracker;
        }

        internal static int StartRegionBlock(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                             Dictionary<int, object> _)
        {
            // Exception Handlers
            int tracker = 0;
            Console.WriteLine("- [Region Start]");

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