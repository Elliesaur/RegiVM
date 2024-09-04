using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using RegiVM.VMBuilder;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMRuntime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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
            //if (d == 34)
            //{
            //    d = 600;
            //}
            //else
            //{
            //    d = 500;
            //}
            return d;
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Math5");
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
                .RegisterLimit(30)
                .RandomizeRegisterNames();
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
                byte[] from = t.ReadBytes(d, ref tracker, out int fromLength);
                byte[] to = t.ReadBytes(d, ref tracker, out int toLength);
                
                ByteArrayKey fromReg = new ByteArrayKey(from);
                ByteArrayKey toReg = new ByteArrayKey(to);

                var valueFrom = h[fromReg];
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

                byte[] shouldSkipTrackerRegName = d.Skip(tracker).ToArray();
                tracker += shouldSkipTrackerRegName.Length;

                ByteArrayKey shouldBranchReg = new ByteArrayKey(shouldSkipTrackerRegName);

                // Should this branch happen?
                bool shouldBranch = h[shouldBranchReg][0] == 1 ? true : false;

                if (!shouldInvert && shouldBranch)
                {
                    // Lol, just set the tracker to the god damn thing.
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                } 
                else if (shouldInvert && !shouldBranch)
                {
                    // Lol, just set the tracker to the god damn thing.
                    tracker = t.InstructionOffsetMappings[branchToOffset].Item1;
                }
                return tracker;
            });
            vm.OpCodeHandlers.Add(compiler.OpCodes.Comparator, (t, h, d, _) =>
            {
                // CEQ/CLT/CGT 
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


            var actualResult = TestProgram.Math5(50, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            
            var reg = vm.GetReturnRegister();
            Console.WriteLine(BitConverter.ToInt32(reg));

            Console.ReadKey();
        }
    }
    
}
