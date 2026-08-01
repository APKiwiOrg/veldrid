using System;
using System.Buffers;
using Xunit;

namespace Veldrid.Tests
{
    // Pure CPU. No graphics device and no backend symbol, so this class compiles and runs in every
    // configuration the test project is built in.
    public class SmallFixedOrDynamicArrayTests
    {
        private const uint Poison = 0xDEADBEEF;

        [Theory]
        [InlineData(1u)]
        [InlineData(5u)]  // the fixed-buffer capacity, the last count that stays inline
        [InlineData(6u)]  // the first count that rents
        [InlineData(9u)]
        [InlineData(64u)]
        public void GetReturnsTheValuesItWasConstructedWith(uint count)
        {
            uint[] values = MakeOffsets(count);
            PoisonPool(count);

            SmallFixedOrDynamicArray array = new SmallFixedOrDynamicArray(count, ref values[0]);

            try
            {
                Assert.Equal(count, array.Count);

                for (uint i = 0; i < count; i++)
                {
                    Assert.Equal(values[i], array.Get(i));
                }
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public void EqualityHoldsForIdenticalOffsetsAboveTheFixedCapacity()
        {
            const uint Count = 9;
            uint[] values = MakeOffsets(Count);

            PoisonPool(Count);
            BoundResourceSetInfo left = new BoundResourceSetInfo(null, Count, ref values[0]);
            PoisonPool(Count);
            BoundResourceSetInfo right = new BoundResourceSetInfo(null, Count, ref values[0]);

            try
            {
                Assert.True(left.Equals(right));
                Assert.True(left.Equals(null, Count, ref values[0]));

                uint[] changed = MakeOffsets(Count);
                changed[Count - 1] += 1;
                Assert.False(left.Equals(null, Count, ref changed[0]));
            }
            finally
            {
                left.Offsets.Dispose();
                right.Offsets.Dispose();
            }
        }

        private static uint[] MakeOffsets(uint count)
        {
            uint[] values = new uint[count];

            for (uint i = 0; i < count; i++)
            {
                values[i] = 0x1000u + (i * 0x40u);
            }

            return values;
        }

        // Above the fixed capacity the values live in a pooled array, and a pool that has never been used
        // hands back a zeroed one. Zero is a plausible dynamic offset, so a constructor that forgot to copy
        // would still look correct. Filling the bucket with a sentinel first removes that cover: ArrayPool
        // gives a returned array straight back to the same thread, so the constructor under test rents a
        // poisoned one.
        private static void PoisonPool(uint count)
        {
            const int Depth = 4;
            uint[][] rented = new uint[Depth][];

            for (int i = 0; i < Depth; i++)
            {
                rented[i] = ArrayPool<uint>.Shared.Rent((int)count);
                rented[i].AsSpan().Fill(Poison);
            }

            for (int i = 0; i < Depth; i++)
            {
                ArrayPool<uint>.Shared.Return(rented[i]);
            }
        }
    }
}
