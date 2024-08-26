﻿using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder;
using RegiVM.VMRuntime;
using System.Diagnostics;
using System.Text;
using static RegiVM.VMRuntime.RegiVMRuntime;

namespace RegiVM
{

    internal static class TestProgram
    {

        public static int Math1()
        {
            // Load to local var.
            int x = 10;
            // Load to local var.
            int z = 30;
            // Load to local var, using first local var.
            int y = x + 50;

            int a = y + 1;
            int b = y + 2;

            // Return 360.
            return y + 300;
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
    }

    public class Program
    {
        static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Math3");

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
                ByteArrayKey regKey = new ByteArrayKey(register);

                if (!h.ContainsKey(regKey))
                {
                    h.Add(regKey, value);
                }
                else
                {
                    h[regKey] = value;
                }
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
                if (!h.ContainsKey(regName))
                {
                    h.Add(regName, endResult);
                }
                else
                {
                    h[regName] = endResult;
                }
            });
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
            });

            var actualResult = TestProgram.Math3(50, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            
            var reg = vm.GetReturnRegister();
            Console.WriteLine(BitConverter.ToInt32(reg));

            Console.ReadKey();
        }
    }
    
}
