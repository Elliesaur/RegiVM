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
using System.Diagnostics;
using AsmResolver.DotNet.Signatures;

namespace RegiVM.VMBuilder
{
    public class InstructionBuilder
    {
        private readonly Dictionary<int, List<VMInstruction>> _instructions = new Dictionary<int, List<VMInstruction>>();
        private readonly Dictionary<int, List<Tuple<object, ulong>>> _realInstructions = new Dictionary<int, List<Tuple<object, ulong>>>();
        private Dictionary<Tuple<int, int>, Tuple<int, int>> _instructionOffsetMappings = new Dictionary<Tuple<int, int>, Tuple<int, int>>();
        private readonly List<Tuple<int, int>> _usedInstructionIndexes = new List<Tuple<int, int>>();
        private readonly List<int> _isDivisibleBy = new List<int>();

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

        public void CalculateOffsets(bool calculateEncrypted, out Dictionary<Tuple<int, int>, Tuple<int, int>> offsetMappings, int startIndex)
        {
            int currentOffset = startIndex;
            int currentIndex = 0;
            offsetMappings = [];
            if (_isDivisibleBy.Count == 0 && _compiler.EncryptionOption == VMEncryptionType.Random)
            {
                // Populate randoms
                Random r = new Random();
                int totalNumber = r.Next(20, 70);
                for (int i = 0; i < totalNumber; i++)
                {
                    _isDivisibleBy.Add(r.Next(3, 100));
                }
            }
            if (_isDivisibleBy.Count == 0 && _compiler.EncryptionOption == VMEncryptionType.MultiPathAndLowChance)
            {
                _isDivisibleBy.Add(2);
                _isDivisibleBy.Add(3);
                _isDivisibleBy.Add(4);
                _isDivisibleBy.Add(5);
                _isDivisibleBy.Add(6);
                _isDivisibleBy.Add(7);
                _isDivisibleBy.Add(8);
                _isDivisibleBy.Add(9);
                _isDivisibleBy.Add(10);
                _isDivisibleBy.Add(11);
                _isDivisibleBy.Add(12);
                _isDivisibleBy.Add(13);
                _isDivisibleBy.Add(14);
                _isDivisibleBy.Add(15);
                _isDivisibleBy.Add(25);
                _isDivisibleBy.Add(50);
            }
            // Is first is for the entire current VM instance.
            bool isFirst = true;
            foreach (var kv in _instructions)
            {
                for (int instIndex = 0; instIndex < kv.Value.Count; instIndex++)
                {
                    VMInstruction? inst = kv.Value[instIndex];
                    var size = calculateEncrypted ? inst.EncryptedTotalSize : inst.Size;
                    if (isFirst || inst.IsHandlerStart
                        // If multipath only we do not encrypt those who have zero keys.
                        || ((inst.ReferencedByInstruction.Count == 0 && _compiler.EncryptionOption == VMEncryptionType.MultiPathOnly)
                        || (_compiler.EncryptionOption == VMEncryptionType.Random && _isDivisibleBy.Any(x => instIndex % x == 0))
                        || (_compiler.EncryptionOption == VMEncryptionType.MultiPathAndLowChance && inst.ReferencedByInstruction.Count == 0 && _isDivisibleBy.Any(x => instIndex % x == 0))))
                    {
                        size = inst.Size;
                        inst.Offset = currentOffset;
                        var nextOffset = inst.NextOffset;
                        currentOffset += size;
                        offsetMappings.Add(
                            new Tuple<int, int>(inst.MethodIndex, currentIndex++),
                            new Tuple<int, int>(inst.Offset, nextOffset));
                        isFirst = false;
                    }
                    else
                    {
                        inst.Offset = currentOffset;
                        var nextOffset = calculateEncrypted ? inst.NextEncryptedOffset : inst.NextOffset;
                        currentOffset += size;
                        offsetMappings.Add(
                            new Tuple<int, int>(inst.MethodIndex, currentIndex++),
                            new Tuple<int, int>(inst.Offset, nextOffset));
                    }
                }
                // Reset the current index for new method.
                currentIndex = 0;          
            }
        }

