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
### Original (Debug Mode IL Code):
![image](https://github.com/user-attachments/assets/60010b62-6cc4-442f-99d6-d3e9ce1ebf37)

### Compiled RegiVM:
![VsDebugConsole_z6IJU08MBJ](https://github.com/user-attachments/assets/a1249f2c-3513-4497-9cd9-3d38fab1c7d9)
- This shows the OpCode and the Operand values. 
- R is a register and T is a temporary register.

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

