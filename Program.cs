using AsmResolver.DotNet;
using RegiVM.VMBuilder;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMRuntime;
using System.Runtime.CompilerServices;
using static RegiVM.VMRuntime.RegiVMRuntime;

namespace RegiVM
{

    internal static class TestProgram
    {

        public static int Math1()
        {
            // Load to local var.
            // Load to local var, using first local var.
            int y = 60;

            int a = y + 1;
            int b = y + 2;

            int c = a + b;

            // Return 360.
            return a + b + 300;
        }

        public static int Math2(int argument1, int argument2)
        {
            int temp = argument1 + argument2;

            return temp + 900;
        }

        public static int Math3(int argument1, int argument2)
        {
            int a = 1;
            int temp = argument1 + argument2;
            temp = temp - 50;
            temp = temp / 5;
            temp = temp ^ 2;
            temp = temp * 3;
            temp = temp | 3;
            temp = temp & 3;
            return a + temp + 900;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math4(int arg1, int arg2)
        {
            int a = 1;
            int b = 2;
            int c = a + b;
            int d = c * 10;
            d = d + arg1;
            d = d - arg2;
            
            return d + c;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math5(int arg1, int arg2)
        {
            a:
                int d = arg1;
                d = d - arg2;
                if (d == 0)
                {
                    goto a;
                }
            if (d != 34)
            {
                d = 600;
            }
            else
            {
                d = 500;
            }
            try
            {
                d = d / 0;
                // exception happens
                // -> push to the handler.
                d = d + 5;
            }
            catch (DivideByZeroException e)
            {
                // value pushed by the CLR that contains object reference for the exception just thrown.
                // <>
                // stloc <e>
                d = d / 1;
            }
            catch (ArgumentOutOfRangeException f)
            {
                d = d / 2;
            }
            catch (Exception g)
            {
                d = d / 3;
            }
            finally
            {
                d = d + 100;
            }
            return d;
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math6(int arg1, int arg2)
        {
            try
            {
            a:
                int d = arg1;
                d = d - arg2;
                if (d == 0)
                {
                    goto a;
                }
                if (d != 34)
                {
                    d = 600;
                }
                else
                {
                    d = 500;
                }
                try
                {
                    d = d / 0;
                    // exception happens
                    // -> push to the handler.
                    d = d + 5;
                }
                catch (DivideByZeroException e)
                {
                    // value pushed by the CLR that contains object reference for the exception just thrown.
                    // <>
                    // stloc <e>
                    d = d / 1;
                }
                catch (ArgumentOutOfRangeException f)
                {
                    d = d / 2;
                }
                catch (Exception g)
                {
                    d = d / 3;
                }
                finally
                {
                    d = d + 100;
                    arg2 = arg2 / 0;
                }
                return d;
            }
            catch
            {
                return 400;
            }
            finally
            {
                arg1 = 0;
                arg2 = 0;
            }
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Math6");
            //var testMd = new MethodDefinition("IDGAF", MethodAttributes.Public, new MethodSignature(CallingConventionAttributes.Default, module.CorLibTypeFactory.Int32, new List<TypeSignature>()));
            //testMd.CilMethodBody = new CilMethodBody(testMd);
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(1));
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(2));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Add));
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(3));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Add));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
            //testMd.CilMethodBody.Instructions.CalculateOffsets();
            //testMd.CilMethodBody.ComputeMaxStack();

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .RegisterLimit(30);
                //.RandomizeRegisterNames();
            byte[] data = compiler.Compile(testMd);

            Console.WriteLine($"Sizeof Data {data.Length} bytes");

            // Data for parameters is passed here.
            RegiVMRuntime vm = new RegiVMRuntime(true, data, 50, 60);

            vm.OpCodeHandlers.Add(compiler.OpCodes.NumberLoad, (t, h, d, _) =>
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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.ParameterLoad, (t, h, d, p) =>
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
            });
            // Yes, technically and definitely the maths opcodes can all be one fucking OPCODE OKAY
            // BUT I CANT BE FUCKED RIGHT NOW OK >:C
            vm.OpCodeHandlers.Add(compiler.OpCodes.Add, (t, h, d, _) =>
            {
                // ADD 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformAddition(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Sub, (t, h, d, _) =>
            {
                // SUB 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformSubtraction(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Mul, (t, h, d, _) =>
            {
                // MUL 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformMultiplication(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Div, (t, h, d, _) =>
            {
                // DIV 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformDivision(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Xor, (t, h, d, _) =>
            {
                // XOR 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformXor(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Or, (t, h, d, _) =>
            {
                // OR 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformOr(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.And, (t, h, d, _) =>
            {
                // XOR 
                int tracker = 0;

                DataType push1DataType = t.ReadDataType(d, ref tracker);
                DataType pop1DataType = t.ReadDataType(d, ref tracker);
                DataType pop2DataType = t.ReadDataType(d, ref tracker);

                byte[] push1 = t.ReadBytes(d, ref tracker, out int push1Length);
                byte[] pop1 = t.ReadBytes(d, ref tracker, out int pop1Length);
                byte[] pop2 = t.ReadBytes(d, ref tracker, out int pop2Length);

                byte[] val1 = h[new ByteArrayKey(pop1)];
                byte[] val2 = h[new ByteArrayKey(pop2)];

                byte[] endResult = t.PerformAnd(pop1DataType, pop2DataType, val1, val2);

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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Ret, (t, h, d, _) =>
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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.LocalLoadStore, (t, h, d, _) =>
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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.JumpBool, (t, h, d, _) =>
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
                    throw new Exception("Is leave protected instruction jump, but there is no handler...?");
                }
                else
                {
                    if (!shouldInvert && shouldBranch)
                    {
                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                    }
                    else if (shouldInvert)
                    {
                        try
                        {
                            switch (h[shouldBranchReg].Length)
                            {
                                // Bool, if it is false, branch, if true, do not branch.
                                case 1:
                                    if (!shouldBranch)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                                case 2:
                                    Int16 tmp3 = (Int16)t.GetNumberObject(DataType.Int16, h[shouldBranchReg]);
                                    if (tmp3 == (Int16)0)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                                case 4:
                                    Double tmp1 = Convert.ToDouble(t.GetNumberObject(DataType.Int32, h[shouldBranchReg]));
                                    if (tmp1 == 0.0d)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                                case 8:
                                    Double tmp2 = Convert.ToDouble(t.GetNumberObject(DataType.Int64, h[shouldBranchReg]));
                                    if (tmp2 == 0.0d)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                            }
                        }
                        catch (OverflowException)
                        {
                            switch (h[shouldBranchReg].Length)
                            {
                                case 4:
                                    UInt32 tmp1 = (UInt32)t.GetNumberObject(DataType.UInt32, h[shouldBranchReg]);
                                    if (tmp1 == 0U)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                                case 8:
                                    UInt64 tmp2 = (UInt64)t.GetNumberObject(DataType.UInt64, h[shouldBranchReg]);
                                    if (tmp2 == 0UL)
                                        tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                                    break;
                            }
                        }
                        
                    }
                }
                
                return tracker;
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.StartBlock, (t, h, d, _) =>
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
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.EndFinally, (t, h, d, _) =>
            {
                // TODO: stacked/nested finally clauses...?

                // Always use the leave offset that entered the finally, to leave the finally.
                int offsetToLeaveTo = t.ActiveExceptionHandler.LeaveInstOffset;

                // Clean up current handler.
                t.ActiveExceptionHandler = default;

                return offsetToLeaveTo;
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Comparator, (t, h, d, _) =>
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
            });


            var actualResult = TestProgram.Math6(50, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            
            var reg = vm.GetReturnRegister();
            Console.WriteLine(BitConverter.ToInt32(reg));

            Console.ReadKey();
        }
    }
    
}
