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
using AsmResolver.DotNet;

namespace RegiVM.VMBuilder
{
    public class InstructionBuilder
    {
        private readonly Dictionary<int, List<VMInstruction>> _instructions = new Dictionary<int, List<VMInstruction>>();
        private readonly Dictionary<int, List<Tuple<object, ulong>>> _realInstructions = new Dictionary<int, List<Tuple<object, ulong>>>();
        private readonly Dictionary<Tuple<int, int>, Tuple<int, int>> _instructionOffsetMappings = new Dictionary<Tuple<int, int>, Tuple<int, int>>();
        private readonly List<Tuple<int, int>> _usedInstructionIndexes = new List<Tuple<int, int>>();

        private readonly VMCompiler _compiler;
        public InstructionBuilder(VMCompiler state)
        {
            _compiler = state;
            AddMethodDoNotIncrementMethodIndex();
        }

        public void AddMethodDoNotIncrementMethodIndex()
        {
            _instructions.Add(_compiler.MethodIndex, new List<VMInstruction>());
            _realInstructions.Add(_compiler.MethodIndex, new List<Tuple<object, ulong>>());
        }

        public void Add(VMInstruction instruction, CilInstruction realInstruction, int methodIndex)
        {
            int index = _instructions[methodIndex].Count;
            _instructions[methodIndex].Add(instruction);
            // TODO: Check key is correct.
            _instructionOffsetMappings.Add(new Tuple<int, int>(methodIndex, index), new Tuple<int, int>(
                index == 0 ? 0 : _instructionOffsetMappings[new Tuple<int, int>(methodIndex, index - 1)].Item2, 
                index == 0 ? instruction.ByteCode.Length + 8 + 4 : _instructionOffsetMappings[new Tuple<int, int>(methodIndex, index - 1)].Item2 + instruction.ByteCode.Length + 8 + 4
                ));

        }

        public void AddDryPass(ulong code, object realInstruction, int methodIndex, int index = -1)
        {
            if (index != -1)
            {
                _realInstructions[methodIndex][index] = new Tuple<object, ulong>(realInstruction, code);
            }
            else
            {
                _realInstructions[methodIndex].Add(new Tuple<object, ulong>(realInstruction, code));
            }
        }

        public int InstructionToIndex(CilInstruction realInstruction, int methodIndex)
        {
            var index = _realInstructions[methodIndex].IndexOf(_realInstructions[methodIndex].FirstOrDefault(x => x.Item1 == realInstruction)!);
            return index;
        }

        public void AddUsedMapping(int instructionIndex, int methodIndex)
        {
            if (_usedInstructionIndexes.Any(x => x.Item1 == methodIndex && x.Item2 == instructionIndex))
            {
                // Prevent duplicates.
                return;
            }
            _usedInstructionIndexes.Add(new Tuple<int, int>(methodIndex, instructionIndex));
        }

