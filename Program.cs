using AsmResolver.DotNet;
using RegiVM.VMBuilder;
using RegiVM.VMRuntime;
using RegiVM.VMRuntime.Handlers;

namespace RegiVM
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Math7");
            //var testMd = new MethodDefinition("IDGAF", MethodAttributes.Public, new MethodSignature(CallingConventionAttributes.Default, module.CorLibTypeFactory.Int32, new List<TypeSignature>()));
            //testMd.CilMethodBody = new CilMethodBody(testMd);
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(1));
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(2));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Add));
            //testMd.CilMethodBody.Instructions.Add(CilInstruction.CreateLdcI4(3));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Add));
            //testMd.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
            //testMd.CilMethodBody.Instructions.CalculateOffsets();
            //testMd.CilMethodBody.ComputeMaxStack();

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .RegisterLimit(30);
                //.RandomizeRegisterNames();
            byte[] data = compiler.Compile(testMd);

            Console.WriteLine($"Sizeof Data {data.Length} bytes");

            // Data for parameters is passed here.
            RegiVMRuntime vm = new RegiVMRuntime(true, data, 50, 60);
            vm.OpCodeHandlers.Add(compiler.OpCodes.NumberLoad, InstructionHandlers.NumberLoad);
            vm.OpCodeHandlers.Add(compiler.OpCodes.ParameterLoad, InstructionHandlers.ParameterLoad);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Add, InstructionHandlers.Add);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Sub, InstructionHandlers.Sub);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Mul, InstructionHandlers.Mul);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Div, InstructionHandlers.Div);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Xor, InstructionHandlers.Xor);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Or, InstructionHandlers.Or);
            vm.OpCodeHandlers.Add(compiler.OpCodes.And, InstructionHandlers.And);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Ret, InstructionHandlers.Return);
            vm.OpCodeHandlers.Add(compiler.OpCodes.LoadOrStoreRegister, InstructionHandlers.LoadOrStoreRegister);
            vm.OpCodeHandlers.Add(compiler.OpCodes.JumpBool, InstructionHandlers.JumpBool);
            vm.OpCodeHandlers.Add(compiler.OpCodes.StartRegionBlock, InstructionHandlers.StartRegionBlock);
            vm.OpCodeHandlers.Add(compiler.OpCodes.EndFinally, InstructionHandlers.Endfinally);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Comparator, InstructionHandlers.Comparator);

            var actualResult = TestProgram.Math7(50, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            
            var reg = vm.GetReturnRegister();
            Console.WriteLine(BitConverter.ToInt32(reg));

            Console.ReadKey();
        }
    }
}
