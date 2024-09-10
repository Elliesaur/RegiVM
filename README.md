# RegiVM
- Supports basic operations and exception handling.
- Somewhat-register-based VM?
- OpCodes are ulong.
- Operands are byte arrays of any length.
- Registers are byte arrays of any length (currently converted from a string to Utf8 bytes), meaning funky things can be done.

## How it workies?
- VM Data is all converted on compile to a byte array.
- VM Data is loaded into a "heap" (it isn't an actual heap). 
- Step through each OpCode, send to handler for that OpCode.
- Any reference to a register is loaded from the heap. 

## Example Output
### Original C# Code:
<details>
  <summary>Show C# Code</summary>
  
```csharp
public static int Math6(int arg1, int arg2)
{
    try
    {
    a:
        int d = arg1;
        d = d - arg2;
        if (d == 0)
        {
            goto a;
        }
        if (d != 34)
        {
            d = 600;
        }
        else
        {
            d = 500;
        }
        try
        {
            d = d / 0;
            // exception happens
            // -> push to the handler.
            d = d + 5;
        }
        catch (DivideByZeroException e)
        {
            // value pushed by the CLR that contains object reference for the exception just thrown.
            // <>
            // stloc <e>
            d = d / 1;
        }
        catch (ArgumentOutOfRangeException f)
        {
            d = d / 2;
        }
        catch (Exception g)
        {
            d = d / 3;
        }
        finally
        {
            d = d + 100;
            arg2 = arg2 / 0;
        }
        return d;
    }
    catch
    {
        return 400;
    }
    finally
    {
        arg1 = 0;
        arg2 = 0;
    }
}
```
</details>

### .NET IL Code
<details>
  <summary>Show .NET IL Code</summary>

```cil
// Token: 0x06000006 RID: 6 RVA: 0x000021B4 File Offset: 0x000003B4
.method public hidebysig static 
	int32 Math6 (
		int32 arg1,
		int32 arg2
	) cil managed noinlining nooptimization 
{
	// Header Size: 12 bytes
	// Code Size: 135 (0x87) bytes
	// LocalVarSig Token: 0x11000004 RID: 4
	.maxstack 2
	.locals init (
		[0] int32 d,
		[1] bool,
		[2] bool,
		[3] class [System.Runtime]System.DivideByZeroException e,
		[4] class [System.Runtime]System.ArgumentOutOfRangeException f,
		[5] class [System.Runtime]System.Exception g,
		[6] int32
	)


	//     {

	/* (110,9)-(110,10) D:\Repos\RegiVM\Program.cs */
	/* 0x000003C0 00           */ IL_0000: nop
	.try
	{
		.try
		{

			//             d = arg1;

			/* (112,13)-(112,14) D:\Repos\RegiVM\Program.cs */
			/* 0x000003C1 00           */ IL_0001: nop
			// loop start (head: IL_0002)
				/* (113,13)-(113,15) D:\Repos\RegiVM\Program.cs */
				/* 0x000003C2 00           */ IL_0002: nop
				/* (114,17)-(114,30) D:\Repos\RegiVM\Program.cs */
				/* 0x000003C3 02           */ IL_0003: ldarg.0
				/* 0x000003C4 0A           */ IL_0004: stloc.0

				//             d -= arg2;

				/* (115,17)-(115,30) D:\Repos\RegiVM\Program.cs */
				/* 0x000003C5 06           */ IL_0005: ldloc.0
				/* 0x000003C6 03           */ IL_0006: ldarg.1
				/* 0x000003C7 59           */ IL_0007: sub
				/* 0x000003C8 0A           */ IL_0008: stloc.0

				//             flag = d == 0;

				/* (116,17)-(116,28) D:\Repos\RegiVM\Program.cs */
				/* 0x000003C9 06           */ IL_0009: ldloc.0
				/* 0x000003CA 16           */ IL_000A: ldc.i4.0
				/* 0x000003CB FE01         */ IL_000B: ceq
				/* 0x000003CD 0B           */ IL_000D: stloc.1

				//         while (flag);

				/* (hidden)-(hidden) D:\Repos\RegiVM\Program.cs */
				/* 0x000003CE 07           */ IL_000E: ldloc.1
				/* 0x000003CF 2C03         */ IL_000F: brfalse.s IL_0014

				/* (117,17)-(117,18) D:\Repos\RegiVM\Program.cs */
				/* 0x000003D1 00           */ IL_0011: nop
				/* (118,21)-(118,28) D:\Repos\RegiVM\Program.cs */
				/* 0x000003D2 2BEE         */ IL_0012: br.s      IL_0002
			// end loop


			//         bool flag2 = d != 34;

			/* (120,17)-(120,29) D:\Repos\RegiVM\Program.cs */
			/* 0x000003D4 06           */ IL_0014: ldloc.0
			/* 0x000003D5 1F22         */ IL_0015: ldc.i4.s  34
			/* 0x000003D7 FE01         */ IL_0017: ceq
			/* 0x000003D9 16           */ IL_0019: ldc.i4.0
			/* 0x000003DA FE01         */ IL_001A: ceq
			/* 0x000003DC 0C           */ IL_001C: stloc.2

			//         if (flag2)

			/* (hidden)-(hidden) D:\Repos\RegiVM\Program.cs */
			/* 0x000003DD 08           */ IL_001D: ldloc.2
			/* 0x000003DE 2C0A         */ IL_001E: brfalse.s IL_002A


			//             d = 600;

			/* (121,17)-(121,18) D:\Repos\RegiVM\Program.cs */
			/* 0x000003E0 00           */ IL_0020: nop
			/* (122,21)-(122,29) D:\Repos\RegiVM\Program.cs */
			/* 0x000003E1 2058020000   */ IL_0021: ldc.i4    600
			/* 0x000003E6 0A           */ IL_0026: stloc.0
			/* (123,17)-(123,18) D:\Repos\RegiVM\Program.cs */
			/* 0x000003E7 00           */ IL_0027: nop
			/* (hidden)-(hidden) D:\Repos\RegiVM\Program.cs */
			/* 0x000003E8 2B08         */ IL_0028: br.s      IL_0032


			//             d = 500;

			/* (125,17)-(125,18) D:\Repos\RegiVM\Program.cs */
			/* 0x000003EA 00           */ IL_002A: nop
			/* (126,21)-(126,29) D:\Repos\RegiVM\Program.cs */
			/* 0x000003EB 20F4010000   */ IL_002B: ldc.i4    500
			/* 0x000003F0 0A           */ IL_0030: stloc.0

			//         {

			/* (127,17)-(127,18) D:\Repos\RegiVM\Program.cs */
			/* 0x000003F1 00           */ IL_0031: nop

			/* (hidden)-(hidden) D:\Repos\RegiVM\Program.cs */
			/* 0x000003F2 00           */ IL_0032: nop
			.try
			{
				.try
				{

					//             d /= 0;

					/* (129,17)-(129,18) D:\Repos\RegiVM\Program.cs */
					/* 0x000003F3 00           */ IL_0033: nop
					/* (130,21)-(130,31) D:\Repos\RegiVM\Program.cs */
					/* 0x000003F4 06           */ IL_0034: ldloc.0
					/* 0x000003F5 16           */ IL_0035: ldc.i4.0
					/* 0x000003F6 5B           */ IL_0036: div
					/* 0x000003F7 0A           */ IL_0037: stloc.0

					//             d += 5;

					/* (133,21)-(133,31) D:\Repos\RegiVM\Program.cs */
					/* 0x000003F8 06           */ IL_0038: ldloc.0
					/* 0x000003F9 1B           */ IL_0039: ldc.i4.5
					/* 0x000003FA 58           */ IL_003A: add
					/* 0x000003FB 0A           */ IL_003B: stloc.0
					/* (134,17)-(134,18) D:\Repos\RegiVM\Program.cs */
					/* 0x000003FC 00           */ IL_003C: nop
					/* 0x000003FD DE1D         */ IL_003D: leave.s   IL_005C
				} // end .try
				catch [System.Runtime]System.DivideByZeroException
				{

					//         catch (DivideByZeroException e)

					/* (135,17)-(135,48) D:\Repos\RegiVM\Program.cs */
					/* 0x000003FF 0D           */ IL_003F: stloc.3

					//             d /= 1;

					/* (136,17)-(136,18) D:\Repos\RegiVM\Program.cs */
					/* 0x00000400 00           */ IL_0040: nop
					/* (140,21)-(140,31) D:\Repos\RegiVM\Program.cs */
					/* 0x00000401 06           */ IL_0041: ldloc.0
					/* 0x00000402 17           */ IL_0042: ldc.i4.1
					/* 0x00000403 5B           */ IL_0043: div
					/* 0x00000404 0A           */ IL_0044: stloc.0
					/* (141,17)-(141,18) D:\Repos\RegiVM\Program.cs */
					/* 0x00000405 00           */ IL_0045: nop
					/* 0x00000406 DE14         */ IL_0046: leave.s   IL_005C
				} // end handler
				catch [System.Runtime]System.ArgumentOutOfRangeException
				{

					//         catch (ArgumentOutOfRangeException f)

					/* (142,17)-(142,54) D:\Repos\RegiVM\Program.cs */
					/* 0x00000408 1304         */ IL_0048: stloc.s   f

					//             d /= 2;

					/* (143,17)-(143,18) D:\Repos\RegiVM\Program.cs */
					/* 0x0000040A 00           */ IL_004A: nop
					/* (144,21)-(144,31) D:\Repos\RegiVM\Program.cs */
					/* 0x0000040B 06           */ IL_004B: ldloc.0
					/* 0x0000040C 18           */ IL_004C: ldc.i4.2
					/* 0x0000040D 5B           */ IL_004D: div
					/* 0x0000040E 0A           */ IL_004E: stloc.0
					/* (145,17)-(145,18) D:\Repos\RegiVM\Program.cs */
					/* 0x0000040F 00           */ IL_004F: nop
					/* 0x00000410 DE0A         */ IL_0050: leave.s   IL_005C
				} // end handler
				catch [System.Runtime]System.Exception
				{

					//         catch (Exception g)

					/* (146,17)-(146,36) D:\Repos\RegiVM\Program.cs */
					/* 0x00000412 1305         */ IL_0052: stloc.s   g

					//             d /= 3;

					/* (147,17)-(147,18) D:\Repos\RegiVM\Program.cs */
					/* 0x00000414 00           */ IL_0054: nop
					/* (148,21)-(148,31) D:\Repos\RegiVM\Program.cs */
					/* 0x00000415 06           */ IL_0055: ldloc.0
					/* 0x00000416 19           */ IL_0056: ldc.i4.3
					/* 0x00000417 5B           */ IL_0057: div
					/* 0x00000418 0A           */ IL_0058: stloc.0
					/* (149,17)-(149,18) D:\Repos\RegiVM\Program.cs */
					/* 0x00000419 00           */ IL_0059: nop
					/* 0x0000041A DE00         */ IL_005A: leave.s   IL_005C
				} // end handler


				//         }

				/* (hidden)-(hidden) D:\Repos\RegiVM\Program.cs */
				/* 0x0000041C DE0D         */ IL_005C: leave.s   IL_006B
			} // end .try
			finally
			{

				//             d += 100;

				/* (151,17)-(151,18) D:\Repos\RegiVM\Program.cs */
				/* 0x0000041E 00           */ IL_005E: nop
				/* (152,21)-(152,33) D:\Repos\RegiVM\Program.cs */
				/* 0x0000041F 06           */ IL_005F: ldloc.0
				/* 0x00000420 1F64         */ IL_0060: ldc.i4.s  100
				/* 0x00000422 58           */ IL_0062: add
				/* 0x00000423 0A           */ IL_0063: stloc.0

				//             arg2 /= 0;

				/* (153,21)-(153,37) D:\Repos\RegiVM\Program.cs */
				/* 0x00000424 03           */ IL_0064: ldarg.1
				/* 0x00000425 16           */ IL_0065: ldc.i4.0
				/* 0x00000426 5B           */ IL_0066: div
				/* 0x00000427 1001         */ IL_0067: starg.s   arg2
				/* (154,17)-(154,18) D:\Repos\RegiVM\Program.cs */
				/* 0x00000429 00           */ IL_0069: nop
				/* 0x0000042A DC           */ IL_006A: endfinally
			} // end handler


			//         num = d;

			/* (155,17)-(155,26) D:\Repos\RegiVM\Program.cs */
			/* 0x0000042B 06           */ IL_006B: ldloc.0
			/* 0x0000042C 1306         */ IL_006C: stloc.s   V_6
			/* 0x0000042E DE14         */ IL_006E: leave.s   IL_0084
		} // end .try
		catch [System.Runtime]System.Object
		{

			//     catch

			/* (157,13)-(157,18) D:\Repos\RegiVM\Program.cs */
			/* 0x00000430 26           */ IL_0070: pop

			//         num = 400;

			/* (158,13)-(158,14) D:\Repos\RegiVM\Program.cs */
			/* 0x00000431 00           */ IL_0071: nop
			/* (159,17)-(159,28) D:\Repos\RegiVM\Program.cs */
			/* 0x00000432 2090010000   */ IL_0072: ldc.i4    400
			/* 0x00000437 1306         */ IL_0077: stloc.s   V_6
			/* 0x00000439 DE09         */ IL_0079: leave.s   IL_0084
		} // end handler
	} // end .try
	finally
	{

		//         arg1 = 0;

		/* (162,13)-(162,14) D:\Repos\RegiVM\Program.cs */
		/* 0x0000043B 00           */ IL_007B: nop
		/* (163,17)-(163,26) D:\Repos\RegiVM\Program.cs */
		/* 0x0000043C 16           */ IL_007C: ldc.i4.0
		/* 0x0000043D 1000         */ IL_007D: starg.s   arg1

		//         arg2 = 0;

		/* (164,17)-(164,26) D:\Repos\RegiVM\Program.cs */
		/* 0x0000043F 16           */ IL_007F: ldc.i4.0
		/* 0x00000440 1001         */ IL_0080: starg.s   arg2
		/* (165,13)-(165,14) D:\Repos\RegiVM\Program.cs */
		/* 0x00000442 00           */ IL_0082: nop
		/* 0x00000443 DC           */ IL_0083: endfinally
	} // end handler


	//     return num;

	/* (166,9)-(166,10) D:\Repos\RegiVM\Program.cs */
	/* 0x00000444 1106         */ IL_0084: ldloc.s   V_6
	/* 0x00000446 2A           */ IL_0086: ret
} // end of method TestProgram::Math6

```
  
</details>

### Compiled RegiVM:
<details>
  <summary>Show Compiled RegiVM</summary>

```
-> START_BLOCK Protected
-> START_BLOCK Protected
PARAMETER Int32 T(T0)
STORE_LOCAL T(T0) -> R(R0)
LOAD_LOCAL R(R0) -> T(T1)
PARAMETER Int32 T(T2)
SUB Int32=Int32-Int32 T(T3) T(T1) T(T2)
STORE_LOCAL T(T3) -> R(R0)
LOAD_LOCAL R(R0) -> T(T4)
NUMBER Int32 T(T5) 0
COMPARE T(T6) T(T4) (IsEqual) T(T5)
STORE_LOCAL T(T6) -> R(R1)
LOAD_LOCAL R(R1) -> T(T7)
JUMP_BOOL 16 True T(T7)
NUMBER Boolean T(T8) True
JUMP_BOOL 2 False T(T8)
LOAD_LOCAL R(R0) -> T(T9)
NUMBER Int32 T(T10) 34
COMPARE T(T11) T(T9) (IsEqual) T(T10)
NUMBER Int32 T(T12) 0
COMPARE T(T13) T(T11) (IsEqual) T(T12)
STORE_LOCAL T(T13) -> R(R2)
LOAD_LOCAL R(R2) -> T(T14)
JUMP_BOOL 28 True T(T14)
NUMBER Int32 T(T15) 600
STORE_LOCAL T(T15) -> R(R0)
NUMBER Boolean T(T16) True
JUMP_BOOL 30 False T(T16)
NUMBER Int32 T(T17) 500
STORE_LOCAL T(T17) -> R(R0)
-> START_BLOCK Protected
-> START_BLOCK Protected
LOAD_LOCAL R(R0) -> T(T18)
NUMBER Int32 T(T19) 0
DIV Int32=Int32/Int32 T(T20) T(T18) T(T19)
STORE_LOCAL T(T20) -> R(R0)
LOAD_LOCAL R(R0) -> T(T21)
NUMBER Int32 T(T22) 5
ADD Int32=Int32+Int32 T(T23) T(T21) T(T22)
STORE_LOCAL T(T23) -> R(R0)
NUMBER Boolean T(T24) True
JUMP_BOOL 63 False T(T24)
STORE_LOCAL T(T25) -> R(R3)
LOAD_LOCAL R(R0) -> T(T26)
NUMBER Int32 T(T27) 3
DIV Int32=Int32/Int32 T(T28) T(T26) T(T27)
STORE_LOCAL T(T28) -> R(R0)
NUMBER Boolean T(T29) True
JUMP_BOOL 63 False T(T29)
STORE_LOCAL T(T30) -> R(R4)
LOAD_LOCAL R(R0) -> T(T31)
NUMBER Int32 T(T32) 2
DIV Int32=Int32/Int32 T(T33) T(T31) T(T32)
STORE_LOCAL T(T33) -> R(R0)
NUMBER Boolean T(T34) True
JUMP_BOOL 63 False T(T34)
STORE_LOCAL T(T35) -> R(R5)
LOAD_LOCAL R(R0) -> T(T36)
NUMBER Int32 T(T37) 1
DIV Int32=Int32/Int32 T(T38) T(T36) T(T37)
STORE_LOCAL T(T38) -> R(R0)
NUMBER Boolean T(T39) True
JUMP_BOOL 63 False T(T39)
NUMBER Boolean T(T40) True
JUMP_BOOL 73 False T(T40)
LOAD_LOCAL R(R0) -> T(T41)
NUMBER Int32 T(T42) 100
ADD Int32=Int32+Int32 T(T43) T(T41) T(T42)
STORE_LOCAL T(T43) -> R(R0)
PARAMETER Int32 T(T44)
NUMBER Int32 T(T45) 0
DIV Int32=Int32/Int32 T(T46) T(T44) T(T45)
END_FINALLY
LOAD_LOCAL R(R0) -> T(T47)
STORE_LOCAL T(T47) -> R(R6)
NUMBER Boolean T(T48) True
JUMP_BOOL 84 False T(T48)
NUMBER Int32 T(T50) 400
STORE_LOCAL T(T50) -> R(R6)
NUMBER Boolean T(T51) True
JUMP_BOOL 84 False T(T51)
NUMBER Int32 T(T52) 0
NUMBER Int32 T(T53) 0
END_FINALLY
LOAD_LOCAL R(R6) -> T(T54)
RETURN T(T54)
```
- This shows the OpCode and the Operand values. 
- R is a register and T is a temporary register.

</details>

## But how does it make my closed-source obfuscator better?
- It does not.
- This is a small project which is only just starting. 
- Right now, the only thing it does is read a method from the TestProgram class and nothing else. 
- Idea is to make a "VM Protection" in the future and add it to [ProwlynxNET](https://github.com/prowlynx/ProwlynxNET).

## Problems?
- Yeah, it is rather crap.
- Yeah, it is inefficient.
- Yeah, it isn't quite perfect.
- Yeah, it doesn't support every operation.
- Open an issue, make a PR, be a *developer*.

# Operands/OpCodes
This is very basic for now.

## Working with Data
- NUMBER `<data-type> <register-to-save-to> <value>`
- PARAM `<data-type> <param-index>`
- STORE `<reg-from> <reg-to>`
- LOAD `<reg-from> <reg-to>` (this is actually just store again!)
- COMPARE `<reg-to-save-to> <comparator-type> <reg-left-compare> <reg-right-compare>`
- RET `<register>`

## Control Flow
- JUMP_BOOL `<offset-to-jump-to> <is-leaving-protected-code> <should-invert> <reg-to-check-for-boolean>` (for all branches)
- END_FINALLY
- START_BLOCK `<handlers-as-objects>`

## Maths
- ADD `<data-type> <register-to-save-to> <register-1> <register-2>`
- Repeat for SUB, MUL, DIV, XOR, AND, OR

# TODO
- [X] Refine try/catch to support handlers without exception types.
- [X] Refine check for branching into a protected region, current it might be that a branch statement is included whilst inside a protected region and the target is changed to be the start of the region when it is simply branching within the same region. Check if the destination is within the same region perhaps?
- [ ] Add support for pop (there is actually no need for this, but might be nice to have?)
- [ ] Add support for storing into parameters (starg).
- [ ] Add support for new objects.
- [ ] Add support for throwing exceptions.
- [ ] Add support for converting between number data types (conv...).
- [ ] Additional testing for exception handlers.
- [ ] Add support for calling basic methods (both definition and references) with arbitrary number of arguments (use registers).

# Dependencies
- The lovely [AsmResolver](https://github.com/Washi1337/AsmResolver).
- The even lovelier [Echo](https://github.com/Washi1337/Echo) (which currently isn't used to its full potential... >:c).

