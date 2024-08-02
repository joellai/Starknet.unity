using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Utils
{
    public static class NumConverter
    {
        public static BigInteger MultiplyFloat(BigInteger bigInteger, float multiplier)
        {
            var bigintMultiplier = ConvertFloatToBigInt(multiplier, out var scaleFactor);

            BigInteger divisor = BigInteger.Pow(10, scaleFactor);
            var res = bigintMultiplier * bigInteger / divisor;

            return res;
        }

        public static BigInteger ConvertFloatToBigInt(float floatVal, out int scaleFactor)
        {
            string floatStr = floatVal.ToString("R");

            int decimalPointIndex = floatStr.IndexOf('.');

            scaleFactor = 0;
            if (decimalPointIndex != -1)
            {
                scaleFactor = floatStr.Length - decimalPointIndex - 1;
            }

            string integerStr = floatStr.Replace(".", "");

            BigInteger bigIntValue = BigInteger.Parse(integerStr);

            return bigIntValue;
        }

        public static string ConvertBigIntStrToHexStr(string bigIntStr)
        {
            Debug.Log(bigIntStr);
            if (bigIntStr.ToLower().StartsWith("0x"))
            {
                return bigIntStr.ToLower();
            }
            else
            {
                BigInteger bigint = BigInteger.Parse(bigIntStr);
                return "0x" + bigint.ToString("X").ToLower();
            }
        }

        public static string ConvertBigIntToHexStr(BigInteger bigInteger)
        {
            return "0x" + bigInteger.ToString("X").ToLower();
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

        public static BigInteger ConvertHexStrToBigInt(string hexStr)
        {
            if (hexStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexStr = hexStr.Substring(2);
            }

            return BigInteger.Parse(hexStr, System.Globalization.NumberStyles.HexNumber);
        }

        public static BigInteger ConvertHexStrToBigIntVer2(string hexStr)
        {
            BigInteger bigIntValue = BigInteger.Zero;

            if (hexStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexStr = hexStr.Substring(2);
            }

            foreach (char c in hexStr)
            {
                bigIntValue <<= 4; // Shift left by 4 bits
                if (c >= '0' && c <= '9')
                {
                    bigIntValue += c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    bigIntValue += c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    bigIntValue += c - 'A' + 10;
                }
                else
                {
                    Debug.LogError("Invalid hexadecimal string.");
                    break;
                }
            }

            return bigIntValue;
        }


    }
}