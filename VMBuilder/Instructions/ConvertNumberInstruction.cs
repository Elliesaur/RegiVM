using AsmResolver.PE.DotNet.Cil;
using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;

namespace RegiVM.VMBuilder.Instructions
{
    public class ConvertNumberInstruction : VMInstruction
    {
        public override ulong OpCode { get; }
        public CilInstruction Inst { get; }
        public VMRegister ToReg1 { get; }
        public VMRegister FromReg1 { get; }
        public bool ThrowOverflowException { get; }
        public bool IsUnsigned { get; }
        public override byte[] ByteCode { get; set; }

        public ConvertNumberInstruction(VMCompiler compiler, CilInstruction inst)
        {
            MethodIndex = compiler.MethodIndex;
            Inst = inst;
            Registers = compiler.RegisterHelper;
            OpCode = compiler.OpCodes.ConvertNumber;

            var num = Registers.Temporary.Pop();
            FromReg1 = num;
            ToReg1 = Registers.ForTemp();

            switch (inst.OpCode.Code)
            {
                case CilCode.Conv_I:
                    ToReg1.DataType = DataType.Int64;
                    break;
                case CilCode.Conv_Ovf_I_Un:
                    ToReg1.DataType = DataType.Int64;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_U:
                    ToReg1.DataType = DataType.UInt64;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_U_Un:
                    ToReg1.DataType = DataType.UInt64;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_U:
                    ToReg1.DataType = DataType.UInt64;
                    break;


                case CilCode.Conv_I1:
                    ToReg1.DataType = DataType.Int8;
                    break;
                case CilCode.Conv_I2:
                    ToReg1.DataType = DataType.Int16;
                    break;
                case CilCode.Conv_I4:
                    ToReg1.DataType = DataType.Int32;
                    break;
                case CilCode.Conv_I8:
                    ToReg1.DataType = DataType.Int64;
                    break;
                case CilCode.Conv_Ovf_I:
                    break;
                case CilCode.Conv_Ovf_I1:
                    ToReg1.DataType = DataType.Int8;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_I1_Un:
                    ToReg1.DataType = DataType.Int8;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_I2:
                    ToReg1.DataType = DataType.Int16;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_I2_Un:
                    ToReg1.DataType = DataType.Int16;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_I4:
                    ToReg1.DataType = DataType.Int32;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_I4_Un:
                    ToReg1.DataType = DataType.Int32;
                    IsUnsigned = true;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_I8:
                    ToReg1.DataType = DataType.Int64;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_I8_Un:
                    ToReg1.DataType = DataType.Int64;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_U1:
                    ToReg1.DataType = DataType.UInt8;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_U1_Un:
                    ToReg1.DataType = DataType.UInt8;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_U2:
                    ToReg1.DataType = DataType.UInt16;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_U2_Un:
                    ToReg1.DataType = DataType.UInt16;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_U4:
                    ToReg1.DataType = DataType.UInt32;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_U4_Un:
                    ToReg1.DataType = DataType.UInt32;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_Ovf_U8:
                    ToReg1.DataType = DataType.UInt64;
                    ThrowOverflowException = true;
                    break;
                case CilCode.Conv_Ovf_U8_Un:
                    ToReg1.DataType = DataType.UInt64;
                    ThrowOverflowException = true;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_R4:
                    ToReg1.DataType = DataType.Single;
                    break;
                case CilCode.Conv_R8:
                    ToReg1.DataType = DataType.Double;
                    break;
                case CilCode.Conv_R_Un:
                    ToReg1.DataType = DataType.Double;
                    IsUnsigned = true;
                    break;
                case CilCode.Conv_U1:
                    ToReg1.DataType = DataType.UInt8;
                    break;
                case CilCode.Conv_U2:
                    ToReg1.DataType = DataType.UInt16;
                    break;
                case CilCode.Conv_U4:
                    ToReg1.DataType = DataType.UInt32;
                    break;
                case CilCode.Conv_U8:
                    ToReg1.DataType = DataType.UInt64;
                    break;
            }

            ByteCode = ToByteArray();
        }

        public override byte[] ToByteArray()
        {
            Console.WriteLine($"CONVERT_NUM {FromReg1}({FromReg1.DataType}) -> {ToReg1}({ToReg1.DataType})");

            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                // FROM
                writer.Write((byte)FromReg1.DataType);
                writer.Write((byte)ToReg1.DataType);
                writer.Write(FromReg1.RawName.Length);
                writer.Write(FromReg1.RawName);
                // TO
                writer.Write(ToReg1.RawName.Length);
                writer.Write(ToReg1.RawName);
                
                writer.Write(ThrowOverflowException);
                writer.Write(IsUnsigned);

                return memStream.ToArray();
            }
        }
    }
}
