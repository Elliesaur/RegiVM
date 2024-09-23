using AsmResolver.DotNet;
using RegiVM.VMBuilder;
using RegiVM.VMRuntime;
using RegiVM.VMRuntime.Handlers;
using System.Linq.Expressions;

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
            //var ctorInfo = typeof(InstanceCreationTest).GetConstructors()[0];
            //var mdToken = ctorInfo.MetadataToken;

            //var ctorParam = Expression.Parameter(typeof(int));
            //var exprNew = Expression.New(
            //    (System.Reflection.ConstructorInfo)typeof(Program).Module.ResolveMethod(mdToken)!, 
            //    [ctorParam]);
            //var lambda = Expression.Lambda(exprNew, [ctorParam]).Compile();
            //var instanceOfClass = lambda.DynamicInvoke(123);

            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //ActionDictionary<ulong> hello = new ActionDictionary<ulong>(10);
            //hello.Add(1, InstructionHandlers.Add);
            //hello.Add(2, InstructionHandlers.Sub);
            //hello.Add(3, InstructionHandlers.Mul);
            //hello.Add(4, InstructionHandlers.Div);
            //hello.Add(5, InstructionHandlers.JumpBool);
            //hello.Add(6, InstructionHandlers.Endfinally);
            //hello.Add(7, InstructionHandlers.Comparator);
            //hello.Add(8, InstructionHandlers.LoadOrStoreRegister);
            //hello.Add(9, InstructionHandlers.Xor);
            //hello.Add(10, InstructionHandlers.And);
            //hello.Add(11, InstructionHandlers.Or);
            //sw.Stop();
            //Console.WriteLine(sw.ToString());
            //sw.Reset();
            //sw.Start();
            //ActionDelegate test = hello[10];
            //sw.Stop();
            //Console.WriteLine(sw.ToString());

            //return;
            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Call1");

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .RegisterLimit(30);
                //.RandomizeRegisterNames();
            byte[] data = compiler.Compile(testMd);

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
