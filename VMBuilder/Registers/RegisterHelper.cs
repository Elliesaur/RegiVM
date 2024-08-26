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
        public int Used => Registers.Count(x => x.InUse);
        public int Free => Registers.Count(x => !x.InUse);

        public RegisterHelper(int numRegisters)
        {
            for (int i = 0; i < numRegisters; i++)
            {
                Registers.Add(new VMRegister($"RGI{i}"));
            }
        }

        public VMRegister ForPush(int depthBeforeChange, int push, int pop)
        {
            return Registers[depthBeforeChange];
        }

        public VMRegister ForPop(int depthAfterChange, int push, int pop)
        {
            return Registers[depthAfterChange];
        }

        // TODO: Remove.
        public VMRegister GetFree()
        {
            var available = Registers.First(x => !x.InUse);
            available.InUse = true;
            return available;
        }

        public List<VMRegister> GetLastUsed(int count)
        {
            return Registers.Where(x => x.InUse)
                .OrderByDescending(x => x.LastOffsetUsed)
                .Take(count)
                .ToList();
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
        }
    }
}
