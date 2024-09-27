namespace RegiVM.VMRuntime
{
    public enum ComparatorType : byte
    {
        IsEqual = 0x1,
        IsNotEqual = 0x2,
        IsNotEqualUnsignedUnordered = 0x12,

        IsGreaterThan = 0x3,
        IsGreaterThanUnsignedUnordered = 0x13,

        IsLessThan = 0x4,
        IsLessThanUnsignedUnordered = 0x14,

        IsGreaterThanOrEqual = 0x5,
        IsGreaterThanOrEqualUnsignedUnordered = 0x15,

        IsLessThanOrEqual = 0x6,
        IsLessThanOrEqualUnsignedUnordered = 0x16,
    }
}
