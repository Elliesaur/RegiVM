using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Registers
{
    public class VMRegister
    {
        public byte[] CurrentData { get; set; } = null!;
        public bool InUse { get; set; } = false;
        public int LastOffsetUsed { get; set; } = -1;
        public int OriginalOffset { get; set; } = -1;
        public byte[] RawName { get; set; } 
        public string Name { get; set; } 

        public DataType DataType { get; set; } = DataType.Unknown;

        public bool IsLocalVar => LocalVar != null;
        public bool IsParameter => Param != null;
        public CilLocalVariable? LocalVar { get; set; }
        public Parameter? Param { get; set; }

        public static byte[] ToUtf8Bytes(string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        public VMRegister(string name)
        {
            this.Name = name;
            this.RawName = ToUtf8Bytes(name);
        }
        
        public VMRegister(VMRegister other)
        {
            this.CurrentData = other.CurrentData;
            this.InUse = other.InUse;
            this.LastOffsetUsed = other.LastOffsetUsed;
            this.OriginalOffset = other.OriginalOffset;
            this.DataType = other.DataType;
            this.LocalVar = other.LocalVar;
            this.Param = other.Param;
            this.Name = other.Name;
            this.RawName = other.RawName;
        }

        public void ResetRegister()
        {
            InUse = false;
            CurrentData = null!;
            LastOffsetUsed = -1;
            OriginalOffset = -1;
            LocalVar = null;
            Param = null;
        }

        public override string ToString() => "Register(" + Name + ")";
    }
}