        public byte[] ToByteArray(MethodDefinition originalStartMethod, bool performCompression)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {

                writer.Write(originalStartMethod.Signature!.ReturnsValue);
                writer.Write(originalStartMethod.Signature!.GetTotalParameterCount());

                // Only write used mapping data, that way we do not reveal a whole lot about our internal workings.
                int totalCount = _usedInstructionIndexes.Count();
                writer.Write(totalCount);
                // We make sure it is unique.
                // We then shuffle the indexes to make sure someone reading them cannot restore the original sequence (know what branches first).
                var currentOffset = 0;
                var diff = 0;
                var prevOffset = 0;
                KeyValuePair<Tuple<int, int>, Tuple<int, int>> prev = default;
                bool recalculate = false;
                foreach (var item in new Dictionary<Tuple<int, int>, Tuple<int, int>>(_instructionOffsetMappings))
                {
                    currentOffset = item.Value.Item1;
                    diff = item.Value.Item2 - item.Value.Item1;

                    Console.WriteLine($"Current {currentOffset}, Previous {prevOffset}");
                    if (currentOffset == 0 && prevOffset != 0)
                    {
                        // Method change.
                        _instructionOffsetMappings[item.Key] = new Tuple<int, int>(prev.Value.Item2, prev.Value.Item2 + diff);
                        Console.WriteLine($"-> NOW {_instructionOffsetMappings[item.Key]}");
                        prevOffset = prev.Value.Item2 + diff;
                        recalculate = true;
                        prev = item;
                        continue;
                    }
                    else if (recalculate)
                    {
                        // Second or onwards after method change.
                        _instructionOffsetMappings[item.Key] = new Tuple<int, int>(prevOffset, prevOffset + diff);
                        Console.WriteLine($"--> NOW {_instructionOffsetMappings[item.Key]}");
                        prevOffset = prevOffset + diff;
                        prev = item;
                        continue;
                    }
                    
                    prevOffset = currentOffset;
                    prev = item;
                }
                
                foreach (var instIndex in _usedInstructionIndexes.Shuffle())
                {
                    var mappingItem = _instructionOffsetMappings[instIndex];

                    // Method Index... TODO: Figure out how to remove this! No need for it :)
                    //writer.Write(instIndex.Item1);
                    
                    // Instruction Index
                    writer.Write(instIndex.Item2);

                    writer.Write(mappingItem.Item1);
                    writer.Write(mappingItem.Item2);
                }

                // Write instruction data. Must write method 0, then 1 after method 0.
                for (int i = 0; i <= _compiler.MethodIndex; i++)
                {
                    foreach (var instruction in _instructions[i])
                    {
                        writer.Write(instruction.OpCode);
                        if (instruction is JumpCallInstruction)
                        {
                            var operandBytes = instruction.ByteCode;

                            // Patch offset.
                            byte[] newOperandBytes = new byte[operandBytes.Length];
                            using (var mStreamO = new MemoryStream(newOperandBytes))
                            using (var mStream = new MemoryStream(operandBytes))
                            using (var bReader = new BinaryReader(mStream))
                            using (var bWriter = new BinaryWriter(mStreamO))
                            {
                                // If position == 4 then overwrite with new byte.
                                var existing = bReader.ReadBytes(1);
                                bWriter.Write(existing);

                                var methodIndex = bReader.ReadInt32();
                                // Read the real offset.
                                var methodIndexOffset = _instructionOffsetMappings[new Tuple<int, int>(methodIndex, 0)].Item1;
                                bWriter.Write(methodIndexOffset);
                                try
                                {
                                    while (true) 
                                        bWriter.Write(bReader.ReadByte());
                                }
                                catch (Exception)
                                {
                                    // Ugh, yea I could handle it properly.
                                }
                                writer.Write(newOperandBytes.Length);
                                writer.Write(newOperandBytes);
                            }
                            
                        }
                        else
                        {
                            var operandBytes = instruction.ByteCode;
                            writer.Write(operandBytes.Length);
                            writer.Write(operandBytes);
                        }
                    }
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
                case CilCode.Beq:
                case CilCode.Blt:
                case CilCode.Ble:
                case CilCode.Bgt:
                case CilCode.Bge:
                case CilCode.Bne_Un:
                case CilCode.Ldarg:
                case CilCode.Ldloc:
                case CilCode.Ldc_I4:
                case CilCode.Ldstr:
                case CilCode.Stloc:
                case CilCode.Starg:
                case CilCode.Ceq:
                case CilCode.Leave:
                case CilCode.Ret:
                case CilCode.Endfinally:
                case CilCode.Bgt_Un:
                case CilCode.Blt_Un:
                case CilCode.Ble_Un:
                case CilCode.Bge_Un:
                case CilCode.Switch:

                // Prefix 7 is used to know when a "startblock" occurs.
                case CilCode.Prefix7:
                    return true;

                default:
                    return false;
            }
        }

        public int FindIndexForObject(object statement, int methodIndex)
        {
            return _realInstructions[methodIndex].IndexOf(_realInstructions[methodIndex].First(x => x.Item1 == statement));
        }
    }
}
