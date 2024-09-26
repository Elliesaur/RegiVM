using RegiVM.VMBuilder.Registers;
using RegiVM.VMRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder.Instructions
{
    public abstract class VMInstruction
    {
        public RegisterHelper Registers { get; protected set; } = null!;

        /// <summary>
        /// Instructions that reference (call) the current instruction.
        /// </summary>
        public List<VMInstruction> ReferencedByInstruction { get; } = [];

        /// <summary>
        /// Instructions that the current instruction references (inverse of ReferencedByInstruction).
        /// </summary>
        public List<int> References { get; } = [];
        public List<VMInstruction> ReferencesInstructions { get; } = [];

        /// <summary>
        /// The method index for this instruction.
        /// </summary>
        public int MethodIndex { get; set; }

        // isEncrypted + opcode + lenByteCode + ByteCode 
        public int Size => 1 + 8 + 4 + ByteCode.Length;

        public int EncryptedTotalSize => 
            // Is Encrypted Encrypted data size, encryption key count.
            1 + 4 + 4 + 
            // Encryption keys + key length entry (at min 1 encryption key).
            // The + 8 is due to internal lengths being stored in encrypt/decrypt function.
            ((AesGcm.NonceByteSizes.MaxSize + 8 + AesGcm.TagByteSizes.MaxSize + 4 + 32)
                * (ReferencedByInstruction.Count == 0 ? 1 : ReferencedByInstruction.Count)) + 
            // Overhead + original size - 1 (original size has "is encrypted" in it, we already do that at the front).
            AesGcm.NonceByteSizes.MaxSize + 8 + AesGcm.TagByteSizes.MaxSize + (Size - 1);

        public int EncryptedByteCodeSize => 8 + 4 + AesGcm.NonceByteSizes.MaxSize + 8 + AesGcm.TagByteSizes.MaxSize + ByteCode.Length;

        public List<byte[]> EncryptionKeys { get; set; } = new List<byte[]>();

        public byte[] MasterEncryptionKey { get; set; }

        public byte[] EncryptedByteCode { get; set;  } = Array.Empty<byte>();

        public bool IsHandlerStart { get; set; } = false;

        /// <summary>
        /// Current offset of the VM Instruction.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Next instruction offset start.
        /// </summary>
        public int NextOffset => Offset + Size;
        /// <summary>
        /// If basing it on encryption, this is the offset + encrypted size;
        /// </summary>
        public int NextEncryptedOffset => Offset + EncryptedTotalSize;

        public abstract ulong OpCode { get; }

        public abstract byte[] ByteCode { get; set;  }

        public abstract byte[] ToByteArray();

        /// <summary>
        /// NOTE: Relies on offsets being calculated and references calculated.
        /// NOTE: Relies on a master key being set through InitializeMasterKey()
        /// </summary>
        /// <returns></returns>
        public void AddKeys(VMInstruction previousInstructionForFallthrough)
        {
            if (MasterEncryptionKey == default)
            {
                throw new Exception("Master encryption key not set");
            }

            // Derive and add encryption keys for referenced by.
            foreach (var referencedBy in ReferencedByInstruction)
            {
                var derivedKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(referencedBy.Offset), BitConverter.GetBytes(Offset), 10000 + Offset, HashAlgorithmName.SHA512, 32);
                var encryptedMasterKey = AesGcmImplementation.Encrypt(MasterEncryptionKey, derivedKey);

                EncryptionKeys.Add(encryptedMasterKey);
            }

            if (EncryptionKeys.Count == 0)
            {
                // Add for fallthrough.
                var derivedKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(previousInstructionForFallthrough.Offset), BitConverter.GetBytes(Offset), 10000 + Offset, HashAlgorithmName.SHA512, 32);
                var encryptedMasterKey = AesGcmImplementation.Encrypt(MasterEncryptionKey, derivedKey);

                EncryptionKeys.Add(encryptedMasterKey);
            }
        }

        /// <summary>
        /// NOTE: Relies on offsets being correct.
        /// </summary>
        public void InitializeMasterKey()
        {
            MasterEncryptionKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(OpCode), BitConverter.GetBytes(Offset), 10000 + Offset, HashAlgorithmName.SHA512, 32);
        }

        /// <summary>
        /// NOTE: Relies on master encryption key being set
        /// NOTE: Relies on byte code being set.
        /// </summary>
        /// <returns></returns>
        public void EncryptCurrentByteCodeAndOperand()
        {
            if (MasterEncryptionKey == default)
            {
                throw new Exception("Master encryption key not set");
            }

            var byteCode = ByteCode;

            var opCodeAsByte = BitConverter.GetBytes(OpCode);
            var byteCodeLengthAsByte = BitConverter.GetBytes(byteCode.Length);

            var totalBytes = opCodeAsByte.Concat(byteCodeLengthAsByte).Concat(byteCode).ToArray();

            // Encrypt the data.
            var result = AesGcmImplementation.Encrypt(totalBytes, MasterEncryptionKey);

            EncryptedByteCode = result;
        }

        public void AddReference(int instructionIndex)
        {
            References.Add(instructionIndex);
        }
    }
}