        public void CalculateReferences()
        {
            foreach (var kv in _instructions)
            {
                for (int instIndex = 0; instIndex < kv.Value.Count; instIndex++)
                {
                    VMInstruction? inst = kv.Value[instIndex];
                    var referencedIndexes = inst.References;
                    if (referencedIndexes.Count == 0 
                        && inst is not ReturnInstruction 
                        && inst is not StartBlockInstruction
                        )
                    {
                        continue;
                    }
                    if (inst is JumpCallInstruction)
                    {
                        foreach (var methodIndex in referencedIndexes)
                        {
                            if (methodIndex > _instructions.Count)
                            {
                                // Likely an external reference.
                                continue;
                            }
                            var instForReference = _instructions[methodIndex][0];
                            //var offsets = _instructionOffsetMappings[new Tuple<int, int>(methodIndex, 0)];
                            //instForReference.ReferencedBy.Add(offsets.Item1);
                            instForReference.ReferencedByInstruction.Add(inst);
                            inst.ReferencesInstructions.Add(instForReference);
                        }
                    }
                    else if (inst is StartBlockInstruction)
                    {
                        var exceptionHandlers = ((StartBlockInstruction)inst).ExceptionHandlers;
                        foreach (var eh in exceptionHandlers)
                        {
                            // Mark each handler start as a handler start which will NOT be encrypted.
                            var filterStartIndex = eh.FilterIndexStart;
                            var handlerStartIndex = eh.HandlerIndexStart;
                            if (filterStartIndex > 0)
                            {
                                var startInst = _instructions[inst.MethodIndex][filterStartIndex];
                                startInst.IsHandlerStart = true;
                            }
                            if (handlerStartIndex > 0)
                            {
                                var startInst = _instructions[inst.MethodIndex][handlerStartIndex];
                                startInst.IsHandlerStart = true;
                            }
                        }
                    }
                    // If is return instruction and our current method index > 0
                    else if (inst is ReturnInstruction && kv.Key > 0)
                    {
                        // Look up first instruction to find caller.
                        var firstInstInMethod = _instructions[kv.Key][0];
                        var methodCallerInsts = firstInstInMethod.ReferencedByInstruction.Where(x => x is JumpCallInstruction);

                        // The method could be targeted by multiple jump call instructions. We must reference correctly ALL instances.
                        foreach (var methodCallerInst in methodCallerInsts)
                        {
                            // Find next inst that will be returned to after method call. Add a reference to it from current return.
                            var nextInstIndex = _instructions[methodCallerInst.MethodIndex].IndexOf(methodCallerInst) + 1;
                            var nextInst = _instructions[methodCallerInst.MethodIndex][nextInstIndex];
                            nextInst.ReferencedByInstruction.Add(inst);

                            // Fix up current insts references, although this will be the wrong method index!
                            inst.References.Add(nextInstIndex);
                            // Easier to rely on the references instructions which is object rich and contains the correct offset + method index.
                            inst.ReferencesInstructions.Add(nextInst);
                        }
                    }
                    else if (inst is JumpBoolInstruction)
                    {
                        var realInst = (JumpBoolInstruction)inst;
                        if (realInst.IsLeave)
                        {
                            if (referencedIndexes.Count > 1)
                            {
                                // Cannot happen.
                                Debugger.Break();
                            }

                            var reference = referencedIndexes.First();
                            var instForReference = _instructions[inst.MethodIndex][reference];

                            instForReference.ReferencedByInstruction.Add(inst);
                            inst.ReferencesInstructions.Add(instForReference);

                            // Add endfinallys.
                            var startSearchIndex = instIndex;
                            var endSearchIndex = reference;
                            var searchCount = endSearchIndex - startSearchIndex;
                            var instsBetween = _instructions[inst.MethodIndex].GetRange(startSearchIndex, searchCount);
                            var endfinallys = instsBetween.Where(x => x is EndFinallyInstruction);
                            foreach (var endfinally in endfinallys)
                            {
                                if (instForReference.ReferencedByInstruction.Contains(endfinally))
                                {
                                    continue;
                                }
                                // Do NOT add a reference to the jump bool to the end finally. 
                                // Doing so would break the fallthrough.
                                //endfinally.ReferencedByInstruction.Add(inst);
                                inst.ReferencesInstructions.Add(endfinally);
                                // Tie it back so the inst target for the leave is linked to the end finally.
                                instForReference.ReferencedByInstruction.Add(endfinally);
                            }
                        }
                        else
                        {
                            foreach (var reference in referencedIndexes)
                            {
                                if (reference > _instructions[inst.MethodIndex].Count)
                                {
                                    // Likely an external reference.
                                    continue;
                                }
                                var instForReference = _instructions[inst.MethodIndex][reference];

                                // This must be done after offsets are calculated.
                                //var offsets = _instructionOffsetMappings[new Tuple<int, int>(inst.MethodIndex, reference)];
                                //instForReference.ReferencedBy.Add(offsets.Item1);
                                instForReference.ReferencedByInstruction.Add(inst);
                                inst.ReferencesInstructions.Add(instForReference);
                            }
                        }
                    }
                    else
                    {
                        foreach (var reference in referencedIndexes)
                        {
                            if (reference > _instructions[inst.MethodIndex].Count)
                            {
                                // Likely an external reference.
                                continue;
                            }
                            var instForReference = _instructions[inst.MethodIndex][reference];

                            // This must be done after offsets are calculated.
                            //var offsets = _instructionOffsetMappings[new Tuple<int, int>(inst.MethodIndex, reference)];
                            //instForReference.ReferencedBy.Add(offsets.Item1);
                            instForReference.ReferencedByInstruction.Add(inst);
                            inst.ReferencesInstructions.Add(instForReference);
                        }
                    }
                }
            }
        }

