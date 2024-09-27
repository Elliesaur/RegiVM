namespace RegiVM.VMRuntime.Handlers
{
    public class VMRuntimeExceptionHandler
    {
        public VMBlockType Type;
        public int HandlerOffsetStart;
        public int FilterOffsetStart;
        public Type ExceptionType;
        public byte[] ExceptionTypeObjectKey;
        public int LeaveInstOffset;
        public int Id;
    }
}
