using RegiVM.VMBuilder;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace RegiVM.VMRuntime
{
    internal class RegiVMRuntime
    {
        private ByteArrayKey INSTRUCTION_POINTER = new ByteArrayKey([0xff, 0xff, 0x1, 0x1]);
        private ByteArrayKey DATA = new ByteArrayKey([0xff, 0xff, 0x1, 0x2]);

        internal ByteArrayKey RETURN_REGISTER;

        // A heap which contains a list of bytes, the key is the register.
        private Dictionary<ByteArrayKey, byte[]> Heap { get; } = new Dictionary<ByteArrayKey, byte[]>();

        public Dictionary<ulong, Action<RegiVMRuntime, Dictionary<ByteArrayKey, byte[]>, byte[], Dictionary<int, object>>> OpCodeHandlers { get; } = new Dictionary<ulong, Action<RegiVMRuntime, Dictionary<ByteArrayKey, byte[]>, byte[], Dictionary<int, object>>>();

        public Dictionary<int, object> Parameters { get; } = new Dictionary<int, object>();

        internal RegiVMRuntime(bool isCompressed, byte[] data, params object[] parameters)
        {
            // Populate Inst Pointer Register (this is actually not used currently...)
            Heap.Add(INSTRUCTION_POINTER, BitConverter.GetBytes(0));

            // Technically we don't need to add the data to the heap, but it makes things easier for us.
            if (isCompressed)
            {
                using var uncompressedStream = new MemoryStream();
                using (var compressedStream = new MemoryStream(data))
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress, false))
                {
                    gzipStream.CopyTo(uncompressedStream);
                }
                Heap.Add(DATA, uncompressedStream.ToArray());
            }
            else
            {
                Heap.Add(DATA, data);
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                Parameters.Add(i, parameters[i]);
            }
        }

        internal void Step(ref int ip)
        {
            // Step once
            ulong opCode = BitConverter.ToUInt64(Heap[DATA].Skip(ip).Take(8).ToArray());
            ip += 8;

            int operandLength = BitConverter.ToInt32(Heap[DATA].Skip(ip).Take(4).ToArray());
            ip += 4;

            byte[] operandValue = Heap[DATA].Skip(ip).Take(operandLength).ToArray();
            ip += operandLength;

            OpCodeHandlers[opCode](this, Heap, operandValue, Parameters);
        }

        internal void Run()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int ip = 0;
            while (ip < Heap[DATA].Length)
            {
                Step(ref ip);
                Heap[INSTRUCTION_POINTER] = BitConverter.GetBytes(ip);
            }
            sw.Stop();

            Console.WriteLine(sw.ElapsedTicks);
        }

        internal int GetByteCountForDataType(DataType numType)
        {
            return numType switch
            {
                DataType.Int8 or DataType.UInt8 => 1,
                DataType.Int16 or DataType.UInt16 => 2,
                DataType.Int32 or DataType.UInt32 or DataType.Single => 4,
                DataType.Int64 or DataType.UInt64 or DataType.Double => 8,
                //_ => throw new ArgumentOutOfRangeException(nameof(numType), $"Unsupported DataType: {numType}")
            };
        }

        internal byte[] ConvertParameter(DataType dataType, object paramData)
        {
            return dataType switch
            {
                DataType.Int32 => BitConverter.GetBytes((Int32)paramData),
                DataType.UInt32 => BitConverter.GetBytes((UInt32)paramData),
                DataType.Int64 => BitConverter.GetBytes((Int64)paramData),
                DataType.UInt64 => BitConverter.GetBytes((UInt64)paramData),
                DataType.Int8 => new byte[] { Convert.ToByte((sbyte)paramData) },
                DataType.UInt8 => new byte[] { (byte)paramData },
                DataType.Int16 => BitConverter.GetBytes((Int16)paramData),
                DataType.UInt16 => BitConverter.GetBytes((UInt16)paramData),
                DataType.Single => BitConverter.GetBytes((Single)paramData),
                DataType.Double => BitConverter.GetBytes((Double)paramData),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal DataType ReadDataType(byte[] data, ref int tracker)
        {
            return (DataType)data[tracker++];
        }

        internal byte[] ReadBytes(byte[] data, ref int tracker, out int length)
        {
            length = BitConverter.ToInt32(data.Skip(tracker).Take(4).ToArray());
            tracker += 4;
            var res = data.Skip(tracker).Take(length).ToArray();
            tracker += length;
            return res;
        }

        internal byte[] PerformAddition(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD + rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) + BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) + BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) + BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) + BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] + (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] + right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) + BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) + BitConverter.ToUInt16(right)),
                DataType.Single => BitConverter.GetBytes(BitConverter.ToSingle(left) + BitConverter.ToSingle(right)),
                DataType.Double => BitConverter.GetBytes(BitConverter.ToDouble(left) + BitConverter.ToDouble(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] PerformSubtraction(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD - rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) - BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) - BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) - BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) - BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] - (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] - right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) - BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) - BitConverter.ToUInt16(right)),
                DataType.Single => BitConverter.GetBytes(BitConverter.ToSingle(left) - BitConverter.ToSingle(right)),
                DataType.Double => BitConverter.GetBytes(BitConverter.ToDouble(left) - BitConverter.ToDouble(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] PerformMultiplication(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD * rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) * BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) * BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) * BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) * BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] * (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] * right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) * BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) * BitConverter.ToUInt16(right)),
                DataType.Single => BitConverter.GetBytes(BitConverter.ToSingle(left) * BitConverter.ToSingle(right)),
                DataType.Double => BitConverter.GetBytes(BitConverter.ToDouble(left) * BitConverter.ToDouble(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }


        internal byte[] PerformDivision(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD / rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) / BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) / BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) / BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) / BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] / (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] / right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) / BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) / BitConverter.ToUInt16(right)),
                DataType.Single => BitConverter.GetBytes(BitConverter.ToSingle(left) / BitConverter.ToSingle(right)),
                DataType.Double => BitConverter.GetBytes(BitConverter.ToDouble(left) / BitConverter.ToDouble(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] PerformXor(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD ^ rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) ^ BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) ^ BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) ^ BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) ^ BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] ^ (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] ^ right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) ^ BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) ^ BitConverter.ToUInt16(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] PerformOr(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD | rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) | BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) | BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) | BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) | BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] | (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] | right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) | BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) | BitConverter.ToUInt16(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] PerformAnd(DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            if (leftDataType != rightDataType)
            {
                // We cannot determine and should not determine the crazy amounts of options.
                var leftObj = GetNumberObject(leftDataType, left);
                var rightObj = GetNumberObject(rightDataType, right);
                dynamic leftObjD = leftObj;
                dynamic rightObjD = rightObj;
                return BitConverter.GetBytes(leftObjD & rightObjD);
            }

            return leftDataType switch
            {
                DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) & BitConverter.ToInt32(right)),
                DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) & BitConverter.ToUInt32(right)),
                DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) & BitConverter.ToInt64(right)),
                DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) & BitConverter.ToUInt64(right)),
                DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] & (sbyte)right[0]), // Assuming sbyte for Int8
                DataType.UInt8 => BitConverter.GetBytes(left[0] & right[0]),
                DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) & BitConverter.ToInt16(right)),
                DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) & BitConverter.ToUInt16(right)),
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }
        internal object GetNumberObject(DataType dataType, byte[] val)
        {
            return dataType switch
            {
                DataType.Int32 => BitConverter.ToInt32(val),
                DataType.UInt32 => BitConverter.ToUInt32(val),
                DataType.Int64 => BitConverter.ToInt64(val),
                DataType.UInt64 => BitConverter.ToUInt64(val),
                DataType.Int8 => (sbyte)val[0],
                DataType.UInt8 => val[0],
                DataType.Int16 => BitConverter.ToInt16(val),
                DataType.UInt16 => BitConverter.ToUInt16(val),
                DataType.Single => BitConverter.ToSingle(val),
                DataType.Double => BitConverter.ToDouble(val)
                //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] GetReturnRegister()
        {
            return Heap[RETURN_REGISTER];
        }

        public struct ByteArrayKey
        {
            public readonly byte[] Bytes;
            private readonly int _hashCode;

            public override readonly bool Equals(object? obj)
            {
                var other = (ByteArrayKey)obj!;
                return Compare(Bytes, other.Bytes);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            private static int GetHashCode([NotNull] byte[] bytes)
            {
                unchecked
                {
                    var hash = 17;
                    for (var i = 0; i < bytes.Length; i++)
                    {
                        hash = hash * 23 + bytes[i];
                    }
                    return hash;
                }
            }

            public ByteArrayKey(byte[] bytes)
            {
                Bytes = bytes;
                _hashCode = GetHashCode(bytes);
            }

            public static unsafe bool Compare(byte[] a1, byte[] a2)
            {
                if (a1 == null || a2 == null || a1.Length != a2.Length)
                    return false;
                fixed (byte* p1 = a1, p2 = a2)
                {
                    byte* x1 = p1, x2 = p2;
                    var l = a1.Length;
                    for (var i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                        if (*(long*)x1 != *(long*)x2) return false;
                    if ((l & 4) != 0)
                    {
                        if (*(int*)x1 != *(int*)x2) return false;
                        x1 += 4;
                        x2 += 4;
                    }
                    if ((l & 2) != 0)
                    {
                        if (*(short*)x1 != *(short*)x2) return false;
                        x1 += 2;
                        x2 += 2;
                    }
                    if ((l & 1) != 0) if (*x1 != *x2) return false;
                    return true;
                }
            }
        }
    }

}
