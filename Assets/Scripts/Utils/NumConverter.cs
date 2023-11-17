using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using UnityEngine;

namespace Utils
{
    public static class NumConverter
    {
        
        public static string ConvertBigIntStrToHexStr(string bigIntStr)
        {
            if (bigIntStr.StartsWith("0x"))
            {
                return bigIntStr.ToLower();
            }
            else
            {
                BigInteger bigint = BigInteger.Parse(bigIntStr);

                return "0x" + bigint.ToString("X").ToLower();
            }

        }

        public static string[] ConvertBigIntStrArrToHexStrArr(string[] input)
        {
            string[] result = new string[input.Length];
            int index = 0;
            foreach (string s in input)
            {
                result[index] = ConvertBigIntStrToHexStr(s);
                index++;
            }

            return result;
        }

        public static int ConvertHexStrToInt(string hexStr)
        {
            return Convert.ToInt32(hexStr, 16);
        }

        public static string ConvertIntToHexStr(int val)
        {
            return "0x"+Convert.ToString(val, 16);
        }
        
        
    }
    
    

}