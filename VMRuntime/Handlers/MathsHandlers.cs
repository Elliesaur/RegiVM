using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RegiVM.VMBuilder;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMRuntime;

namespace RegiVM.VMRuntime.Handlers
{
    internal static partial class InstructionHandlers
    {
        internal static int Add(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // ADD 
            int tracker = 0;
            Console.WriteLine("- [Add]");

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
        }

        internal static int And(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // XOR 
            int tracker = 0;
            Console.WriteLine("- [Bitwise AND]");

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
        }

        internal static int Div(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // DIV 
            int tracker = 0;
            Console.WriteLine("- [Divide]");

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
        }

        internal static int Mul(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // MUL 
            int tracker = 0;
            Console.WriteLine("- [Multiply]");

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
        }

        internal static int Or(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // OR 
            int tracker = 0;
            Console.WriteLine("- [Bitwise OR]");

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
        }

        internal static int Sub(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // SUB 
            int tracker = 0;
            Console.WriteLine("- [Subtract]");

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
        }

        internal static int Xor(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d, Dictionary<int, object> _)
        {
            // XOR 
            int tracker = 0;
            Console.WriteLine("- [Bitwise XOR]");

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
        }
    }
}
