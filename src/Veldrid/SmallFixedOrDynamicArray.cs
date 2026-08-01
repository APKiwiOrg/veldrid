using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Veldrid
{
    internal unsafe struct SmallFixedOrDynamicArray : IDisposable
    {
        private const int MaxFixedValues = 5;

        public readonly uint Count;
        private fixed uint FixedData[MaxFixedValues];
        public readonly uint[] Data;

        public uint Get(uint i) => Count > MaxFixedValues ? Data[i] : FixedData[i];

        public SmallFixedOrDynamicArray(uint count, ref uint data)
        {
            if (count > MaxFixedValues)
            {
                // A rented array carries whatever the previous renter left in it, so the values have to be
                // copied in the same way the fixed buffer copies them. Reading Get(i) off an uncopied rent
                // returned pool garbage, and BoundResourceSetInfo.Equals compared that garbage.
                uint[] rented = ArrayPool<uint>.Shared.Rent((int)count);

                for (int i = 0; i < count; i++)
                {
                    rented[i] = Unsafe.Add(ref data, i);
                }

                Data = rented;
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    FixedData[i] = Unsafe.Add(ref data, i);
                }

                Data = null;
            }

            Count = count;
        }

        public void Dispose()
        {
            if (Data != null) { ArrayPool<uint>.Shared.Return(Data); }
        }
    }
}
