﻿using RegiVM.VMBuilder;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using TestVMApp;

namespace RegiVM.VMRuntime.Handlers
{
    internal static partial class InstructionHandlers
    {
        internal static int Comparator(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
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

        internal static int EndFinally(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
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

        internal static int JumpBool(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                     Dictionary<int, object> _)
        {
            int tracker = 0;
            Console.WriteLine("- [JUMP_BOOL]");

            int branchOffsetLength = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            int[] jumpOffsets = new int[branchOffsetLength];
            for (int i = 0; i < branchOffsetLength; i++)
            {
                jumpOffsets[i] = BitConverter.ToInt32(d.Slice(tracker, 4));
                tracker += 4;
            }
            bool shouldInvert = d[tracker++] == 1;
            bool isLeaveProtected = d[tracker++] == 1;

            byte[] shouldSkipTrackerRegName = d[tracker..].ToArray();
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
                        Int16 tmp3 = (Int16)t.GetConstObject(DataType.Int16, h[shouldBranchReg]);
                        isZero = tmp3 == (Int16)0;
                        break;
                    case 4:
                        Double tmp1 = Convert.ToDouble(t.GetConstObject(DataType.Int32, h[shouldBranchReg]));
                        isZero = tmp1 == 0.0d;
                        break;
                    case 8:
                        Double tmp2 = Convert.ToDouble(t.GetConstObject(DataType.Int64, h[shouldBranchReg]));
                        isZero = tmp2 == 0.0d;
                        break;
                }
            }
            catch (OverflowException)
            {
                switch (h[shouldBranchReg].Length)
                {
                    case 4:
                        UInt32 tmp1 = (UInt32)t.GetConstObject(DataType.UInt32, h[shouldBranchReg]);
                        isZero = tmp1 == 0U;
                        break;
                    case 8:
                        UInt64 tmp2 = (UInt64)t.GetConstObject(DataType.UInt64, h[shouldBranchReg]);
                        isZero = tmp2 == 0UL;
                        break;
                }
            }

            
            if (jumpOffsets.Length > 1)
            {
                Console.WriteLine("- > Switch Jump");

                // Switch statement is possibly in the stack.
                // We do not need to worry about control transfers into protected region blocks.
                uint value = (uint)t.GetConstObject(DataType.UInt32, h[shouldBranchReg]);
                if (value < jumpOffsets.Length)
                {
                    // ECMA: if value is less than n execution is transferred to the valueths target.
                    // value of 1 takes the second target, 0 takes the first target.
                    var option = jumpOffsets[value];
                    tracker = option;
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
                int branchToOffsetValue = branchToOffset;

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
                        if (t.ExceptionHandlers.Count > 0 && t.ExceptionHandlers.Peek().Type == VMBlockType.Finally)
                        {
                            // Treat as finally.
                            var finallyClause = t.ExceptionHandlers.Pop();
                            t.ActiveExceptionHandler = finallyClause;
                            tracker = finallyClause.HandlerOffsetStart;

                            // Store the active leave inst offset so we know where to go after the endfinally instruction.
                            t.ActiveExceptionHandler.LeaveInstOffset = branchToOffsetValue;
                        }
                        // Well, we don't have any finally handlers, let's just leave to the branch target.
                        else if (t.ExceptionHandlers.Count == 0)
                        {
                            tracker = branchToOffsetValue;
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

        internal static int JumpCall(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d, Dictionary<int, object> p)
        {
            int tracker = 0;
            Console.WriteLine("- [JUMP CALL!!!!]");

            // Crying, laughing... Why are you doing this Ellie?

            bool isInline = d[tracker++] == 1;

            int methodOffsetToCall = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            bool hasReturnValue = d[tracker++] == 1;

            int numParams = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            // Add param values.
            Dictionary<int, object> parameters = new Dictionary<int, object>();
            for (int i = 0; i < numParams; i++)
            {
                DataType paramDt = t.ReadDataType(d, ref tracker);
                DataType paramDtReal = t.ReadDataType(d, ref tracker);
                byte[] paramRegName = t.ReadBytes(d, ref tracker, out int _);
                var paramValue = h[new ByteArrayKey(paramRegName)];
                // TODO: Doing this means that every single reg MUST have a data type associated if it is a primitive type.
                if (paramDt == DataType.Unknown)
                {
                    parameters.Add(i, t.GetObject(paramValue));
                }
                else if (paramDt == DataType.Phi)
                {
                    // Load exception object onto param list.
                    parameters.Add(i, t.GetObject(t.ActiveExceptionHandler.ExceptionTypeObjectKey));
                }
                else
                {
                    parameters.Add(i, t.GetConstObject(paramDt, paramValue, paramDtReal));
                }
            }

            ByteArrayKey returnRegKey = default;
            if (hasReturnValue)
            {
                //DataType dt = t.ReadDataType(d, ref tracker);
                byte[] returnRegName = t.ReadBytes(d, ref tracker, out int _);
                returnRegKey = new ByteArrayKey(returnRegName);
            }

            if (!isInline)
            {
                int methodToken = methodOffsetToCall;
#if DEBUG
                var methodBase = typeof(TestProgram).Module.ResolveMethod(methodToken)!;
#else
                var methodBase = Assembly.GetExecutingAssembly().Modules.First().ResolveMethod(methodToken)!;
#endif
                var hasThis = methodBase!.CallingConvention.HasFlag(CallingConventions.HasThis);

                // TODO: Generics! By Ref! Expression trees cannot support:
                /*
                    https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/#limitations
                */
                var instance = hasThis && methodBase is not ConstructorInfo ? parameters[0] : null;
                var invokeParams = parameters.Skip(instance == null ? 0 : 1).Select(x => x.Value).ToArray();
                var eInvokeParams = new ParameterExpression[invokeParams.Length];
                var paramInfo = methodBase.GetParameters();
                for (int i = 0; i < invokeParams.Length; i++)
                {
                    if (i < paramInfo.Length)
                    {
                        var paramI = paramInfo[i];
                        // If paramI is signed and actual param is unsigned, convert.
                        if (paramI.ParameterType != invokeParams[i].GetType() 
                            && paramI.ParameterType.IsPrimitive 
                            && ((invokeParams[i].GetType().Name.StartsWith("U") 
                                && paramI.ParameterType.Name.StartsWith("I")) ||
                                (invokeParams[i].GetType().Name.StartsWith("I")
                                && paramI.ParameterType.Name.StartsWith("U"))))
                        {
                            unchecked
                            {
                                switch (paramI.ParameterType.Name)
                                {
                                    case "UInt32":
                                        {
                                            invokeParams[i] = (UInt32)(Int32)invokeParams[i];
                                        }
                                        break;
                                    case "UInt64":
                                        {
                                            invokeParams[i] = (UInt64)(Int64)invokeParams[i];
                                        }
                                        break;
                                    case "Int32":
                                        {
                                            invokeParams[i] = (Int32)(UInt32)invokeParams[i];
                                        }
                                        break;
                                    case "Int64":
                                        {
                                            invokeParams[i] = (Int64)(UInt64)invokeParams[i];
                                        }
                                        break;
                                    case "Int16":
                                        {
                                            invokeParams[i] = (Int16)(UInt16)invokeParams[i];
                                        }
                                        break;
                                    case "UInt16":
                                        {
                                            invokeParams[i] = (UInt16)(Int16)invokeParams[i];
                                        }
                                        break;
                                }
                            }
                        }

                        // Special conversion of uint16 to char.
                        if (paramI.ParameterType != invokeParams[i].GetType()
                            && invokeParams[i].GetType().Name.StartsWith("UInt16")
                            && paramI.ParameterType.Name.StartsWith("Char"))
                        {
                            invokeParams[i] = (char)(UInt16)invokeParams[i];
                        }
                    }

                    eInvokeParams[i] = Expression.Parameter(invokeParams[i].GetType());
                }

                Expression eCall = default!;
                if (methodBase is MethodInfo)
                {
                    // Call/callvirt.
                    eCall = Expression.Call(instance != null ? Expression.Constant(instance) : null, (MethodInfo)methodBase, eInvokeParams);
                }
                else if (methodBase is ConstructorInfo)
                {
                    // Newobj.
                    eCall = Expression.New((ConstructorInfo)methodBase, eInvokeParams);
                }
                var del = Expression.Lambda(eCall, eInvokeParams).Compile();
                var result = del.DynamicInvoke(invokeParams);
                // Instead of the below:
                //var result = methodInfo.Invoke(instance, invokeParams);
                if (hasReturnValue)
                {
                    if (result != null && (result.GetType().IsPrimitive))
                    {
                        // TODO: Avoid dynamic, switch on type of primitive?
                        if (!h.ContainsKey(returnRegKey))
                        {
                            h.Add(returnRegKey, BitConverter.GetBytes((dynamic)result));
                        }
                        else
                        {
                            h[returnRegKey] = BitConverter.GetBytes((dynamic)result);
                        }
                    }
                    else if (result != null && result.GetType() == typeof(string))
                    {
                        if (!h.ContainsKey(returnRegKey))
                        {
                            h.Add(returnRegKey, Encoding.Unicode.GetBytes((string)result));
                        }
                        else
                        {
                            h[returnRegKey] = Encoding.Unicode.GetBytes((string)result);
                        }
                    }
                    else
                    {
                        // It is an object, treat it like object to heap store in object table.
                        var objKey = t.ConvertObjectToHeap(result);
                        if (!h.ContainsKey(returnRegKey))
                        {
                            h.Add(returnRegKey, objKey);
                        }
                        else
                        {
                            h[returnRegKey] = objKey;
                        }
                    }
                }
            }
            else
            {
                t.MethodSignatures.Push(new VMMethodSig()
                {
                    PreviousIP = t.UnstableNextIP,
                    ParamCount = numParams,
                    ParamValues = parameters,
                    HasReturnValue = hasReturnValue,
                    ReturnRegister = returnRegKey
                });

                tracker = methodOffsetToCall;
            }
            
            return tracker;
        }

        internal static int LoadOrStoreRegister(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                                Dictionary<int, object> _)
        {
            // This covers loading and storing.
            int tracker = 0;
            DataType fromDataType = (DataType)d[tracker++];
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

        internal static int Duplicate(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
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

        internal static int ConvertNumber(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                       Dictionary<int, object> _)
        {
            Console.WriteLine("- [CONVERT NUMBER]");
            int tracker = 0;

            DataType fromDatatype = t.ReadDataType(d, ref tracker);
            DataType toDatatype = t.ReadDataType(d, ref tracker);

            var fromRegKey = new ByteArrayKey(t.ReadBytes(d, ref tracker, out var _));
            var toRegKey = new ByteArrayKey(t.ReadBytes(d, ref tracker, out var _));

            bool throwOverflowException = d[tracker++] == 1 ? true : false;
            bool isUnsignedFrom = d[tracker++] == 1 ? true : false;

            object fromValue = t.GetConstObject(fromDatatype, h[fromRegKey]);

            try
            {
                switch (toDatatype)
                {
                    case DataType.Int32:
                        {
                            var res = Convert.ToInt32(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.UInt32:
                        {
                            var res = Convert.ToUInt32(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.Int64:
                        {
                            var res = Convert.ToInt64(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.UInt64:
                        {
                            var res = Convert.ToUInt64(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.Single:
                        {
                            var res = Convert.ToSingle(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.Double:
                        {
                            var res = Convert.ToDouble(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.Int8:
                        {
                            var res = Convert.ToSByte(fromValue);
                            // Save.
                            h[toRegKey] = [(byte)res];
                        }
                        break;
                    case DataType.UInt8:
                        {
                            var res = Convert.ToByte(fromValue);
                            // Save.
                            h[toRegKey] = [res];
                        }
                        break;
                    case DataType.UInt16:
                        {
                            var res = Convert.ToUInt16(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                    case DataType.Int16:
                        {
                            var res = Convert.ToInt16(fromValue);
                            // Save.
                            h[toRegKey] = BitConverter.GetBytes(res);
                        }
                        break;
                }
            }
            catch (OverflowException)
            {
                // If we should throw, throw it to the caller.
                if (throwOverflowException)
                {
                    throw;
                }
            }
            return tracker;
        }

        internal static int NumberLoad(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                       Dictionary<int, object> _)
        {
            Console.WriteLine("- [CONST LOAD]");

            DataType numType = (DataType)d[0];
            int tracker = 1;
            int numByteToReadForValue = t.GetByteCountForDataType(numType);

            int registerLength = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            byte[] register = d.Slice(tracker, registerLength).ToArray();
            tracker += registerLength;

            int stringLength = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            byte[] value = d.Slice(tracker, numByteToReadForValue == -1 ? stringLength : numByteToReadForValue).ToArray();
            tracker += numByteToReadForValue == -1 ? stringLength : numByteToReadForValue;

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
        
        internal static int ParameterLoad(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                          Dictionary<int, object> p)
        {

            // PARAMETER LOAD
            int tracker = 0;
            int paramOffset = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;
            Console.WriteLine($"- [PARAMETER LOAD]");

            DataType paramDataType = (DataType)d[tracker++];
            object paramData = p[paramOffset];

            byte[] endResult = t.ConvertParameter(paramDataType, paramData);
            if (endResult == null)
            {
                endResult = t.ConvertObjectToHeap(paramData);
            }

            ByteArrayKey regName = new ByteArrayKey(d[tracker..].ToArray());
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

        internal static int Ret(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
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

        internal static int StartRegionBlock(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, ReadOnlySpan<byte> d,
                                             Dictionary<int, object> _)
        {
            // Exception Handlers
            int tracker = 0;
            Console.WriteLine("- [Region Start]");

            byte blockType = d[tracker++];

            int handlerCount = BitConverter.ToInt32(d.Slice(tracker, 4));
            tracker += 4;

            List<VMRuntimeExceptionHandler> handlers = new List<VMRuntimeExceptionHandler>();
            for (int i = 0; i < handlerCount; i++)
            {
                var handler = new VMRuntimeExceptionHandler();
                handler.Type = (VMBlockType)d[tracker++];

                int handlerOffsetStart = BitConverter.ToInt32(d.Slice(tracker, 4));
                tracker += 4;
                if (handlerOffsetStart > 0)
                {
                    handler.HandlerOffsetStart = handlerOffsetStart;
                }

                int filterOffsetStart = BitConverter.ToInt32(d.Slice(tracker, 4));
                tracker += 4;
                if (filterOffsetStart > 0)
                {
                    handler.FilterOffsetStart = filterOffsetStart;
                }

                uint exceptionTypeToken = BitConverter.ToUInt32(d.Slice(tracker, 4));
                tracker += 4;
                if (exceptionTypeToken != 0)
                {
                    handler.ExceptionType = typeof(VMRuntimeExceptionHandler).Module.ResolveType((int)exceptionTypeToken);
                }

                byte[] exceptionObjectKey = t.ReadBytes(d, ref tracker, out var exceptionObjectKeyLength);
                handler.ExceptionTypeObjectKey = exceptionObjectKey;

                int id = BitConverter.ToInt32(d.Slice(tracker, 4));
                tracker += 4;
                handler.Id = id;
                t.ExceptionHandlers.Push(handler);
            }

            return tracker;
        }
    }
}