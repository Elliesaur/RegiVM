using RegiVM.VMBuilder.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Instructions
{
    public abstract class VMInstruction
    {
        public RegisterHelper Registers { get; protected set; } = null!;

        public abstract ulong OpCode { get; }

        public abstract byte[] ByteCode { get; }

        public abstract byte[] ToByteArray();
    }
}
