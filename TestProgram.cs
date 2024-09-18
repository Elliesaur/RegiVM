using System.Runtime.CompilerServices;

namespace RegiVM
{
    internal static class TestProgram
    {

        public static void Switch1(int argument1, int argument2)
        {
            var test = argument1 switch
            {
                1 => argument2 * 1,
                3 => argument2 * 4,
                49 => argument1 * 10,
                5 => argument2 * 5,
                7 => argument2 * 7,
                9 => argument2 * 9,
                10 => argument2 * 10,
                11 => argument2 * 11,
                12 => argument2 * 12,
                13 => argument2 * 13,
                14 => argument2 * 14,
            };
            if (test == 0)
            {
                return;
            }
        }

        public static int Math1()
        {
            // Load to local var.
            // Load to local var, using first local var.
            int y = 60;

            int a = y + 1;
            int b = y + 2;

            int c = a + b;

            // Return 360.
            return a + b + 300;
        }

        public static int Math2(int argument1, int argument2)
        {
            int temp = argument1 + argument2;

            return temp + 900;
        }

        public static int Math3(int argument1, int argument2)
        {
            int a = 1;
            int temp = argument1 + argument2;
            temp = temp - 50;
            temp = temp / 5;
            temp = temp ^ 2;
            temp = temp * 3;
            temp = temp | 3;
            temp = temp & 3;
            return a + temp + 900;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math4(int arg1, int arg2)
        {
            int a = 1;
            int b = 2;
            int c = a + b;
            int d = c * 10;
            d = d + arg1;
            d = d - arg2;
            
            return d + c;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math5(int arg1, int arg2)
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
            }
            return d;
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Math7(int arg1, int arg2)
        {
            try
            {
            a:
                int x = -10;
                int d = arg1;
                d = d - arg2;
                //d = Math2(arg1, arg2);
                if (d == 0)
                {
                    goto a;
                }
                switch (d)
                {
                    case 30:
                    case 35:
                    case 37:
                        x = 10;
                        d = 10;
                        break;
                    case 40:
                        x = 20;
                        break;
                    case 50:
                    case 60:
                        x = 30;
                        d = 30;
                        break;
                    default:
                        x = 40;
                        break;
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

                    //arg2 = arg2 / 0;
                }
                return d + x;
            }
            catch
            {
                return 6700;
            }
            finally 
            {
                arg1 = 0;
                arg2 = 0;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Call1(int arg1, int arg2)
        {
            try
            {
                int a = arg1 + 2;
                int b = Math2(a, arg2 * 2);
                b = b * 2;
                int c = Math2(b, a);
                return b + c;
            }
            catch (Exception)
            {
                return arg1 + arg2;
            }
            finally
            {
                int hello = 1337;
            }
        }
    }
}
