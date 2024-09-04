using RegiVM.VMBuilder.Instructions;
using RegiVM.VMBuilder.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using static System.Runtime.InteropServices.JavaScript.JSType;
using AsmResolver.PE.DotNet.Cil;
using System.Reflection.Emit;

namespace RegiVM.VMBuilder
{
    public class InstructionBuilder
    {
        private readonly List<VMInstruction> _instructions = new List<VMInstruction>();
        private readonly List<Tuple<CilInstruction, ulong>> _realInstructions = new List<Tuple<CilInstruction, ulong>>();
        public Dictionary<int, Tuple<int, int>> _instructionOffsetMappings = new Dictionary<int, Tuple<int, int>>();

        public InstructionBuilder()
        {
        }

        public void Add(VMInstruction instruction, CilInstruction realInstruction = null!)
        {
            int index = _instructions.Count;
            _instructions.Add(instruction);
            _instructionOffsetMappings.Add(index, new Tuple<int, int>(
                index == 0 ? 0 : _instructionOffsetMappings[index - 1].Item2, 
                index == 0 ? instruction.ByteCode.Length + 8 + 4 : _instructionOffsetMappings[index - 1].Item2 + instruction.ByteCode.Length + 8 + 4
                ));

        }

        public void AddDryPass(ulong code, CilInstruction realInstruction)
        {
            _realInstructions.Add(new Tuple<CilInstruction, ulong>(realInstruction, code));
        }

        public int InstructionToOffset(CilInstruction realInstruction)
        {
            return _realInstructions.IndexOf(_realInstructions.FirstOrDefault(x => x.Item1 == realInstruction)!);
        }

        public byte[] ToByteArray(bool performCompression)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(_instructionOffsetMappings.Count);    
                // Write mapping data.
                foreach (var mappingItem in _instructionOffsetMappings)
                {
                    writer.Write(mappingItem.Key);
                    writer.Write(mappingItem.Value.Item1);
                    writer.Write(mappingItem.Value.Item2);
                }
                // Write instruction data.
                foreach (var instruction in _instructions)
                {
                    writer.Write(instruction.OpCode);
                    var operandBytes = instruction.ByteCode;
                    writer.Write(operandBytes.Length);
                    writer.Write(operandBytes);
                }
                var result = memStream.ToArray();
                if (performCompression)
                {
                    using var compressedStream = new MemoryStream();
                    using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal, false))
                    {
                        gzipStream.Write(result);
                    }
                    return compressedStream.ToArray();
                }
                else
                {
                    return result;
                }
            }
        }

        public bool IsValidOpCode(CilCode code)
        {
            switch (code)
            {
                case CilCode.Nop:
                    return false;
                case CilCode.Add:
                case CilCode.Sub:
                case CilCode.Mul:
                case CilCode.Div:
                case CilCode.And:
                case CilCode.Or:
                case CilCode.Xor:
                case CilCode.Br:
                case CilCode.Brfalse:
                case CilCode.Brtrue:
                case CilCode.Ldarg:
                case CilCode.Ldloc:
                case CilCode.Ldc_I4:
                case CilCode.Ldstr:
                case CilCode.Stloc:
                case CilCode.Starg:
                case CilCode.Ceq:
                    return true;

                default:
                    return false;
            }
        }
    }
}
