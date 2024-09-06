﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RegiVM.VMBuilder
{
    public class VMOpCode
    {
        public ulong ParameterLoad { get; set; } = 0x3000UL;
        public ulong NumberLoad { get; set; } = 0x1000UL;

        public ulong Add { get; set; } = 0x2000UL;
        public ulong Div { get; set; } = 0x4000UL;
        public ulong Mul { get; set; } = 0x5000UL;
        public ulong Sub { get; set; } = 0x6000UL;
        public ulong Xor { get; set; } = 0x7000UL;
        public ulong Ret { get; set; } = 0x8000UL;
        public ulong And { get; set; } = 0x9000UL;
        public ulong Or { get; set; } = 0x10000UL;
        public ulong LocalLoadStore { get; set; } = 0x11000UL;
        public ulong JumpBool { get; set; } = 0x12000UL;
        public ulong Comparator { get; set; } = 0x13000UL;
        public ulong StartBlock { get; set; } = 0x14000UL;
        public ulong LoadPhi { get; set; } = 0x16000UL;

        public void RandomizeAll()
        {
            Random r = new Random();
            List<long> used = new List<long>();
            foreach (var propertyInfo in this.GetType()
                                .GetProperties(
                                        BindingFlags.Public
                                        | BindingFlags.Instance))
            {
                long val = 0L;
                do
                {
                    val = r.NextInt64();
                }
                while (used.Contains(val));
                
                propertyInfo.SetValue(this, (ulong)val);
            }
        }
    }
}
