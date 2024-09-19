using RegiVM.VMBuilder;
using RegiVM.VMBuilder.Instructions;
using RegiVM.VMRuntime.Handlers;
using System.Diagnostics;
using System.IO.Compression;

namespace RegiVM.VMRuntime
{
    public class RegiVMRuntime
    {
        private ByteArrayKey DATA = new ByteArrayKey([0xff, 0xff, 0x1, 0x2]);
        internal ByteArrayKey INSTRUCTION_POINTER = new ByteArrayKey([0xff, 0xff, 0x1, 0x1]);
        internal ByteArrayKey RETURN_REGISTER = new ByteArrayKey([0xff, 0xff, 0x6, 0x1]);

        // A heap which contains a list of bytes, the key is the register.
        private Dictionary<ByteArrayKey, byte[]> Heap { get; } = new Dictionary<ByteArrayKey, byte[]>();

        // Special heap for objects? Idk... maybe redo this.
        private Dictionary<ByteArrayKey, object> ObjectHeap { get; } = new Dictionary<ByteArrayKey, object>();

        // TODO: Calculate max opcode supported.
        public FuncDictionary<ulong> OpCodeHandlers { get; } = new FuncDictionary<ulong>(50);

        public Dictionary<int, object> Parameters { get; } = new Dictionary<int, object>();

        public StackList<VMMethodSig> MethodSignatures { get; } = new StackList<VMMethodSig>();

        public VMRuntimeExceptionHandler ActiveExceptionHandler { get; set; }

        public StackList<VMRuntimeExceptionHandler> ExceptionHandlers { get; } = new StackList<VMRuntimeExceptionHandler>();

        // Track instruction offset mappings for branch statements.
        // Item1 = start offset (IP).
        // Item2 = end offset (IP end after tracking).
        public Dictionary<int, Tuple<int, int>> InstructionOffsetMappings { get; } = new Dictionary<int, Tuple<int, int>>();

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
                // Reuse arg.
                data = uncompressedStream.ToArray();
                int track = 0;

                bool hasReturnValue = data.Skip(track).Take(1).ToArray()[0] == 1 ? true : false;
                track += 1;

                int numParams = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                track += 4;

                MethodSignatures.Push(new VMMethodSig
                {
                    HasReturnValue = hasReturnValue,
                    ReturnRegister = hasReturnValue ? RETURN_REGISTER : default, 
                    ParamCount = numParams
                });

                var mappingLength = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                track += 4;
                for (var i = 0; i < mappingLength; i++)
                {
                    // TODO: Remove this, how!?
                    //var methodIndex = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    //track += 4;

                    var mapKey = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;

                    var offset1 = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;

                    var offset2 = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;

                    InstructionOffsetMappings.Add(mapKey, new Tuple<int, int>(offset1, offset2));
                }
                Heap.Add(DATA, data.Skip(track).ToArray());
            }
            else
            {
                var mappingLength = BitConverter.ToInt32(data.Take(4).ToArray());
                var track = 4;
                for (var i = 0; i < mappingLength; i++) 
                {
                    //var methodIndex = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    //track += 4;

                    var mapKey = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;

                    var offset1 = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;

                    var offset2 = BitConverter.ToInt32(data.Skip(track).Take(4).ToArray());
                    track += 4;
                    
                    InstructionOffsetMappings.Add(mapKey, new Tuple<int, int>(offset1, offset2));
                }
                Heap.Add(DATA, data.Skip(track).ToArray());
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

            try
            {
                // Use the current param values.
                var track = OpCodeHandlers[opCode](this, Heap, operandValue, MethodSignatures.Peek().ParamValues);
                // TRACK MUST BE KEPT UP TO DATE. FAILURE TO DO THIS WILL LEAD TO CRASHES.
                if (track != operandLength)
                {
                    ip = track;
                }
                else
                {
                    ip += track;
                }
            }
            catch (Exception vmException)
            {
                var isHandled = false;

                while (ExceptionHandlers.Count > 0)
                {
                    var handler = ExceptionHandlers.Pop();

                    if (handler.Type == VMBlockType.Exception)
                    {
                        if (handler.ExceptionType != null && handler.ExceptionType.IsAssignableFrom(vmException.GetType()))
                        {
                            ActiveExceptionHandler = handler;

                            // Set IP to handler start
                            ip = handler.HandlerOffsetStart;

                            // We want to load the object into the heap for objects and make sure the exception handler
                            // knows what to do with the exception type object key.
                            ByteArrayKey handlerKey = new ByteArrayKey(handler.ExceptionTypeObjectKey);

                            if (!ObjectHeap.ContainsKey(handlerKey))
                            {
                                ObjectHeap.Add(handlerKey, vmException);
                            }
                            else
                            {
                                ObjectHeap[handlerKey] = vmException;
                            }

                            isHandled = true;

                            break;
                        }
                        else if (handler.ExceptionType == null)
                        {
                            ActiveExceptionHandler = handler;

                            // Set IP to handler start
                            ip = handler.HandlerOffsetStart;

                            isHandled = true;

                            break;
                        }
                    }
                }

                if (!isHandled)
                {
                    // We throw this to the callers, hope they have a plan! We don't! :3
                    throw;
                }
            }
        }

        internal void Run()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int ip = 0;

            // It is the first item, we know there is literally no others.
            MethodSignatures.Peek().PreviousIP = int.MinValue;
            MethodSignatures.Peek().ParamValues = Parameters;

