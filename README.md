# RegiVM
- Some crap VM that Ellie came up with one day and thought "why not try make one?".
- Somewhat-register-based VM?
- OpCodes are ulong.
- Operands are byte arrays of any length.
- Registers are byte arrays of any length (currently converted from a string to Utf8 bytes), meaning funky things can be done.

## How it workies?
- VM Data is all converted on compile to a byte array.
- VM Data is loaded into a "heap" (it isn't an actual heap). 
- Step through each OpCode, send to handler for that OpCode.
- Any reference to a register is loaded from the heap. 

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
- RET `<register>`

## Maths
- ADD `<data-type> <register-to-save-to> <register-1> <register-2>`
- Repeat for SUB, MUL, DIV, XOR, AND, OR

# Dependencies
- The lovely [AsmResolver](https://github.com/Washi1337/AsmResolver).
- The even lovelier [Echo](https://github.com/Washi1337/Echo) (which currently isn't used to its full potential... >:c).

