using AsmResolver.DotNet;
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

            var masterKeyOpCode = 1234546161531ul;
            var opCodeBytes = BitConverter.GetBytes(masterKeyOpCode);
            var currentInstOffset = 3411;
            var callerInstOffset1 = 3339;
            var callerInstOffset2 = 1234;
            byte[] currentInstOperand = new byte[] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0x10, 0x12 };

            // Our master key is actually a derived key due to the opCode bytes being only 8 bytes long.
            var masterKey = Rfc2898DeriveBytes.Pbkdf2(opCodeBytes, BitConverter.GetBytes(currentInstOffset), 10000, HashAlgorithmName.SHA512, 32);
            
            var encryptedOperand = AesGcmImplementation.Encrypt(currentInstOperand, masterKey);

            var encryptedKey1DerivedKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(callerInstOffset1), BitConverter.GetBytes(currentInstOffset), currentInstOffset, HashAlgorithmName.SHA512, 32);
            var encryptedKey2DerivedKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(callerInstOffset2), BitConverter.GetBytes(currentInstOffset), currentInstOffset, HashAlgorithmName.SHA512, 32);
            
            var encryptedKey1 = AesGcmImplementation.Encrypt(masterKey, encryptedKey1DerivedKey);
            var encryptedKey2 = AesGcmImplementation.Encrypt(masterKey, encryptedKey1DerivedKey);


            // What is needed at runtime:
            /* 
             * - Previous offset
             * - Current offset
             * - Instruction opcode + operand to decrypt. The raw value underlying the master key does not matter.
             * - Need the encrypted master keys stored with the instruction.
             */

            // Runtime:
            var runtimeKey1 = encryptedKey1;

            var runtimeEncryptedKey1DerivedKey = Rfc2898DeriveBytes.Pbkdf2(BitConverter.GetBytes(callerInstOffset1), BitConverter.GetBytes(currentInstOffset), currentInstOffset, HashAlgorithmName.SHA512, 32);
            var decryptedMasterKey = AesGcmImplementation.Decrypt(runtimeKey1, runtimeEncryptedKey1DerivedKey);

            var decryptedData = AesGcmImplementation.Decrypt(encryptedOperand, decryptedMasterKey);



            // Or do something crazy.

            //var payload = currentInstOperand.Concat(opCodeBytes).ToArray();

            //using (var shamir = new ShamirSecretSharingImplementation.ShamirSecretSharing())
            //{

            //    var mod = BigInteger.Pow(2, 4253) - 1;
            //    var val = new BigInteger(payload);


            //    var shares = shamir.Split(2, 3, val, mod);

            //    var value = shamir.Join(shares, mod);

            //    var originalPayload = value.ToByteArray();

            //    if (payload != originalPayload)
            //    {

            //    }

            //}




            //// This happens at compile time, and runtime. The key is derived from what the runtime knows.
            //var derivedKeyBytes2 = Rfc2898DeriveBytes.Pbkdf2(derivedMasterKeyBytes, BitConverter.GetBytes(callerInstOffset2), currentInstOffset, HashAlgorithmName.SHA512, 32);
            //Debugger.Break();


            // RUNTIME NOW.

            //var runtimeDerivedKeyBytes1 = Rfc2898DeriveBytes.Pbkdf2(derivedMasterKeyBytes, BitConverter.GetBytes(callerInstOffset1), currentInstOffset, HashAlgorithmName.SHA512, 32);



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
