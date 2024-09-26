using AsmResolver.PE.DotNet.Cil;
using Echo.Ast;
using RegiVM.VMBuilder.Instructions;

namespace RegiVM.VMBuilder
{
    public class VMAstWalker : AstNodeListener<CilInstruction>
    {
        /// <inheritdoc />
        public override void EnterAssignmentStatement(AssignmentStatement<CilInstruction> statement) 
        {
            //Console.WriteLine("Enter assignment ");
        }

        /// <inheritdoc />
        public override void ExitAssignmentStatement(AssignmentStatement<CilInstruction> statement) 
        { 

        }

        /// <inheritdoc />
        public override void EnterExpressionStatement(ExpressionStatement<CilInstruction> statement) 
        {

        }

        /// <inheritdoc />
        public override void ExitExpressionStatement(ExpressionStatement<CilInstruction> statement) 
        {

        }

        /// <inheritdoc />
        public override void EnterPhiStatement(PhiStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void ExitPhiStatement(PhiStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void EnterBlockStatement(BlockStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void ExitBlockStatement(BlockStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void EnterExceptionHandlerStatement(ExceptionHandlerStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void ExitExceptionHandlerBlock(ExceptionHandlerStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void EnterProtectedBlock(ExceptionHandlerStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void ExitProtectedBlock(ExceptionHandlerStatement<CilInstruction> statement)
        {

        }

        /// <inheritdoc />
        public override void EnterHandlerBlock(ExceptionHandlerStatement<CilInstruction> statement, int handlerIndex)
        {

        }

        /// <inheritdoc />
        public override void ExitHandlerBlock(ExceptionHandlerStatement<CilInstruction> statement, int handlerIndex)
        {

        }

        /// <inheritdoc />
        public override void EnterPrologueBlock(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void ExitPrologueBlock(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void EnterEpilogueBlock(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void ExitEpilogueBlock(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void EnterHandlerContents(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void ExitHandlerContents(HandlerClause<CilInstruction> clause)
        {

        }

        /// <inheritdoc />
        public override void EnterVariableExpression(VariableExpression<CilInstruction> expression)
        {
            Console.WriteLine("Enter Variable Expression " + expression);
        }

        /// <inheritdoc />
        public override void ExitVariableExpression(VariableExpression<CilInstruction> expression)
        {
            Console.WriteLine("Exit Variable Expression " + expression);
        }

        public VMCompiler Compiler { get; set; } = null!;

        /// <inheritdoc />
        public override void EnterInstructionExpression(InstructionExpression<CilInstruction> expression)
        {
            Console.WriteLine("Enter Instruction Expression " + expression);

        }

        /// <inheritdoc />
        public override void ExitInstructionExpression(InstructionExpression<CilInstruction> expression)
        {
            Console.WriteLine("Exit Instruction Expression " + expression);
        }
    }
}
