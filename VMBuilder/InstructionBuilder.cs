using RegiVM.VMBuilder.Instructions;
using RegiVM.VMBuilder.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RegiVM.VMBuilder
{
    public class InstructionBuilder
    {
        private readonly List<VMInstruction> _instructions = new List<VMInstruction>();

        public InstructionBuilder()
        {
            
        }

        public void Add(VMInstruction instruction)
        {
            _instructions.Add(instruction);
        }

        public byte[] ToByteArray(bool performCompression)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
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
    }
}
