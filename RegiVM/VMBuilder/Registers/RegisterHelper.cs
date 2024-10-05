using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RegiVM.VMBuilder.Registers
{
    public class RegisterHelper
    {
        public List<VMRegister> Registers { get; } = new List<VMRegister>();
        public Stack<VMRegister> Temporary { get; } = new Stack<VMRegister>();

        public bool IsRandomNames { get; set; } = false;

        public int Used => Registers.Count(x => x.InUse);
        public int Free => Registers.Count(x => !x.InUse);

        private int _numTempTotalLife = 0;

        //private List<string> _guidTempList = new List<string>();

        public RegisterHelper(int numRegisters)
        {
            for (int i = 0; i < numRegisters; i++)
            {
                Registers.Add(new VMRegister($"R{i}", RegisterType.LocalVariable));
                //_guidTempList.Add(Guid.NewGuid().ToString());
            }
        }

        public VMRegister ForPush(int depth, int push, int pop)
        {
            // Get a free register, instead of relying on depth being the index of the register to use.
            var reg = GetFree();
            reg.StackPosition = depth;
            return reg;
        }

        public VMRegister PushTemp()
        {
            string newGuid = Guid.NewGuid().ToString();//_guidTempList[_numTempTotalLife++];
            var reg = new VMRegister(IsRandomNames ? $"{newGuid}" : $"T{_numTempTotalLife++}", RegisterType.Temporary);
            Temporary.Push(reg);
            return reg;
        }

        public VMRegister PopTemp()
        {
            var reg = Temporary.Pop();

            // Reduce total number to match current stack.
            //_numTempTotalLife--;

            return reg;
        }

        private VMRegister GetFree()
        {
            var available = Registers.First(x => !x.InUse);
            available.InUse = true;
            return available;
        }

        public void Reset(IEnumerable<VMRegister> regs)
        {
            foreach (var r in regs)
            {
                if (r.IsLocalVar)
                {
                    // Skip...?
                    Console.WriteLine($" -> Skipping Local Var Register {r}");
                    continue;
                }
                if (r.IsParameter)
                {
                    // Skip...?
                    Console.WriteLine($" -> Skipping Param Register {r}");
                    continue;
                }
                r.ResetRegister();
            }
        }

        public void RandomizeRegisterNames()
        {
            foreach (var reg in Registers)
            {
                reg.Name = Guid.NewGuid().ToString();
                reg.RawName = VMRegister.ToUtf8Bytes(reg.Name);
            }
            IsRandomNames = true;
        }

        public VMRegister PeekTemp()
        {
            return Temporary.Peek();
        }
    }
}
