namespace RegiVM.VMRuntime
{
    public enum DataType : byte
    {
        Unknown = 0,

        Int32 = 0x1,
        UInt32 = 0x2,

        Int64 = 0x3,
        UInt64 = 0x4,

        Single = 0x5,
        Double = 0x6,

        Int8 = 0x7,
        UInt8 = 0x8,

        Int16 = 0x9,
        UInt16 = 0x10,

        Boolean = 0x12,

        Phi = 0x13
    }
}
