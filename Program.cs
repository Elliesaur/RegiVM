using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using RegiVM.VMBuilder;
using RegiVM.VMRuntime;
using RegiVM.VMRuntime.Handlers;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace RegiVM
{
    public static class Program
    {
        internal static ByteArrayKey GetKey(this ulong val)
        {
            return new ByteArrayKey(BitConverter.GetBytes(val));
        }

        public static void Main(string[] args)
        {
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Call1");
            // This MUST be done prior to anything else.
            testMd.CilMethodBody!.Instructions.ExpandMacros();

            var (testBlocks, _, _) = testMd.GetGraphsAndBlocks();

            var blocks = testBlocks.GetAllBlocks();
            var targetBlock = blocks.First();
            var sigForBlock = new MethodSignature(
                CallingConventionAttributes.Default, 
                module.CorLibTypeFactory.Void, 
                [module.CorLibTypeFactory.Int32]);

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .RegisterLimit(30);
            //.RandomizeRegisterNames();
            //byte[] data = compiler.Compile(testMd);

            byte[] data = compiler.Compile(targetBlock.Instructions, testMd.CilMethodBody!.ExceptionHandlers, testMd.CilMethodBody!);

            Console.WriteLine($"Sizeof Data {data.Length} bytes");

            // Data for parameters is passed here.
            // In reality the opcode handlers will be randomly chosen and inserted at compile time to the IL.
            // This will not be static as shown below.
            RegiVMRuntime vm = new RegiVMRuntime(true, data, 10, 60);
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
            vm.OpCodeHandlers.Add(compiler.OpCodes.JumpCall, InstructionHandlers.JumpCall);
            vm.OpCodeHandlers.Add(compiler.OpCodes.Duplicate, InstructionHandlers.DuplicateRegister);
            vm.OpCodeHandlers.Add(compiler.OpCodes.ConvertNumber, InstructionHandlers.ConvertNumber);


            var actualResult = TestProgram.Call1(10, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            
            var reg = vm.GetReturnRegister();
            Console.WriteLine(BitConverter.ToInt32(reg));

            Console.ReadKey();
        }
    }
}
