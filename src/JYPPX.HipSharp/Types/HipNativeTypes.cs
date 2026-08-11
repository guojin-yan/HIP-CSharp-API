using System;
using System.Runtime.InteropServices;

namespace JYPPX.HipSharp.Types;

/// <summary>表示按值传递的原生 <c>dim3</c> / Represents the native by-value <c>dim3</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipDim3
{
    /// <summary>创建三维尺寸 / Creates a three-dimensional size.</summary>
    public HipDim3(uint x, uint y = 1, uint z = 1)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>X 维度 / X dimension.</summary>
    public uint X { get; }

    /// <summary>Y 维度 / Y dimension.</summary>
    public uint Y { get; }

    /// <summary>Z 维度 / Z dimension.</summary>
    public uint Z { get; }
}

/// <summary>表示按值传递的原生 <c>hipExtent</c> / Represents the native by-value <c>hipExtent</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipExtent
{
    /// <summary>创建三维范围 / Creates a three-dimensional extent.</summary>
    public HipExtent(UIntPtr width, UIntPtr height, UIntPtr depth)
    {
        Width = width;
        Height = height;
        Depth = depth;
    }

    /// <summary>宽度 / Width.</summary>
    public UIntPtr Width { get; }

    /// <summary>高度 / Height.</summary>
    public UIntPtr Height { get; }

    /// <summary>深度 / Depth.</summary>
    public UIntPtr Depth { get; }
}

/// <summary>表示按值传递的原生 <c>hipPitchedPtr</c> / Represents the native by-value <c>hipPitchedPtr</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipPitchedPtr
{
    /// <summary>创建 pitched pointer 描述 / Creates a pitched-pointer descriptor.</summary>
    public HipPitchedPtr(IntPtr address, UIntPtr pitch, UIntPtr xSize, UIntPtr ySize)
    {
        Address = address;
        Pitch = pitch;
        XSize = xSize;
        YSize = ySize;
    }

    /// <summary>内存地址 / Memory address.</summary>
    public IntPtr Address { get; }

    /// <summary>行跨度 / Row pitch.</summary>
    public UIntPtr Pitch { get; }

    /// <summary>逻辑宽度 / Logical width.</summary>
    public UIntPtr XSize { get; }

    /// <summary>逻辑高度 / Logical height.</summary>
    public UIntPtr YSize { get; }
}

/// <summary>表示按值传递的原生 <c>hipMemLocation</c> / Represents the native by-value <c>hipMemLocation</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipMemLocation
{
    /// <summary>创建内存位置 / Creates a memory location.</summary>
    public HipMemLocation(int type, int id)
    {
        Type = type;
        Id = id;
    }

    /// <summary>原生 <c>hipMemLocationType</c> 数值 / Native <c>hipMemLocationType</c> value.</summary>
    public int Type { get; }

    /// <summary>位置标识 / Location identifier.</summary>
    public int Id { get; }
}

/// <summary>表示 64 字节的原生 <c>hipIpcMemHandle_t</c> / Represents the 64-byte native <c>hipIpcMemHandle_t</c>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HipIpcMemHandle
{
    /// <summary>字节 0-7 / Bytes 0-7.</summary>
    public ulong Data0;
    /// <summary>字节 8-15 / Bytes 8-15.</summary>
    public ulong Data1;
    /// <summary>字节 16-23 / Bytes 16-23.</summary>
    public ulong Data2;
    /// <summary>字节 24-31 / Bytes 24-31.</summary>
    public ulong Data3;
    /// <summary>字节 32-39 / Bytes 32-39.</summary>
    public ulong Data4;
    /// <summary>字节 40-47 / Bytes 40-47.</summary>
    public ulong Data5;
    /// <summary>字节 48-55 / Bytes 48-55.</summary>
    public ulong Data6;
    /// <summary>字节 56-63 / Bytes 56-63.</summary>
    public ulong Data7;
}

/// <summary>表示 64 字节的原生 <c>hipIpcEventHandle_t</c> / Represents the 64-byte native <c>hipIpcEventHandle_t</c>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HipIpcEventHandle
{
    /// <summary>字节 0-7 / Bytes 0-7.</summary>
    public ulong Data0;
    /// <summary>字节 8-15 / Bytes 8-15.</summary>
    public ulong Data1;
    /// <summary>字节 16-23 / Bytes 16-23.</summary>
    public ulong Data2;
    /// <summary>字节 24-31 / Bytes 24-31.</summary>
    public ulong Data3;
    /// <summary>字节 32-39 / Bytes 32-39.</summary>
    public ulong Data4;
    /// <summary>字节 40-47 / Bytes 40-47.</summary>
    public ulong Data5;
    /// <summary>字节 48-55 / Bytes 48-55.</summary>
    public ulong Data6;
    /// <summary>字节 56-63 / Bytes 56-63.</summary>
    public ulong Data7;
}
