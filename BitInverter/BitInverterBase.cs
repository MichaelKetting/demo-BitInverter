﻿using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;

namespace BitInverter;

public class BitInverterBase
{
  private static readonly Vector64<byte> byteShuffler = Vector64.Create((byte)7, 6, 5, 4, 3, 2, 1, 0);

  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert (ulong input)
  {
    ulong output = 0;
    for (int i = 0; i < 64; i++)
    {
      ulong currentBitMask = 1ul << i;
      bool currentBit = (currentBitMask & input)!= 0;
      output = output << 1;
      if (currentBit)
      {
        output = output | 0x_00_00_00_00_0_00_00_01;
      }
    }

    //1: 1011  -> bitmask 0001 o = 0000 | 0001
    //2: 1011  -> bitmask 0010 o = 0010 | 0001
    //2: 1011  -> bitmask 0100 o = 0110
    //2: 1011  -> bitmask 1000 o = 1100 | 0001


    return output;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v02_No_Branch (ulong input)
  {
    ulong output = 0;
    ulong remainingBits = input;
    for (int i = 0; i < 64; i++)
    {
      var currentBit = remainingBits & 0x1;
      remainingBits = remainingBits >> 1;

      var tempOutput = output << 1;
      output = tempOutput | currentBit;
    }

    //1: 1011 & 0001 -> currentBit = 0001, o = 0000 | 0001
    //2: 0101 & 0001 -> currentBit = 0001, o = 0010 | 0001
    //2: 0010 & 0001 -> currentBit = 0000, o = 0110 | 0000
    //2: 0001 & 0001 -> currentBit = 0001, o = 1100 | 0001

    return output;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v03_Log2 (ulong input)
  {
    ulong left;
    ulong right;
    ulong result = input;

    left = result  & 0x_AA_AA_AA_AA_AA_AA_AA_AA; // AA = 10101010
    right = result & 0x_55_55_55_55_55_55_55_55; // 55 = 01010101
    left = left >> 1;
    right = right << 1;
    result = left | right;

    left = result  & 0x_CC_CC_CC_CC_CC_CC_CC_CC; // CC = 11001100
    right = result & 0x_33_33_33_33_33_33_33_33; // 33 = 00110011
    left = left >> 2;
    right = right << 2;
    result = left | right;

    left = result  & 0x_F0_F0_F0_F0_F0_F0_F0_F0; // F0 = 11110000
    right = result & 0x_0F_0F_0F_0F_0F_0F_0F_0F; // 0F = 00001111
    left = left >> 4;
    right = right << 4;
    result = left | right;

    left = result  & 0x_FF_00_FF_00_FF_00_FF_00;
    right = result & 0x_00_FF_00_FF_00_FF_00_FF;
    left = left >> 8;
    right = right << 8;
    result = left | right;

    left = result  & 0x_FF_FF_00_00_FF_FF_00_00;
    right = result & 0x_00_00_FF_FF_00_00_FF_FF;
    left = left >> 16;
    right = right << 16;
    result = left | right;

    left = result;//  & 0x_FF_FF_FF_FF_00_00_00_00; // implicit zero on shift
    right = result;// & 0x_00_00_00_00_FF_FF_FF_FF; // implicit zero on shift
    left = left >> 32;
    right = right << 32;
    result = left | right;

    return result;
  }


  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v03a_Log2_XOR (ulong input)
  {
    ulong left;
    ulong right;
    ulong result = input;

    left = result  & 0x_AA_AA_AA_AA_AA_AA_AA_AA; // AA = 10101010
    right = result & 0x_55_55_55_55_55_55_55_55; // 55 = 01010101
    left = left >> 1;
    right = right << 1;
    result = left ^ right;

    left = result  & 0x_CC_CC_CC_CC_CC_CC_CC_CC; // CC = 11001100
    right = result & 0x_33_33_33_33_33_33_33_33; // 33 = 00110011
    left = left >> 2;
    right = right << 2;
    result = left ^ right;

    left = result  & 0x_F0_F0_F0_F0_F0_F0_F0_F0; // F0 = 11110000
    right = result & 0x_0F_0F_0F_0F_0F_0F_0F_0F; // 0F = 00001111
    left = left >> 4;
    right = right << 4;
    result = left ^ right;

    left = result  & 0x_FF_00_FF_00_FF_00_FF_00;
    right = result & 0x_00_FF_00_FF_00_FF_00_FF;
    left = left >> 8;
    right = right << 8;
    result = left | right;

    left = result  & 0x_FF_FF_00_00_FF_FF_00_00;
    right = result & 0x_00_00_FF_FF_00_00_FF_FF;
    left = left >> 16;
    right = right << 16;
    result = left ^ right;

    left = result;//  & 0x_FF_FF_FF_FF_00_00_00_00; // implicit zero on shift
    right = result;// & 0x_00_00_00_00_FF_FF_FF_FF; // implicit zero on shift
    left = left >> 32;
    right = right << 32;
    result = left ^ right;

    return result;
  }


  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v03b_Log2_Compact (ulong input)
  {
    // NOTE: keeping the OR operations separate from the SHIFT results in fewer statements and thus better performance.
    // NOTE: the present form results in the same ASM code as the non-compact form. 

    ulong left;
    ulong right;
    ulong result = input;

    left = (result  & 0x_AA_AA_AA_AA_AA_AA_AA_AA) >> 1;
    right = (result & 0x_55_55_55_55_55_55_55_55) << 1;
    result = left | right;

    left = (result  & 0x_CC_CC_CC_CC_CC_CC_CC_CC) >> 2;
    right = (result & 0x_33_33_33_33_33_33_33_33) << 2;
    result = left | right;

    left = (result  & 0x_F0_F0_F0_F0_F0_F0_F0_F0) >> 4;
    right = (result & 0x_0F_0F_0F_0F_0F_0F_0F_0F) << 4;
    result = left | right;

    left = (result  & 0x_FF_00_FF_00_FF_00_FF_00) >> 8;
    right = (result & 0x_00_FF_00_FF_00_FF_00_FF) << 8;
    result = left | right;

    left = (result  & 0x_FF_FF_00_00_FF_FF_00_00) >> 16;
    right = (result & 0x_00_00_FF_FF_00_00_FF_FF) << 16;
    result = left | right;

    left = result >> 32;
    right = result << 32;
    result = left | right;

    return result;
  }


  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v04_Log2_ReverseEndianness (ulong input)
  {
    ulong left;
    ulong right;
    ulong result = input;

    left = result & 0x_AA_AA_AA_AA_AA_AA_AA_AA; // AA = 10101010
    right = result & 0x_55_55_55_55_55_55_55_55; // 55 = 01010101
    left = left >> 1;
    right = right << 1;
    result = left ^ right;

    left = result & 0x_CC_CC_CC_CC_CC_CC_CC_CC; // CC = 11001100
    right = result & 0x_33_33_33_33_33_33_33_33; // 33 = 00110011
    left = left >> 2;
    right = right << 2;
    result = left ^ right;

    left = result & 0x_F0_F0_F0_F0_F0_F0_F0_F0; // F0 = 11110000
    right = result & 0x_0F_0F_0F_0F_0F_0F_0F_0F; // 0F = 00001111
    left = left >> 4;
    right = right << 4;
    result = left ^ right;

    result = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(result);

    return result;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  public ulong Invert_v05 (ulong input)
  {
    ulong left;
    ulong right;
    ulong result = input;

    left = result  & 0x_AA_AA_AA_AA_AA_AA_AA_AA; // AA = 10101010
    right = result & 0x_55_55_55_55_55_55_55_55; // 55 = 01010101
    left = left >> 1;
    right = right << 1;
    result = left ^ right;

    left = result  & 0x_CC_CC_CC_CC_CC_CC_CC_CC; // CC = 11001100
    right = result & 0x_33_33_33_33_33_33_33_33; // 33 = 00110011
    left = left >> 2;
    right = right << 2;
    result = left ^ right;

    left = result  & 0x_F0_F0_F0_F0_F0_F0_F0_F0; // F0 = 11110000
    right = result & 0x_0F_0F_0F_0F_0F_0F_0F_0F; // 0F = 00001111
    left = left >> 4;
    right = right << 4;
    result = left ^ right;

    var byteVector = Vector128.Create(result).AsByte();

    var vectorOutput = Vector128.Shuffle(byteVector, Vector128.Create(0x00_01_02_03_04_05_06_07).AsByte());

    return vectorOutput.AsUInt64()[0];
  }
}