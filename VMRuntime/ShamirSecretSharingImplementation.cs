using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RegiVM.VMRuntime
{
    //    MIT License

    //Copyright(c) 2023 mtanksl

    //Permission is hereby granted, free of charge, to any person obtaining a copy
    //of this software and associated documentation files (the "Software"), to deal
    //in the Software without restriction, including without limitation the rights
    //to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    //copies of the Software, and to permit persons to whom the Software is
    //furnished to do so, subject to the following conditions:

    //The above copyright notice and this permission notice shall be included in all
    //copies or substantial portions of the Software.

    //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    //IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    //FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    //AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    //LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    //OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    //SOFTWARE.
    public class ShamirSecretSharingImplementation
    {
        public class ShamirSecretSharing : IDisposable
        {
            private static readonly int[] MersennePrimes = new int[] { /* 2, 3, 5, 7, 13, 17, 19, 31, 61, 89, 107, 127, */ 521, 607, 1279, 2203, 2281, 3217, 4253, 4423, 9689, 9941, 11213, 19937, 21701, 23209, 44497, 86243, 110503, 132049, 216091, 756839, 859433, 1257787, 1398269, 2976221, 3021377, 6972593, 13466917, /* 20996011, 24036583, 25964951, 30402457, 32582657, 37156667, 42643801, 43112609 */ };

            private static BigInteger CalculateModulo(BigInteger value)
            {
                foreach (var exponent in MersennePrimes)
                {
                    var modulo = BigInteger.Pow(2, exponent) - 1;

                    if (modulo > value)
                    {
                        return modulo;
                    }
                }

                throw new NotImplementedException();
            }

            private static BigInteger CalculateModulo(int length)
            {
                foreach (var exponent in MersennePrimes)
                {
                    var modulo = BigInteger.Pow(2, exponent) - 1;

                    if (modulo.ToByteArray().Length == length)
                    {
                        return modulo;
                    }
                }

                throw new NotImplementedException();
            }

            private RandomNumberGenerator random;

            public ShamirSecretSharing()
            {
                random = RandomNumberGenerator.Create();
            }

            ~ShamirSecretSharing()
            {
                Dispose(false);
            }

            /// <summary>
            /// Split the message into n (totalShares) shares, requiring m (minimumShares) shares to reconstruct it.
            /// </summary>        
            public Share[] Split(int minimumShares, int totalShares, string message)
            {
                var value = new BigInteger(Encoding.UTF8.GetBytes(message).Concat(new byte[] { 0x00 }).ToArray());

                var modulo = CalculateModulo(value);

                var shares = Split(minimumShares, totalShares, value, modulo);

                return shares;
            }

            /// <summary>
            /// Split the message into n (totalShares) shares, requiring m (minimumShares) shares to reconstruct it.
            /// </summary>
            public Share[] Split(int minimumShares, int totalShares, BigInteger value, BigInteger modulo)
            {
                if (minimumShares < 2)
                {
                    throw new ArgumentException("Minimum shares must be greater than or equal to 2.", nameof(minimumShares));
                }

                if (totalShares < minimumShares)
                {
                    throw new ArgumentException("Total shares must be greater than or equal to minimum shares.", nameof(totalShares));
                }

                if (modulo <= value)
                {
                    throw new ArgumentException("Modulo must be a prime number greater than value.", nameof(modulo));
                }

                var coeficients = new BigInteger[minimumShares];

                coeficients[0] = value; // c

                for (int i = 1; i < coeficients.Length; i++)
                {
                    var randomNumber = new byte[modulo.ToByteArray().Length];

                    random.GetNonZeroBytes(randomNumber);

                    coeficients[i] = new BigInteger(randomNumber.Concat(new byte[] { 0x00 }).ToArray()); // k₁, k₂, ..., kₙ
                }

                var shares = new Share[totalShares];

                for (int j = 0; j < shares.Length; j++)
                {
                    var x = new BigInteger(j + 1);

                    var y = new BigInteger(0);

                    for (int i = 0; i < coeficients.Length; i++)
                    {
                        // f(x) = c + k₁ * x + k₂ * x² + ... + kₙ * xⁿ

                        y = (y + coeficients[i] * BigInteger.Pow(x, i)) % modulo;
                    }

                    shares[j] = new Share(x, y, modulo.ToByteArray().Length);
                }

                return shares;
            }

            /// <summary>
            /// Reconstruct the message using the minimum required shares.
            /// </summary>      
            public string Join(Share[] shares)
            {
                var modulo = CalculateModulo(shares[0].Length);

                var value = Join(shares, modulo);

                var message = Encoding.UTF8.GetString(value.ToByteArray());

                return message;
            }

            /// <summary>
            /// Reconstruct the message using the minimum required shares.
            /// </summary>
            public BigInteger Join(Share[] shares, BigInteger modulo)
            {
                var y = new BigInteger(0);

                for (int i = 0; i < shares.Length; i++)
                {
                    var delta = new BigInteger(1);

                    for (int j = 0; j < shares.Length; j++)
                    {
                        if (i != j)
                        {
                            // d(i, x) = ∏ (x - j) / (i - j) for j E { i₁, i₂, ..., iₙ }, i != j

                            delta *= -1 * shares[j].X * BigIntegerExtensions.ModInverse(shares[i].X - shares[j].X, modulo);
                        }
                    }

                    // f(x) = f(i₁) * d(i₁, x) + f(i₂) * d(i₂, x) + ... + f(iₙ) * d(iₙ, x)

                    y = (y + shares[i].Y * delta) % modulo;
                }

                if (y < 0)
                {
                    y += modulo;
                }

                return y;
            }

            private bool disposed = false;

            public void Dispose()
            {
                Dispose(true);

                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    disposed = true;

                    if (disposing)
                    {
                        if (random != null)
                        {
                            random.Dispose();
                        }
                    }
                }
            }
        }

        public class Share
        {
            public static Share Parse(string value)
            {
                var splits = value.Split('-');

                var x = new byte[splits[0].Length / 2];

                for (int i = 0; i < splits[0].Length / 2; i++)
                {
                    x[i] = Convert.ToByte(splits[0].Substring(i * 2, 2), 16);
                }

                var y = new byte[splits[1].Length / 2];

                for (int i = 0; i < splits[1].Length / 2; i++)
                {
                    y[i] = Convert.ToByte(splits[1].Substring(i * 2, 2), 16);
                }

                return new Share(new BigInteger(x.Concat(new byte[] { 0x00 }).ToArray()), new BigInteger(y.Concat(new byte[] { 0x00 }).ToArray()), y.Length);
            }

            public static bool TryParse(string value, out Share result)
            {
                try
                {
                    result = Parse(value);

                    return true;
                }
                catch
                {
                    result = null;

                    return false;
                }
            }

            public Share(BigInteger x, BigInteger y, int length)
            {
                X = x;

                Y = y;

                Length = length;
            }

            public BigInteger X { get; }

            public BigInteger Y { get; }

            public int Length { get; }

            public override string ToString()
            {
                var extended = new byte[Length];

                var y = Y.ToByteArray();

                for (int i = 0; i < y.Length; i++)
                {
                    extended[i] = y[i];
                }

                return string.Concat(X.ToByteArray().Select(b => b.ToString("X2"))) + "-" + string.Concat(extended.Select(b => b.ToString("X2")));
            }
        }

        public static class BigIntegerExtensions
        {
            public static (BigInteger gcd, BigInteger s, BigInteger t) ExtendedGreatestCommonDivisor(BigInteger a, BigInteger b)
            {
                if (a <= 0)
                {
                    throw new ArgumentException("a must be greater than zero.", nameof(a));
                }

                if (b <= 0)
                {
                    throw new ArgumentException("b must be greater than zero.", nameof(b));
                }

                BigInteger temp;

                BigInteger old_r = a; BigInteger r = b;

                BigInteger old_s = 1; BigInteger s = 0;

                while (r != 0)
                {
                    var quotient = old_r / r;

                    temp = r; r = old_r - temp * quotient; old_r = temp;

                    temp = s; s = old_s - temp * quotient; old_s = temp;
                }

                if (b != 0)
                {
                    return (old_r, old_s, (old_r - old_s * a) / b);
                }

                return (old_r, old_s, 0);
            }

            public static BigInteger ModInverse(BigInteger a, BigInteger modulo)
            {
                if (a == 0)
                {
                    throw new ArgumentException("a must be different than zero.", nameof(a));
                }

                if (modulo == 0)
                {
                    throw new ArgumentException("modulo must be different than zero.", nameof(modulo));
                }

                var result = ExtendedGreatestCommonDivisor(BigInteger.Abs(a), BigInteger.Abs(modulo));

                if (result.gcd != 1)
                {
                    throw new InvalidOperationException("a is not invertible.");
                }

                var t = result.s;

                if (t < 0)
                {
                    t += modulo;
                }

                if ((a < 0 && modulo < 0) || (a > 0 && modulo > 0))
                {
                    return t;
                }

                return -t;
            }
        }

    }
}
