using RegiVM.VMRuntime.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMRuntime
{
    public delegate int ActionDelegate(RegiVMRuntime t, Dictionary<ByteArrayKey, byte[]> h, byte[] d,
                                  Dictionary<int, object> p);

    public class FuncDictionary<TKey>
    {
        private byte[] _data;
        private int _capacity;
        private int _currentSize;
        private int _lastOffset;
        public unsafe FuncDictionary(int capacity)
        {
            _capacity = capacity;
            _currentSize = capacity * 12;
            _data = new byte[_currentSize];
            _lastOffset = 0;
        }

        private void Grow(int numToAdd)
        {
            _capacity++;
            _currentSize += (numToAdd * 12);
            Array.Resize(ref _data, _currentSize);
        }

        public void Add(TKey key, ActionDelegate del)
        {
            Random r = new(key!.GetHashCode());

            nint ptrToDel = Marshal.GetFunctionPointerForDelegate(del);
            byte[] dataToAdd = [0xEF, 0xFF, 0xEF, 0xF0, .. BitConverter.GetBytes(ptrToDel.ToInt64())];
            byte[] keyBytes = new byte[12];
            r.NextBytes(keyBytes);
            try
            {
                // Encode.
                for (int i = 0; i < dataToAdd.Length; i++)
                {
                    dataToAdd[i] ^= (byte)(keyBytes[i] + r.Next(254));
                }
                Array.Copy(dataToAdd, 0, _data, _lastOffset, 12);
            }
            catch (ArgumentException)
            {
                // Grow.
                Grow(1);
                Array.Copy(dataToAdd, 0, _data, _lastOffset, 12);
            }
            _lastOffset += 12;
        }

        public ActionDelegate this[TKey key] { get => Get(key); }

        public ActionDelegate Get(TKey key)
        {
            // Capacity is raw number of.
            int triesMax = _capacity;
            int tries = 0;
            while (tries < triesMax)
            {
                Random r = new(key.GetHashCode());
                byte[] keyBytes = new byte[12];
                r.NextBytes(keyBytes);

                // Copy only header.
                byte[] tmp = new byte[4];
                Array.Copy(_data, tries * 12, tmp, 0, 4);

                // Decode.
                for (int i = 0; i < tmp.Length; i++)
                {
                    tmp[i] ^= (byte)(keyBytes[i] + r.Next(254));
                }

                // If header signature is matched.
                if (tmp[0] != 0xEF || tmp[1] != 0xFF || tmp[2] != 0xEF || tmp[3] != 0xF0)
                {
                    tries++;
                    continue;
                }
               
                // Proper copy + decode.
                byte[] encodedData = new byte[8];
                Array.Copy(_data, (tries * 12) + 4, encodedData, 0, 8);
                for (int i = 0; i < encodedData.Length; i++)
                {
                    encodedData[i] ^= (byte)(keyBytes[i + 4] + r.Next(254));
                }

                return Marshal.GetDelegateForFunctionPointer<ActionDelegate>(new nint(BitConverter.ToInt64(encodedData, 0)));
            }
            return default!;
        }

        //protected virtual void Dispose(bool disposing)
        //{
        //    if (!_disposedValue)
        //    {
        //        if (disposing)
        //        {
        //            // TODO: dispose managed state (managed objects)
        //        }
        //        Marshal.FreeHGlobal(_dataPtr);
        //        _disposedValue = true;
        //    }
        //}

        //~ActionDictionary()
        //{
        //    Dispose(disposing: false);
        //}

        //public void Dispose()
        //{
        //    Dispose(disposing: true);
        //    GC.SuppressFinalize(this);
        //}
    }
}
