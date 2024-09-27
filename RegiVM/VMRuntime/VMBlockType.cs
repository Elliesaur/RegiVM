namespace RegiVM.VMRuntime
{
    public enum VMBlockType : byte
    {
        Exception = 0x0,
        Filter = 0x1,
        Finally = 0x2,
        Fault = 0x3,
        Protected = 0x4
    }
}
