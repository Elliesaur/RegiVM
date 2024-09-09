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
using Echo.Ast;

namespace RegiVM.VMBuilder
{
    public class InstructionBuilder
    {
        private readonly List<VMInstruction> _instructions = new List<VMInstruction>();
        private readonly List<Tuple<object, ulong>> _realInstructions = new List<Tuple<object, ulong>>();
        private readonly Dictionary<int, Tuple<int, int>> _instructionOffsetMappings = new Dictionary<int, Tuple<int, int>>();
        private readonly List<int> _usedInstructionIndexes = new List<int>();

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

        public void AddDryPass(ulong code, object realInstruction, int index = -1)
        {
            if (index != -1)
            {
                _realInstructions[index] = new Tuple<object, ulong>(realInstruction, code);
            }
            else
            {
                _realInstructions.Add(new Tuple<object, ulong>(realInstruction, code));
            }
        }

        public int InstructionToOffset(CilInstruction realInstruction)
        {
            var index = _realInstructions.IndexOf(_realInstructions.FirstOrDefault(x => x.Item1 == realInstruction)!);
            return index;
        }

        public void AddUsedMapping(int instructionIndex)
        {
            _usedInstructionIndexes.Add(instructionIndex);
        }

        public byte[] ToByteArray(bool performCompression)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                // Only write used mapping data, that way we do not reveal a whole lot about our internal workings.
                writer.Write(_usedInstructionIndexes.Distinct().Count());
                // We make sure it is unique.
                // We then shuffle the indexes to make sure someone reading them cannot restore the original sequence (know what branches first).
                foreach (var instIndex in _usedInstructionIndexes.Distinct().Shuffle())
                {
                    var mappingItem = _instructionOffsetMappings[instIndex];
                    writer.Write(instIndex);
                    writer.Write(mappingItem.Item1);
                    writer.Write(mappingItem.Item2);
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
                case CilCode.Leave:

                // Prefix 7 is used to know when a "startblock" occurs.
                case CilCode.Prefix7:
                    return true;

                default:
                    return false;
            }
        }

        public int FindIndexForObject(object statement)
        {
            return _realInstructions.IndexOf(_realInstructions.First(x => x.Item1 == statement));
        }
    }
}
