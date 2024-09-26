using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Registers
{
    public enum RegisterType
    {
        Temporary = 0x1,
        LocalVariable = 0x2,
        Parameter = 0x3
    }

    public class VMRegister
    {
        public int StackPosition = -50;

        public byte[] CurrentData { get; set; } = null!;
        public bool InUse { get; set; } = false;
        public int LastOffsetUsed { get; set; } = -1;
        public int OriginalOffset { get; set; } = -1;
        public byte[] RawName { get; set; } 
        public string Name { get; set; } 

        public DataType DataType { get; set; } = DataType.Unknown;

        public RegisterType RegisterType { get; set; }

        public bool IsLocalVar => LocalVar != null;
        public bool IsParameter => Param != null;


        public CilLocalVariable? LocalVar { get; set; }
        public Parameter? Param { get; set; }

        public static byte[] ToUtf8Bytes(string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        public VMRegister(string name, RegisterType type = RegisterType.Temporary)
        {
            this.Name = name;
            this.RawName = ToUtf8Bytes(name);
            this.RegisterType = type;
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
            this.StackPosition = other.StackPosition;
            this.RegisterType = other.RegisterType;
        }

        public void ResetRegister()
        {
            InUse = false;
            CurrentData = null!;
            LastOffsetUsed = -1;
            OriginalOffset = -1;
            LocalVar = null;
            Param = null;
            StackPosition = -50;
        }

        public override string ToString() => $"{(RegisterType == RegisterType.Temporary ? "T" : "R")}({Name})";

        public void TempCopyFrom(VMRegister other)
        {
            this.CurrentData = other.CurrentData;
            this.InUse = other.InUse;
            this.LastOffsetUsed = other.LastOffsetUsed;
            this.OriginalOffset = other.OriginalOffset;
            this.DataType = other.DataType;
            this.LocalVar = other.LocalVar;
            this.Param = other.Param;
            this.StackPosition = other.StackPosition;
        }

        public VMRegister FromInstruction(CilInstruction inst)
        {
            if (inst.IsLdcI4())
            {
                CurrentData = BitConverter.GetBytes((int)inst.Operand!);
                DataType = DataType.Int32;
                OriginalOffset = inst.Offset;
                LastOffsetUsed = inst.Offset;
            }
            return this;
        }
    }
}