        public byte[] ToByteArray(MethodSignature signature, bool performCompression, bool useEncryption)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                // TODO: Remove requirement for original start method.
                // Replace this with dynamic options to add a signature instead.
                writer.Write(signature.ReturnsValue);
                writer.Write(signature.GetTotalParameterCount());

                // Calculate new instruction offset mappings and update all instructions to have offsets in their sequence.
                CalculateOffsets(false, out _instructionOffsetMappings, 0);

                // Perform reference calculations.
                CalculateReferences();

                if (useEncryption)
                {
                    // Calculate offsets again, this time taking into consideration encryption keys.
                    CalculateOffsets(true, out _instructionOffsetMappings, 0);
                }

                // Patch offsets and issues with particular instruction bytecodes.
                PatchInstructionIndexesAsOffsets();
                // Add encryption over current bytecode.
                if (useEncryption)
                {
                    int totalEncrypted = 0;
                    int totalUnencrypted = 0;
                    Random r = new Random();
                    for (int i = 0; i <= _compiler.MethodIndex; i++)
                    {
                        VMInstruction prevInstruction = null!;

                        for (int instIndex = 0; instIndex < _instructions[i].Count; instIndex++)
                        {
                            VMInstruction? instruction = _instructions[i][instIndex];
                            var mapping = _instructionOffsetMappings[new Tuple<int, int>(i, instIndex)];

                            if (instruction.IsHandlerStart || (i == 0 && instIndex == 0)
                                || (((_compiler.EncryptionOption == VMEncryptionType.MultiPathOnly) && instruction.ReferencedByInstruction.Count == 0)
                                || (_compiler.EncryptionOption == VMEncryptionType.Random && _isDivisibleBy.Any(x => instIndex % x == 0))
                                || (_compiler.EncryptionOption == VMEncryptionType.MultiPathAndLowChance && instruction.ReferencedByInstruction.Count == 0 && _isDivisibleBy.Any(x => instIndex % x == 0))))
                            {
                                totalUnencrypted++;
                                Console.WriteLine($"Total Unencrypted: {totalUnencrypted}");
                                // First instruction!
                                // Is encrypted = false
                                writer.Write(false);
                                writer.Write(instruction.OpCode);

                                var operandBytes = instruction.ByteCode;
                                writer.Write(operandBytes.Length);
                                writer.Write(operandBytes);
                            }
                            else
                            {
                                totalEncrypted++;
                                Console.WriteLine($"Total Encrypted: {totalEncrypted}");
                                instruction.InitializeMasterKey();
                                instruction.EncryptCurrentByteCodeAndOperand();
                                instruction.AddKeys(prevInstruction);

                                var pos = writer.BaseStream.Position;
                                if (pos - 5 != mapping.Item1)
                                {
                                    Debugger.Break();
                                }

                                var isEncrypted = true;
                                writer.Write(isEncrypted);

                                var keyCount = instruction.EncryptionKeys.Count;
                                writer.Write(keyCount);

                                foreach (var key in instruction.EncryptionKeys.Shuffle())
                                {
                                    // Write key length (4)
                                    writer.Write(key.Length);
                                    // Write the key itself.
                                    writer.Write(key);
                                }

                                // Write length of encrypted content, then encrypted content.
                                var encryptedBytes = instruction.EncryptedByteCode;
                                writer.Write(encryptedBytes.Length);
                                writer.Write(encryptedBytes);
                                var afterPos = writer.BaseStream.Position;
                                var size = afterPos - pos;
                                if (afterPos - 5 != mapping.Item2)
                                {
                                    Debugger.Break();
                                }
                                if (size != instruction.EncryptedTotalSize)
                                {
                                    Debugger.Break();
                                }
                            }
                            prevInstruction = instruction;
                        }
                    }
                    Console.WriteLine($"-------------------------------------------------");
                    Console.WriteLine($"Total Encrypted Instructions: {totalEncrypted}");
                    Console.WriteLine($"Total Unencrypted Instructions: {totalUnencrypted}");
                    Console.WriteLine($"-------------------------------------------------");
                }
                else
                {
                    // Write instruction data. Must write method 0, then 1 after method 0.
                    for (int i = 0; i <= _compiler.MethodIndex; i++)
                    {
                        foreach (var instruction in _instructions[i])
                        {
                            // Is Encrypted = false
                            writer.Write(false);
                            writer.Write(instruction.OpCode);

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

        private void PatchInstructionIndexesAsOffsets()
        {
            for (int i = 0; i <= _compiler.MethodIndex; i++)
            {
                foreach (var instruction in _instructions[i])
                {
                    if (instruction is JumpCallInstruction && ((JumpCallInstruction)instruction).IsInlineCall)
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
                            instruction.ByteCode = newOperandBytes;
                        }

                    }
                    else if (instruction is JumpBoolInstruction)
                    {
                        var operandBytes = instruction.ByteCode;

                        // Patch offset.
                        byte[] newOperandBytes = new byte[operandBytes.Length];
                        using (var mStreamO = new MemoryStream(newOperandBytes))
                        using (var mStream = new MemoryStream(operandBytes))
                        using (var bReader = new BinaryReader(mStream))
                        using (var bWriter = new BinaryWriter(mStreamO))
                        {
                            var countOffsets = bReader.ReadInt32();
                            bWriter.Write(countOffsets);

                            for (int x = 0; x < countOffsets; x++)
                            {
                                var index = bReader.ReadInt32();

                                // Read the real offset.
                                var branchTargetOffset = _instructionOffsetMappings[new Tuple<int, int>(i, index)].Item1;
                                bWriter.Write(branchTargetOffset);
                            }

                            try
                            {
                                while (true)
                                    bWriter.Write(bReader.ReadByte());
                            }
                            catch (Exception)
                            {
                                // Ugh, yea I could handle it properly.
                            }
                            instruction.ByteCode = newOperandBytes;
                        }
                    }
                    else if (instruction is StartBlockInstruction)
                    {
                        var operandBytes = instruction.ByteCode;

                        // Patch offset.
                        byte[] newOperandBytes = new byte[operandBytes.Length];
                        using (var mStreamO = new MemoryStream(newOperandBytes))
                        using (var mStream = new MemoryStream(operandBytes))
                        using (var bReader = new BinaryReader(mStream))
                        using (var bWriter = new BinaryWriter(mStreamO))
                        {
                            // BlockType.
                            bWriter.Write(bReader.ReadByte());

                            var countHandlers = bReader.ReadInt32();
                            bWriter.Write(countHandlers);

                            for (int x = 0; x < countHandlers; x++)
                            {
                                // Type of handler.
                                bWriter.Write(bReader.ReadByte());

                                // Handler index start
                                var handlerIndex = bReader.ReadInt32();

                                // Filter index start.
                                var filterIndex = bReader.ReadInt32();

                                // Read the real offsets.
                                var handlerOffset = handlerIndex > 0 ? _instructionOffsetMappings[new Tuple<int, int>(i, handlerIndex)].Item1 : 0;
                                var filterOffset = filterIndex > 0 ? _instructionOffsetMappings[new Tuple<int, int>(i, filterIndex)].Item1 : 0;
                                bWriter.Write(handlerOffset);
                                bWriter.Write(filterOffset);
                                // Read rest of stuff.
                                // Exception type mdtkn
                                bWriter.Write(bReader.ReadUInt32());

                                // Exception handler object key.
                                var objKeyLength = bReader.ReadInt32();
                                bWriter.Write(objKeyLength);

                                bWriter.Write(bReader.ReadBytes(objKeyLength));
                                // Id
                                bWriter.Write(bReader.ReadInt32());
                            }

                            try
                            {
                                while (true)
                                    bWriter.Write(bReader.ReadByte());
                            }
                            catch (Exception)
                            {
                                // Ugh, yea I could handle it properly.
                            }
                            instruction.ByteCode = newOperandBytes;
                        }
                    }
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
                case CilCode.Call:
                case CilCode.Callvirt:
                case CilCode.Newobj:
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

        public ulong[] GetUsedOpCodes()
        {
            return _instructions.SelectMany(x => x.Value.Select(x => x.OpCode)).Distinct().ToArray();
        }
    }
}