            while (ip < Heap[DATA].Length)
            {
                Step(ref ip);
                if (ip == int.MinValue)
                {
                    // We have returned a value, or are wanting to return.
                    break;
                }
                Heap[INSTRUCTION_POINTER] = BitConverter.GetBytes(ip);
            }
            sw.Stop();

            Console.WriteLine(sw.ElapsedTicks);
        }

        internal int GetByteCountForDataType(DataType numType)
        {
            return numType switch
            {
                DataType.Int8 or DataType.UInt8 or DataType.Boolean => 1,
                DataType.Int16 or DataType.UInt16 => 2,
                DataType.Int32 or DataType.UInt32 or DataType.Single => 4,
                DataType.Int64 or DataType.UInt64 or DataType.Double => 8,
                //_ => throw new ArgumentOutOfRangeException(nameof(numType), $"Unsupported DataType: {numType}")
            };
        }

        internal byte[] ConvertParameter(DataType dataType, object paramData)
        {
            if (paramData.GetType() == typeof(byte[]))
            {
                return (byte[])paramData;
            }
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
        internal ComparatorType ReadComparatorType(byte[] data, ref int tracker)
        {
            return (ComparatorType)data[tracker++];
        }
        internal byte[] ReadBytes(byte[] data, ref int tracker, out int length)
        {
            length = BitConverter.ToInt32(data.Skip(tracker).Take(4).ToArray());
            tracker += 4;
            var res = data.Skip(tracker).Take(length).ToArray();
            tracker += length;
            return res;
        }

        internal byte[] PerformComparison(ComparatorType cType, DataType leftDataType, DataType rightDataType, byte[] left, byte[] right)
        {
            //if (leftDataType != rightDataType)
            //{
                // We cannot determine and should not determine the crazy amounts of options.
            var leftObj = GetNumberObject(leftDataType, left);
            var rightObj = GetNumberObject(rightDataType, right);
            dynamic leftObjD = leftObj;
            dynamic rightObjD = rightObj;

            // If one side is boolean but not both...
            if (leftDataType != rightDataType && (leftDataType == DataType.Boolean || rightDataType == DataType.Boolean))
            {
                if (leftDataType == DataType.Boolean)
                {
                    leftObjD = leftObjD ? 1 : 0;
                }
                else if (rightDataType == DataType.Boolean) 
                {
                    rightObjD = rightObjD ? 1 : 0;
                }
            }
            bool result = cType switch
            {
                ComparatorType.IsEqual => leftObjD == rightObjD,
                ComparatorType.IsNotEqual => leftObjD != rightObjD,
                ComparatorType.IsNotEqualUnsignedUnordered => leftObjD != rightObjD,
                ComparatorType.IsGreaterThan => leftObjD > rightObjD,
                ComparatorType.IsGreaterThanUnsignedUnordered => leftObjD > rightObjD,
                ComparatorType.IsGreaterThanOrEqual => leftObjD >= rightObjD,
                ComparatorType.IsGreaterThanOrEqualUnsignedUnordered => leftObjD >= rightObjD,
                ComparatorType.IsLessThan => leftObjD < rightObjD,
                ComparatorType.IsLessThanUnsignedUnordered => leftObjD < rightObjD,
                ComparatorType.IsLessThanOrEqual => leftObjD <= rightObjD,
                ComparatorType.IsLessThanOrEqualUnsignedUnordered => leftObjD <= rightObjD,
            };
            return [(byte)(result ? 1 : 0)];
            //}
            
            // TODO: Figure out a way that is independent of switch statements... Anon method? ILProcessor?
            
            //bool result = leftDataType switch
            //{
            //    DataType.Int32 => BitConverter.GetBytes(BitConverter.ToInt32(left) + BitConverter.ToInt32(right)),
            //    DataType.UInt32 => BitConverter.GetBytes(BitConverter.ToUInt32(left) + BitConverter.ToUInt32(right)),
            //    DataType.Int64 => BitConverter.GetBytes(BitConverter.ToInt64(left) + BitConverter.ToInt64(right)),
            //    DataType.UInt64 => BitConverter.GetBytes(BitConverter.ToUInt64(left) + BitConverter.ToUInt64(right)),
            //    DataType.Int8 => BitConverter.GetBytes((sbyte)left[0] + (sbyte)right[0]), // Assuming sbyte for Int8
            //    DataType.UInt8 => BitConverter.GetBytes(left[0] + right[0]),
            //    DataType.Int16 => BitConverter.GetBytes(BitConverter.ToInt16(left) + BitConverter.ToInt16(right)),
            //    DataType.UInt16 => BitConverter.GetBytes(BitConverter.ToUInt16(left) + BitConverter.ToUInt16(right)),
            //    DataType.Single => BitConverter.GetBytes(BitConverter.ToSingle(left) + BitConverter.ToSingle(right)),
            //    DataType.Double => BitConverter.GetBytes(BitConverter.ToDouble(left) + BitConverter.ToDouble(right)),
            //    //_ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            //};
            //return [(byte)(result ? 1 : 0)];
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
                DataType.Double => BitConverter.ToDouble(val),
                DataType.Boolean => (bool)(val[0] == 1 ? true : false),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported DataType: {dataType}")
            };
        }

        internal byte[] GetReturnRegister()
        {
            return Heap[RETURN_REGISTER];
        }
    }
}
