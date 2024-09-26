
namespace RegiVM.VMRuntime.Handlers
{
    public class VMMethodSig
    {
        public int PreviousIP { get; set; }
        public int ParamCount { get; set; }
        public Dictionary<int, object> ParamValues { get; set; }
        public bool HasReturnValue { get; set; }
        public ByteArrayKey ReturnRegister { get; set; }
    }
}