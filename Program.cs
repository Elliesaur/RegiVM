using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
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
            var sigForBlock = targetBlock.GetMethodSignatureForBlock(testMd, false);

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .Encrypt(true)
                .RegisterLimit(30);
            //.RandomizeRegisterNames();
            //byte[] data = compiler.Compile(testMd);
            
            var insts = targetBlock.Instructions;
            var retInst = new AsmResolver.PE.DotNet.Cil.CilInstruction(AsmResolver.PE.DotNet.Cil.CilOpCodes.Ret);
            retInst.Offset = insts.Last().Offset + insts.Last().Size;
            insts.Add(retInst);

            // Fake a new method.
            var newMethodDef = new MethodDefinition("SINGLE BLOCK BODY",
                testMd.Attributes,
                sigForBlock);
            var newMethodBody = new CilMethodBody(newMethodDef);
            foreach (var lv in testMd.CilMethodBody.LocalVariables)
            {
                newMethodBody.LocalVariables.Add(new CilLocalVariable(lv.VariableType));
            }
            newMethodBody.Instructions.AddRange(insts);
            newMethodBody.Instructions.CalculateOffsets();

            byte[] data = compiler.Compile(insts, testMd.CilMethodBody!.ExceptionHandlers, newMethodBody, testMd);
            ulong[] usedOpCodes = compiler.GetUsedOpCodes();
            var opCodesWithNames = compiler.OpCodes.GetAllOpCodesWithNames();

            Console.WriteLine($"Sizeof Data {data.Length} bytes");

            // Data for parameters is passed here.
            // In reality the opcode handlers will be randomly chosen and inserted at compile time to the IL.
            // This will not be static as shown below.
            RegiVMRuntime vm = new RegiVMRuntime(true, data, 10, 60);

            foreach (var (opCode, name) in opCodesWithNames)
            {
                var associatedMethod = typeof(InstructionHandlers).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).First(x => x.Name == name);
                FuncDelegate del = (FuncDelegate)associatedMethod!.CreateDelegate(typeof(FuncDelegate));
                vm.OpCodeHandlers.Add(opCode, del);
            }

            //vm.OpCodeHandlers.Add(compiler.OpCodes.NumberLoad, InstructionHandlers.NumberLoad);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.ParameterLoad, InstructionHandlers.ParameterLoad);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Add, InstructionHandlers.Add);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Sub, InstructionHandlers.Sub);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Mul, InstructionHandlers.Mul);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Div, InstructionHandlers.Div);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Xor, InstructionHandlers.Xor);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Or, InstructionHandlers.Or);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.And, InstructionHandlers.And);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Ret, InstructionHandlers.Return);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.LoadOrStoreRegister, InstructionHandlers.LoadOrStoreRegister);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.JumpBool, InstructionHandlers.JumpBool);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.StartRegionBlock, InstructionHandlers.StartRegionBlock);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.EndFinally, InstructionHandlers.Endfinally);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Comparator, InstructionHandlers.Comparator);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.JumpCall, InstructionHandlers.JumpCall);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.Duplicate, InstructionHandlers.DuplicateRegister);
            //vm.OpCodeHandlers.Add(compiler.OpCodes.ConvertNumber, InstructionHandlers.ConvertNumber);


            var actualResult = TestProgram.Call1(10, 60);
            Console.WriteLine(actualResult);

            vm.Run();
            try
            {
                var reg = vm.GetReturnRegister();
                Console.WriteLine(BitConverter.ToInt32(reg));
            }
            catch (Exception)
            {
                Console.WriteLine("No return register present!");
            }
           
            Console.ReadKey();
        }
    }
}
