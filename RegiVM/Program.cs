using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using ProwlynxNET.Core;
using ProwlynxNET.Core.Services.Argument;
using RegiVM.ObfuscationEngine;
using RegiVM.VMBuilder;
using RegiVM.VMRuntime;
using RegiVM.VMRuntime.Handlers;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TestVMApp;

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
            PerformObfuscationTask(args);

            return;

            ModuleDefinition module = ModuleDefinition.FromModule(typeof(TestProgram).Module);

            var testType = module.GetAllTypes().First(x => x.Name == typeof(TestProgram).Name);
            var testMd = testType.Methods.First(x => x.Name == "Main");
            // This MUST be done prior to anything else.
            testMd.CilMethodBody!.Instructions.ExpandMacros();

            var (testBlocks, _, _) = testMd.GetGraphsAndBlocks();

            var blocks = testBlocks.GetAllBlocks();
            var targetBlock = blocks.First();

            var compiler = new VMCompiler()
                .RandomizeOpCodes()
                .Encrypt(false)
                .Compress(true)
                .RegisterLimit(30);
            //.RandomizeRegisterNames();
            //byte[] data = compiler.Compile(testMd);

            byte[] data = compiler.Compile(testMd);
            ulong[] usedOpCodes = compiler.GetUsedOpCodes();
            var opCodesWithNames = compiler.OpCodes.GetAllOpCodesWithNames();

            Console.WriteLine($"Sizeof Data {data.Length} bytes");

            // Data for parameters is passed here.
            // In reality the opcode handlers will be randomly chosen and inserted at compile time to the IL.
            // This will not be static as shown below.
            RegiVMRuntime vm = new RegiVMRuntime(true, data, 10, 60);

            foreach (var (opCode, name) in opCodesWithNames.Shuffle())
            {
                FuncDelegate del = null!;
                if (compiler.OpCodes.NumberLoad == opCode)
                {
                    del = InstructionHandlers.NumberLoad;
                }
                else if (compiler.OpCodes.ParameterLoad == opCode)
                {
                    del = InstructionHandlers.ParameterLoad;
                }
                else if (compiler.OpCodes.Add == opCode)
                {
                    del = InstructionHandlers.Add;
                }
                else if (compiler.OpCodes.Sub == opCode)
                {
                    del = InstructionHandlers.Sub;
                }
                else if (compiler.OpCodes.Mul == opCode)
                {
                    del = InstructionHandlers.Mul;
                }
                else if (compiler.OpCodes.Div == opCode)
                {
                    del = InstructionHandlers.Div;
                }
                else if (compiler.OpCodes.Xor == opCode)
                {
                    del = InstructionHandlers.Xor;
                }
                else if (compiler.OpCodes.Or == opCode)
                {
                    del = InstructionHandlers.Or;
                }
                else if (compiler.OpCodes.And == opCode)
                {
                    del = InstructionHandlers.And;
                }
                else if (compiler.OpCodes.Ret == opCode)
                {
                    del = InstructionHandlers.Ret;
                }
                else if (compiler.OpCodes.LoadOrStoreRegister == opCode)
                {
                    del = InstructionHandlers.LoadOrStoreRegister;
                }
                else if (compiler.OpCodes.JumpBool == opCode)
                {
                    del = InstructionHandlers.JumpBool;
                }
                else if (compiler.OpCodes.StartRegionBlock == opCode)
                {
                    del = InstructionHandlers.StartRegionBlock;
                }
                else if (compiler.OpCodes.EndFinally == opCode)
                {
                    del = InstructionHandlers.EndFinally;
                }
                else if (compiler.OpCodes.Comparator == opCode)
                {
                    del = InstructionHandlers.Comparator;
                }
                else if (compiler.OpCodes.JumpCall == opCode)
                {
                    del = InstructionHandlers.JumpCall;
                }
                else if (compiler.OpCodes.Duplicate == opCode)
                {
                    del = InstructionHandlers.Duplicate;
                }
                else if (compiler.OpCodes.ConvertNumber == opCode)
                {
                    del = InstructionHandlers.ConvertNumber;
                }
                else
                {
                    throw new Exception($"Cannot support '{name}' OpCode");
                }
                //var associatedMethod = typeof(InstructionHandlers).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).First(x => x.Name == name);
                //FuncDelegate del = (FuncDelegate)associatedMethod!.CreateDelegate(typeof(FuncDelegate));
                vm.OpCodeHandlers.Add(opCode, del);
            }

            //var actualResult = TestProgram.Call1(10, 20);
            //Console.WriteLine(actualResult);
            TestProgram.Main(null);

            vm.Run();

            try
            {
                var reg = vm.GetReturnValue();
                Console.WriteLine(reg);
            }
            catch (Exception)
            {
                Console.WriteLine("No return register present!");
            }

            Console.ReadKey();
        }

        private static void PerformObfuscationTask(string[] args)
        {
            var target = "./AA/TestVMApp/TestVMApp.dll";
            if (args.Length > 0)
            {
                target = args[0];
            }

            var obf = new Obfuscator();
            var task = obf.CreateTask(target);

            obf.RunTask(task);

            var parentDir = new FileInfo(task.OutputFile).Directory!.Parent;
            var currentDir = new FileInfo(task.OutputFile).Directory!.FullName;
            var outDirName = new FileInfo(task.OutputFile!).DirectoryName + "_protected";
            var outDir = outDirName;
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            else
            {
                // Clear dir.
                foreach (var file in Directory.GetFiles(outDir))
                {
                    File.Delete(file);
                }
                // Do not delete dir, leave as-is (empty).
            }

            foreach (var file in Directory.GetFiles(currentDir))
            {
                var fName = Path.GetFileName(file);
                string inputFName = Path.GetFileName(task.InputFile);
                if (fName == inputFName)
                {
                    // Do not copy
                    continue;
                }
                if (fName.Contains("_regivm"))
                {
                    // Change name on output.
                    File.Copy(file, outDir + "\\" + inputFName);
                }
                else
                {
                    File.Copy(file, outDir + "\\" + fName);
                }
            }
        }
    }
}
