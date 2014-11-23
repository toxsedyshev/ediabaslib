﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace EdiabasLib
{
    using EdFloatType = Double;
    using EdValueType = UInt32;

    public partial class EdiabasNet
    {
        private static void OpA2fix(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpA2fix: Invalid type");
            }

            arg0.SetRawData((EdValueType)StringToValue(arg1.GetStringData()));
            ediabas.flags.UpdateFlags(arg0.GetValueData(), arg0.GetDataLen());
        }

        // BEST2: ator
        private static void OpA2flt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpA2flt: Invalid type");
            }

            string valueStr = arg1.GetStringData();
            EdFloatType result = StringToFloat(valueStr);

            arg0.SetRawData(result);
        }

        // BEST2: atoy
        private static void OpA2y(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpA2y: Invalid type");
            }

            List<byte> resultList = new List<byte>();
            string stringData = arg1.GetStringData();
            if (stringData.Length > 0)
            {
                bool exitLoop = false;
                string stringLower = stringData.ToLower(culture);
                for (int i = 0; i < stringLower.Length; i++)
                {
                    char currentChar = stringLower[i];
                    if (!(Char.IsDigit(currentChar) || (currentChar >= 'a' && currentChar <= 'f') ||
                        (currentChar == ' ') || (currentChar == ';') || (currentChar == ',')))
                    {
                        stringData = stringData.Substring(0, i);
                        break;
                    }
                }

                string[] stringArray = stringData.Split(new char[] { ',', ';' });
                foreach (string stringValue in stringArray)
                {
                    if (string.IsNullOrEmpty(stringValue.Trim()))
                    {
                        for (int i = 0; i < stringValue.Length + 1; i++)
                        {
                            resultList.Add(0);
                        }
                    }
                    else
                    {
                        string[] stringSubArray = stringValue.Trim().Split(' ');
                        foreach (string stringSubValue in stringSubArray)
                        {
                            if (stringSubValue.Length > 0)
                            {
                                try
                                {
                                    resultList.Add(Convert.ToByte(stringSubValue, 16));
                                }
                                catch (Exception)
                                {
                                    exitLoop = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (exitLoop)
                    {
                        break;
                    }
                }
            }
            arg0.SetArrayData(resultList.ToArray());
        }

        private static void OpAddc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAddc: Invalid type");
            }

            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType val0 = arg0.GetValueData(len);
            EdValueType val1 = arg1.GetValueData(len);
            if (ediabas.flags.carry)
            {
                val1++;
            }

            UInt64 sum = (UInt64)val0 + (UInt64)val1;
            arg0.SetRawData((EdValueType)sum);
            ediabas.flags.UpdateFlags((EdValueType)sum, len);
            ediabas.flags.SetOverflow(val0, val1, (EdValueType)sum, len);
            ediabas.flags.SetCarry(sum, len);
        }

        private static void OpAdds(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAdds: Invalid type");
            }

            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType val0 = arg0.GetValueData(len);
            EdValueType val1 = arg1.GetValueData(len);

            UInt64 sum = (UInt64)val0 + (UInt64)val1;
            arg0.SetRawData((EdValueType)sum);
            ediabas.flags.UpdateFlags((EdValueType)sum, len);
            ediabas.flags.SetOverflow(val0, val1, (EdValueType)sum, len);
            ediabas.flags.SetCarry(sum, len);
        }

        private static void OpAnd(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAnd: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len) & arg1.GetValueData(len);
            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpAsl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAsl: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len);
            Int32 shift = (Int32)arg1.GetValueData(len);
            if (shift < 0)
            {
                // don't touch carry here!
            }
            else if (shift == 0)
            {
                ediabas.flags.carry = false;
            }
            else
            {
                if (shift > len * 8)
                {
                    ediabas.flags.carry = false;
                }
                else
                {
                    long carryShift = (long)(len << 3) - shift;
                    EdValueType carryMask = (EdValueType)(1 << (int)carryShift);
                    ediabas.flags.carry = (value & carryMask) != 0;
                }

                if (shift >= len * 8)
                {
                    value = 0;
                }
                else
                {
                    value = value << shift;
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpAsr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAsr: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len);
            Int32 shift = (Int32)arg1.GetValueData(len);
            if (shift < 0)
            {
                // don't touch carry here!
            }
            else if (shift == 0)
            {
                ediabas.flags.carry = false;
            }
            else
            {
                if (shift > len * 8)
                {
                    EdValueType signMask = (EdValueType)(1 << (int)((len * 8) - 1));
                    if ((value & signMask) != 0)
                    {
                        ediabas.flags.carry = true;
                    }
                    else
                    {
                        ediabas.flags.carry = false;
                    }
                }
                else
                {
                    long carryShift = (long)shift - 1;
                    EdValueType carryMask = (EdValueType)(1 << (int)carryShift);
                    ediabas.flags.carry = (value & carryMask) != 0;
                }

                if (shift >= len * 8)
                {
                    EdValueType signMask = (EdValueType)(1 << (int)((len  * 8) - 1));
                    if ((value & signMask) != 0)
                    {
                        value = 0xFFFFFFFF;
                    }
                    else
                    {
                        value = 0;
                    }
                }
                else
                {
                    switch (len)
                    {
                        case 1:
                            value = (EdValueType)((SByte)value >> (int)shift);
                            break;

                        case 2:
                            value = (EdValueType)((Int16)value >> (int)shift);
                            break;

                        case 4:
                            value = (EdValueType)((Int32)value >> (int)shift);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException("len", "OpAsr: Invalid length");
                    }
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpAtsp(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAtsp: Invalid type");
            }
            if (arg0.GetDataType() != typeof(EdValueType))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpAtsp: Invalid data type");
            }

            EdValueType value = 0;
            EdValueType length = arg0.GetDataLen();
            EdValueType pos = arg1.GetValueData();
            if (ediabas.stackList.Count < length)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0005);
            }
            else
            {
                byte[] stackArray = ediabas.stackList.ToArray();

                long index = pos - length;
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException("index", "OpAtsp: Invalid stack index");
                }
                for (int i = 0; i < length; i++)
                {
                    value <<= 8;
                    value |= stackArray[index++];
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.UpdateFlags(value, length);
        }

        // BEST2: userbreak
        private static void OpBreak(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetError(ErrorCodes.EDIABAS_BIP_0008);
        }

        private static void OpClear(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpClear: Invalid type");
            }
            Register arg0Data = (Register)arg0.opData1;
            Type dataType = arg0Data.GetDataType();
            if (dataType == typeof(byte[]))
            {
                arg0Data.ClearData();
            }
            else if (dataType == typeof(EdFloatType))
            {   // not supported in ediabas
                arg0Data.SetFloatData(0);
            }
            else
            {
                arg0Data.SetValueData(0);
            }
            ediabas.flags.carry = false;
            ediabas.flags.zero = true;
            ediabas.flags.sign = false;
            ediabas.flags.overflow = false;
        }

        // BEST2: getCfgInt
        private static void OpCfgig(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpCfgig: Invalid type");
            }

            string value = ediabas.GetConfigProperty(arg1.GetStringData());
            if (value != null)
            {
                arg0.SetRawData((EdValueType)StringToValue(value));
            }
        }

        private static void OpCfgis(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string valueString = string.Format(culture, "{0}", arg1.GetValueData());
            ediabas.SetConfigProperty(arg0.GetStringData(), valueString);
        }

        // BEST2: getCfgString
        private static void OpCfgsg(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpCfgsg: Invalid type");
            }

            string value = ediabas.GetConfigProperty(arg1.GetStringData());
            if (value != null)
            {
                arg0.SetArrayData(encoding.GetBytes(value));
            }
        }

        // clear carry
        private static void OpClrc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.flags.carry = false;
        }

        // BEST2: clear_error
        private static void OpClrt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.errorTrapBitNr = -1;
        }

        // clear overflow
        private static void OpClrv(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.flags.overflow = false;
        }

        private static void OpComp(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType val0 = arg0.GetValueData(len);
            EdValueType val1 = arg1.GetValueData(len);

            UInt64 diff = (UInt64)val0 - (UInt64)val1;
            ediabas.flags.UpdateFlags((EdValueType)diff, len);
            ediabas.flags.SetOverflow(val0, (EdValueType)(-val1), (EdValueType)diff, len);
            ediabas.flags.SetCarry(diff, len);
        }

        // BEST2: getdate
        private static void OpDate(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpDate: Invalid type");
            }
            DateTime saveNow = DateTime.Now;
            byte[] resultArray = new byte[5];

            resultArray[0] = (byte)saveNow.Day;
            resultArray[1] = (byte)saveNow.Month;
            resultArray[2] = (byte)(saveNow.Year % 100);
            resultArray[3] = (byte)CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(saveNow, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            byte dayOfWeek = (byte)saveNow.DayOfWeek;
            if (dayOfWeek == 0)
            {   // sunday
                dayOfWeek = 7;
            }
            resultArray[4] = dayOfWeek;

            arg0.SetArrayData(resultArray);
        }

        private static void OpDivs(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpDivs: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);

            EdValueType result = 0;
            bool error = false;
            try
            {
                switch (len)
                {
                    case 1:
                        // DIVS bug in ediabas!
                        // result = (EdValueType)((SByte)arg0.GetValueData(len) / (SByte)arg1.GetValueData(len));
                        result = (EdValueType)((Int32)arg0.GetValueData(len) / (Int32)arg1.GetValueData(len));
                        break;

                    case 2:
                        // DIVS bug in ediabas!
                        // result = (EdValueType)((Int16)arg0.GetValueData(len) / (Int16)arg1.GetValueData(len));
                        result = (EdValueType)((Int32)arg0.GetValueData(len) / (Int32)arg1.GetValueData(len));
                        break;

                    case 4:
                        result = (EdValueType)((Int32)arg0.GetValueData(len) / (Int32)arg1.GetValueData(len));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException("len", "OpDivs: Invalid length");
                }
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0007);
                error = true;
            }
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(result, len);
            if (error)
            {   // DIVS bug in ediabas!
                result = arg0.GetValueData(len);
            }
            arg0.SetRawData(result);
        }

        // BEST2: make_error (execute)
        private static void OpEerr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.errorTrapBitNr >= 0)
            {
                foreach (ErrorCodes key in EdiabasNet.trapBitDict.Keys)
                {
                    if (EdiabasNet.trapBitDict[key] == ediabas.errorTrapBitNr)
                    {
                        ediabas.RaiseError(key);
                    }
                }
                ediabas.RaiseError(ErrorCodes.EDIABAS_BIP_0000);
            }
        }

        // BEST2: new_set_of_results
        private static void OpEnewset(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.resultSetsTemp != null && ediabas.resultDict.Count > 0)
            {
                ediabas.resultSetsTemp.Add(new Dictionary<string, ResultData>(ediabas.resultDict));
                ediabas.resultDict.Clear();
            }
        }

        private static void OpErgb(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeB, arg0.GetStringData(), (Int64)((Byte)arg1.GetValueData(1))));
        }

        private static void OpErgw(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeW, arg0.GetStringData(), (Int64)((UInt16)arg1.GetValueData(2))));
        }

        private static void OpErgd(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeD, arg0.GetStringData(), (Int64)((UInt32)arg1.GetValueData(4))));
        }

        private static void OpErgi(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeI, arg0.GetStringData(), (Int64)((Int16)arg1.GetValueData(2))));
        }

        private static void OpErgr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeR, arg0.GetStringData(), arg1.GetFloatData()));
        }

        private static void OpErgs(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeS, arg0.GetStringData(), arg1.GetStringData()));
        }

        private static void OpErgy(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeY, arg0.GetStringData(), arg1.GetArrayData()));
        }

        private static void OpErgc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeC, arg0.GetStringData(), (Int64)((SByte)arg1.GetValueData(1))));
        }

        private static void OpErgl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.SetResultData(new ResultData(ResultType.TypeL, arg0.GetStringData(), (Int64)((Int32)arg1.GetValueData(4))));
        }

        // BEST2: doNewInit
        private static void OpErgsysi(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string data = arg0.GetStringData();
            EdValueType dataValue = arg1.GetValueData(2);
            if (data == "!INITIALISIERUNG")
            {
                if (dataValue != 0)
                {
                    ediabas.requestInit = true;
                }
            }
            else
            {
                ediabas.SetSysResultData(new ResultData(ResultType.TypeI, data, (Int64)dataValue));
            }
        }

        // BEST2: realadd
        private static void OpFadd(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFadd Invalid type");
            }

            EdFloatType val0 = arg0.GetFloatData();
            EdFloatType val1 = arg1.GetFloatData();

            EdFloatType result = 0;
            try
            {
                result = val0 + val1;
                if (Double.IsInfinity(result) || Double.IsNaN(result))
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
                }
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
            }

            arg0.SetRawData(result);
        }

        private static void OpFcomp(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdFloatType val0 = arg0.GetFloatData();
            EdFloatType val1 = arg1.GetFloatData();

            EdFloatType diff = val0 - val1;
            if (Double.IsInfinity(diff) || Double.IsNaN(diff))
            {
                ediabas.flags.carry = true;
            }
            ediabas.flags.zero = val0 == val1;
            ediabas.flags.sign = val0 < val1;
            ediabas.flags.overflow = false;
        }

        // BEST2: realdiv
        private static void OpFdiv(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFdiv: Invalid type");
            }

            EdFloatType val0 = arg0.GetFloatData();
            EdFloatType val1 = arg1.GetFloatData();

            EdFloatType result = 0;
            try
            {
                result = val0 / val1;
                if (Double.IsInfinity(result) || Double.IsNaN(result))
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
                }
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
            }

            arg0.SetRawData(result);
        }

        // BEST2: itoad
        private static void OpFix2dez(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFix2dez: Invalid type");
            }

            EdValueType len;
            if (arg1.GetDataType() != typeof(EdValueType))
            {
                len = 1;
            }
            else
            {
                len = arg1.GetDataLen();
            }
            EdValueType value = arg1.GetValueData(len);
            string result;
            switch (len)
            {
                case 1:
                    result = string.Format(culture, "{0}", (SByte)value);
                    break;

                case 2:
                    result = string.Format(culture, "{0}", (Int16)value);
                    break;

                case 4:
                    result = string.Format(culture, "{0}", (Int32)value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("len", "OpFix2dez: Invalid length");
            }
            arg0.SetStringData(result);
        }

        // BEST2: itor
        private static void OpFix2flt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFix2flt: Invalid type");
            }

            Int32 value = (Int32)arg1.GetValueData();
            EdFloatType result = (EdFloatType)value;
            arg0.SetRawData(result);
        }

        // BEST2: itoax
        private static void OpFix2hex(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFix2hex: Invalid type");
            }

            EdValueType len;
            if (arg1.GetDataType() != typeof(EdValueType))
            {
                len = 1;
            }
            else
            {
                len = arg1.GetDataLen();
            }
            EdValueType value = arg1.GetValueData(len);
            string result;
            switch (len)
            {
                case 1:
                    result = string.Format(culture, "0x{0:X02}", value);
                    break;

                case 2:
                    result = string.Format(culture, "0x{0:X04}", value);
                    break;

                case 4:
                    result = string.Format(culture, "0x{0:X08}", value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("len", "OpFix2hex: Invalid length");
            }
            arg0.SetStringData(result);
        }

        // BEST2: rtoa
        private static void OpFlt2a(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFlt2a: Invalid type");
            }

            EdFloatType value = arg1.GetFloatData();
            EdFloatType valueConv = RoundToSignificantDigits(value, ediabas.floatPrecision);

            string result = string.Format(culture, "{0}", valueConv);
            int digitCount = 0;
            int pos = 0;
            foreach (char singleChar in result)
            {
                if (Char.IsDigit(singleChar))
                {
                    digitCount++;
                    if (digitCount >= ediabas.floatPrecision)
                    {
                        result = result.Substring(0, pos + 1);
                        break;
                    }
                }
                pos++;
            }
            arg0.SetStringData(result);
        }

        // BEST2: rtoi
        private static void OpFlt2fix(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFlt2fix: Invalid type");
            }

            EdFloatType value = arg1.GetFloatData();
            
            EdValueType result = (EdValueType)value;
            arg0.SetRawData(result);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(result, sizeof(EdValueType));
        }

        // BEST2: real_to_data (intel byte order)
        private static void OpFlt2y4(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFlt2y4: Invalid type");
            }

            Register arg0Data = (Register)arg0.opData1;
            byte[] dataArrayDest = arg0Data.GetArrayData();
            EdValueType startIdx = (EdValueType)arg0.opData2;

            EdValueType dataLen = sizeof(Single);
            if (startIdx + dataLen > ediabas.ArrayMaxSize)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                return;
            }
            if (dataArrayDest.Length < (startIdx + dataLen))
            {
                Array.Resize(ref dataArrayDest, (int)(startIdx + dataLen));
            }

            EdFloatType value = arg1.GetFloatData();
            Single singleValue = 0;
            try
            {
                singleValue = (Single)value;
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
            }

            byte[] resultArray = BitConverter.GetBytes(singleValue);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(resultArray);
            }
            Array.Copy(resultArray, 0, dataArrayDest, (int)startIdx, resultArray.Length);
            arg0Data.SetArrayData(dataArrayDest);
        }

        // BEST2: real_to_data (intel byte order)
        private static void OpFlt2y8(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFlt2y8: Invalid type");
            }

            Register arg0Data = (Register)arg0.opData1;
            byte[] dataArrayDest = arg0Data.GetArrayData();
            EdValueType startIdx = (EdValueType)arg0.opData2;

            EdValueType dataLen = sizeof(Double);
            if (startIdx + dataLen > ediabas.ArrayMaxSize)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                return;
            }
            if (dataArrayDest.Length < (startIdx + dataLen))
            {
                Array.Resize(ref dataArrayDest, (int)(startIdx + dataLen));
            }

            EdFloatType value = arg1.GetFloatData();
            byte[] resultArray = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(resultArray);
            }
            Array.Copy(resultArray, 0, dataArrayDest, (int)startIdx, resultArray.Length);
            arg0Data.SetArrayData(dataArrayDest);
        }

        // BEST2: realmul
        private static void OpFmul(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFmul: Invalid type");
            }

            EdFloatType val0 = arg0.GetFloatData();
            EdFloatType val1 = arg1.GetFloatData();

            EdFloatType result = 0;
            try
            {
                result = val0 * val1;
                if (Double.IsInfinity(result) || Double.IsNaN(result))
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
                }
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
            }

            arg0.SetRawData(result);
        }

        // BEST2: realsub
        private static void OpFsub(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFsub: Invalid type");
            }

            EdFloatType val0 = arg0.GetFloatData();
            EdFloatType val1 = arg1.GetFloatData();

            EdFloatType result = 0;
            try
            {
                result = val0 - val1;
                if (Double.IsInfinity(result) || Double.IsNaN(result))
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
                }
            }
            catch (Exception)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0011);
            }

            arg0.SetRawData(result);
        }

        private static void OpEoj(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.AddrMode != OpAddrMode.None)
            {
                ediabas.resultJobStatus = arg0.GetStringData();
            }
            ediabas.jobEnd = true;
        }

        // BEST2: ascii2hex
        private static void OpHex2y(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpHex2y: Invalid type");
            }
            string valueStr = arg1.GetStringData();
            byte[] result;
            try
            {
                result = EdiabasNet.HexToByteArray(valueStr);
                ediabas.flags.carry = false;
            }
            catch (Exception)
            {
                result = byteArray0;
                ediabas.flags.carry = true;
            }
            arg0.SetArrayData(result);
        }

        // jump if result tag is not existing
        private static void OpEtag(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            lock (ediabas.apiLock)
            {
                if (ediabas.resultsRequestDict.Count > 0)
                {
                    bool result = false;
                    if (!ediabas.resultsRequestDict.TryGetValue(arg1.GetStringData().ToUpper(culture), out result))
                    {
                        ediabas.pcCounter = arg0.GetValueData();
                    }
                }
            }
        }

        // BEST2: fclose
        private static void OpFclose(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType handle = arg0.GetValueData(1);
            if (!ediabas.CloseUserFile((int)handle))
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
        }

        // BEST2: fopen
        private static void OpFopen(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFopen: Invalid type");
            }
            int handle = -1;

            string fileName = arg1.GetStringData();
            if (!File.Exists(fileName))
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    Stream fs = MemoryStreamReader.OpenRead(fileName);
                    handle = ediabas.StoreUserFile(fs);
                    if (handle < 0)
                    {
                        fs.Dispose();
                        ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                    }
                }
                catch (Exception)
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }
            arg0.SetRawData((EdValueType)handle);
            ediabas.flags.UpdateFlags((EdValueType)handle, 1);
        }

        // BEST2: fread
        private static void OpFread(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFread: Invalid type");
            }
            int value = -1;

            EdValueType handle = arg1.GetValueData(1);
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    value = fs.ReadByte();
                }
                catch (Exception)
                {
                    value = -1;
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }

            if (value < 0)
            {
                value = 0;
                ediabas.flags.carry = true;
            }
            else
            {
                ediabas.flags.carry = false;
            }
            arg0.SetRawData((EdValueType)value);
        }

        // BEST2: freadln
        private static void OpFreadln(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFreadln: Invalid type");
            }
            string lineString = null;

            EdValueType handle = arg1.GetValueData(1);
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    lineString = ediabas.ReadFileLine(fs);
                }
                catch (Exception)
                {
                    lineString = null;
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }

            if (lineString == null)
            {
                lineString = string.Empty;
                ediabas.flags.carry = true;
            }
            else
            {
                ediabas.flags.carry = false;
            }
            arg0.SetArrayData(encoding.GetBytes(lineString));
        }

        // BEST2: fseek
        private static void OpFseek(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType handle = arg0.GetValueData(1);
            EdValueType position = arg1.GetValueData();
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    fs.Position = position;
                }
                catch (Exception)
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }
        }

        // BEST2: fseekln
        private static void OpFseekln(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType handle = arg0.GetValueData(1);
            EdValueType line = arg1.GetValueData();
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    fs.Position = 0;
                    for (int i = 0; i < line; i++)
                    {
                        if (ediabas.ReadFileLineLength(fs) < 0)
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }
        }

        // BEST2: ftell
        private static void OpFtell(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFtell: Invalid type");
            }
            Int32 position = 0;

            EdValueType handle = arg1.GetValueData(1);
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    position = (Int32)fs.Position;
                }
                catch (Exception)
                {
                    position = 0;
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }
            arg0.SetRawData((EdValueType)position);
            ediabas.flags.UpdateFlags((EdValueType)position, sizeof(EdValueType));
        }

        // BEST2: ftellln
        private static void OpFtellln(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpFtellln: Invalid type");
            }
            EdValueType line = 0;

            EdValueType handle = arg1.GetValueData(1);
            Stream fs = ediabas.GetUserFile((int)handle);
            if (fs == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
            }
            else
            {
                try
                {
                    long currentPos = fs.Position;
                    fs.Position = 0;
                    for (; ; )
                    {
                        if (ediabas.ReadFileLineLength(fs) < 0)
                        {
                            break;
                        }
                        if (fs.Position >= currentPos)
                        {
                            if (fs.Position == currentPos)
                            {
                                line++;
                            }
                            break;
                        }
                        line++;
                    }

                    fs.Position = currentPos;
                }
                catch (Exception)
                {
                    line = 0;
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0006);
                }
            }
            arg0.SetRawData(line);
            ediabas.flags.UpdateFlags(line, sizeof(EdValueType));
        }

        // BEST2: generateRunError
        private static void OpGenerr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ErrorCodes error = (ErrorCodes)arg0.GetValueData();
            if ((error < ErrorCodes.EDIABAS_RUN_0000) || (error > ErrorCodes.EDIABAS_RUN_LAST))
            {
                ediabas.RaiseError(ErrorCodes.EDIABAS_BIP_0001);
            }
            else
            {
                ediabas.RaiseError(error);
            }
        }

        // BEST2: get_trap_mask
        private static void OpGettmr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpGettmr: Invalid type");
            }
            arg0.SetRawData(ediabas.errorTrapMask);
            ediabas.flags.UpdateFlags(arg0.GetValueData(), arg0.GetDataLen());
        }

        private static void OpMove(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpMove: Invalid type");
            }
            Type data0Type = arg0.GetDataType();
            Type data1Type = arg1.GetDataType();
            if (data0Type == typeof(EdValueType))
            {
                EdValueType len = GetArgsValueLength(arg0, arg1);
                EdValueType value = arg1.GetValueData(len);
                arg0.SetRawData(value);
                ediabas.flags.carry = false;
                ediabas.flags.overflow = false;
                ediabas.flags.UpdateFlags(value, len);
            }
            else if (data0Type == typeof(byte[]))
            {
                if (data1Type == typeof(EdValueType))
                {
                    EdValueType len = GetArgsValueLength(arg0, arg1);
                    EdValueType value = arg1.GetValueData(len);
                    arg0.SetRawData(value);
                    ediabas.flags.carry = false;
                    ediabas.flags.overflow = false;
                    ediabas.flags.UpdateFlags(value, 1);
                }
                else if (data1Type == typeof(byte[]))
                {
                    object sourceData = arg1.GetRawData();
                    if ((arg0.AddrMode == OpAddrMode.RegS) && (sourceData.GetType() == typeof(byte[])))
                    {
                        byte[] destArray = arg0.GetArrayData();
                        byte[] sourceArray = (byte[])sourceData;
                        if (destArray.Length < sourceArray.Length)
                        {
                            Array.Resize(ref destArray, sourceArray.Length);
                        }
                        Array.Copy(sourceArray, 0, destArray, 0, sourceArray.Length);
                        arg0.SetRawData(destArray);
                    }
                    else
                    {
                        arg0.SetRawData(arg1.GetRawData());
                    }
                    ediabas.flags.carry = false;
                    ediabas.flags.zero = false;
                    ediabas.flags.sign = false;
                    ediabas.flags.overflow = false;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("data1Type", "OpMove: Invalid source data type");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException("data0Type", "OpMove: Invalid target data type");
            }
        }

        // BEST2: incProgressPos
        private static void OpIincpos(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            long incVal = arg0.GetValueData();
            long newValue;

            lock (ediabas.apiLock)
            {
                if (ediabas.infoProgressPos < 0)
                {
                    newValue = incVal;
                }
                else
                {
                    newValue = ediabas.infoProgressPos + incVal;
                }
                if (newValue > ediabas.infoProgressRange)
                {
                    newValue = ediabas.infoProgressRange;
                }
                ediabas.infoProgressPos = newValue;
            }
            ediabas.JobProgressInform();
        }

        // BEST2: setProgressRange
        private static void OpIrange(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType progressRange = arg0.GetValueData();
            lock (ediabas.apiLock)
            {
                ediabas.infoProgressPos = -1;
                ediabas.infoProgressRange = progressRange;
            }
            ediabas.JobProgressInform();
        }

        // BEST2: updateInfo
        private static void OpIupdate(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string progressText = arg0.GetStringData();
            lock (ediabas.apiLock)
            {
                ediabas.infoProgressText = progressText;
            }
            ediabas.JobProgressInform();
        }

        // jump above
        private static void OpJa(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.carry && !ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump above equal (identical to jnc)
        private static void OpJae(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.carry)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump below or equal
        private static void OpJbe(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.carry || ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump carry (identical to jb)
        private static void OpJc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.carry)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump greater
        private static void OpJg(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.sign == ediabas.flags.overflow && !ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump greater or equal
        private static void OpJge(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.zero || (ediabas.flags.sign == ediabas.flags.overflow))
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump less
        private static void OpJl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.zero && (ediabas.flags.sign != ediabas.flags.overflow))
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump less or equal
        private static void OpJle(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if ((ediabas.flags.sign != ediabas.flags.overflow) || ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump minus
        private static void OpJmi(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.sign)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump not trap
        private static void OpJnt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            bool errorDetected = false;
            if (arg1.AddrMode != OpAddrMode.None)
            {
                EdValueType testBit = arg1.GetValueData(1);
                if (testBit > 0)
                {
                    if (ediabas.errorTrapBitNr == (int)testBit)
                    {
                        errorDetected = true;
                    }
                    if (ediabas.errorTrapBitNr == 0 && testBit == 32)
                    {
                        errorDetected = true;
                    }
                }
                else
                {
                    if (ediabas.errorTrapBitNr >= 0x40000000)
                    {
                        errorDetected = true;
                    }
                }
            }
            else
            {
                // Ediabas bug, should be identical to OpJt
                // incorrect behaviour if no argument is specified
                if (ediabas.errorTrapBitNr >= 0x40000000)
                {
                    errorDetected = true;
                }
            }
            if (!errorDetected)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump not overflow
        private static void OpJnv(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.overflow)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump not zero
        private static void OpJnz(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump plus
        private static void OpJpl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (!ediabas.flags.sign)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump
        private static void OpJump(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.pcCounter = arg0.GetValueData();
        }

        // jump trap
        private static void OpJt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            bool errorDetected = false;
            if (arg1.AddrMode != OpAddrMode.None)
            {
                EdValueType testBit = arg1.GetValueData(1);
                if (testBit > 0)
                {
                    if (ediabas.errorTrapBitNr == (int)testBit)
                    {
                        errorDetected = true;
                    }
                    if (ediabas.errorTrapBitNr == 0 && testBit == 32)
                    {
                        errorDetected = true;
                    }
                }
                else
                {
                    if (ediabas.errorTrapBitNr >= 0x40000000)
                    {
                        errorDetected = true;
                    }
                }
            }
            else
            {
                if (ediabas.errorTrapBitNr >= 0)
                {
                    errorDetected = true;
                }
            }
            if (errorDetected)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump overflow
        private static void OpJv(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.overflow)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        // jump zero
        private static void OpJz(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.flags.zero)
            {
                ediabas.pcCounter = arg0.GetValueData();
            }
        }

        private static void OpLsl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpLsl: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len);
            Int32 shift = (Int32)arg1.GetValueData(len);
            if (shift < 0)
            {
                // don't touch carry here!
            }
            else if (shift == 0)
            {
                ediabas.flags.carry = false;
            }
            else
            {
                if (shift > len * 8)
                {
                    ediabas.flags.carry = false;
                }
                else
                {
                    long carryShift = (long)(len << 3) - shift;
                    EdValueType carryMask = (EdValueType)(1 << (int)carryShift);
                    ediabas.flags.carry = (value & carryMask) != 0;
                }

                if (shift >= len * 8)
                {
                    value = 0;
                }
                else
                {
                    value = value << shift;
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpLsr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpLsr: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len);
            Int32 shift = (Int32)arg1.GetValueData(len);
            if (shift < 0)
            {
                // don't touch carry here!
            }
            else if (shift == 0)
            {
                ediabas.flags.carry = false;
            }
            else
            {
                if (shift > len * 8)
                {
                    ediabas.flags.carry = false;
                }
                else
                {
                    long carryShift = (long)shift - 1;
                    EdValueType carryMask = (EdValueType)(1 << (int)carryShift);
                    ediabas.flags.carry = (value & carryMask) != 0;
                }

                if (shift >= len * 8)
                {
                    value = 0;
                }
                else
                {
                    value = value >> shift;
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpMult(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpMult: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);

            EdValueType result;
            try
            {
                switch (len)
                {
                    case 1:
                        result = (EdValueType)((SByte)arg0.GetValueData(len) * (SByte)arg1.GetValueData(len));
                        break;

                    case 2:
                        result = (EdValueType)((Int16)arg0.GetValueData(len) * (Int16)arg1.GetValueData(len));
                        break;

                    case 4:
                        result = (EdValueType)((Int32)arg0.GetValueData(len) * (Int32)arg1.GetValueData(len));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException("len", "OpMult: Invalid length");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("OpMult mult failure", ex);
            }
            arg0.SetRawData(result);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(result, len);
        }

        private static void OpNop(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
        }

        private static void OpNot(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpNot: Invalid type");
            }
            EdValueType value = ~arg0.GetValueData();
            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(arg0.GetValueData(), arg0.GetDataLen());
        }

        private static void OpParl(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpParl Invalid type");
            }

            EdValueType result = 0;
            ediabas.flags.zero = true;
            ediabas.flags.carry = false;
            ediabas.flags.sign = false;
            ediabas.flags.overflow = false;
            EdValueType pos = arg1.GetValueData();
            pos--;

            List<string> argStrings = ediabas.getActiveArgStrings();
            if (pos < argStrings.Count)
            {
                string argString = argStrings[(int)pos];
                result = (EdValueType)StringToValue(argString);
                ediabas.flags.zero = false;
            }
            arg0.SetRawData(result);
        }

        // BEST2: parcount
        private static void OpParn(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpParn: Invalid type");
            }

            List<string> argStrings = ediabas.getActiveArgStrings();
            EdValueType count = (EdValueType)argStrings.Count;
            arg0.SetRawData(count);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(arg0.GetValueData(), arg0.GetDataLen());
        }

        private static void OpParr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpParr: Invalid type");
            }

            EdFloatType result = 0;
            ediabas.flags.zero = true;
            ediabas.flags.carry = false;
            ediabas.flags.sign = false;
            ediabas.flags.overflow = false;
            EdValueType pos = arg1.GetValueData();
            pos--;

            List<string> argStrings = ediabas.getActiveArgStrings();
            if (pos < argStrings.Count)
            {
                string argString = argStrings[(int)pos];
                result = StringToFloat(argString);
                ediabas.flags.zero = false;
            }
            arg0.SetRawData(result);
        }

        private static void OpPars(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpPars: Invalid type");
            }

            string result = string.Empty;
            ediabas.flags.zero = true;
            EdValueType pos = arg1.GetValueData();
            pos--;

            List<string> argStrings = ediabas.getActiveArgStrings();
            if (pos < argStrings.Count)
            {
                result = argStrings[(int)pos];
                ediabas.flags.zero = false;
            }
            arg0.SetStringData(result);
        }

        private static void OpPary(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpPary: Invalid type");
            }

            byte[] result = EdiabasNet.byteArray0;
            ediabas.flags.zero = true;
            byte[] argBin = ediabas.getActiveArgBinary();
            if (argBin.Length > 0)
            {
                result = argBin;
                ediabas.flags.zero = false;
            }
            arg0.SetArrayData(result);
        }

        private static void OpPush(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType value = arg0.GetValueData();
            EdValueType length = arg0.GetDataLen();

            for (int i = 0; i < length; i++)
            {
                ediabas.stackList.Push((byte) value);
                value >>= 8;
            }
        }

        private static void OpPushf(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType value = ediabas.flags.ToValue();
            for (int i = 0; i < sizeof(EdValueType); i++)
            {
                ediabas.stackList.Push((byte)value);
                value >>= 8;
            }
        }

        private static void OpPop(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0.opData1", "OpPop: Invalid type");
            }
            if (arg0.GetDataType() != typeof(EdValueType))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpPop: Invalid data type");
            }

            EdValueType value = 0;
            EdValueType length = arg0.GetDataLen();
            if (ediabas.stackList.Count < length)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0005);
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    value <<= 8;
                    value |= ediabas.stackList.Pop();
                }
            }

            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, length);
        }

        private static void OpPopf(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType value = 0;
            if (ediabas.stackList.Count < sizeof(EdValueType))
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0005);
            }
            else
            {
                for (int i = 0; i < sizeof(EdValueType); i++)
                {
                    value <<= 8;
                    value |= ediabas.stackList.Pop();
                }
            }
            ediabas.flags.FromValue(value);
        }

        private static void OpOr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpOr: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len) | arg1.GetValueData(len);
            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        // BEST2: datacat
        private static void OpScat(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpScat: Invalid type");
            }

            byte[] data1 = arg0.GetArrayData();
            byte[] data2 = arg1.GetArrayData();

            byte[] resultArray = new byte[data1.Length + data2.Length];
            data1.CopyTo(resultArray, 0);
            data2.CopyTo(resultArray, data1.Length);
            if (resultArray.Length > ediabas.ArrayMaxSize)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                return;
            }
            arg0.SetRawData(resultArray);
        }

        // BEST2: datacmp
        private static void OpScmp(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            byte[] data1 = arg0.GetArrayData();
            byte[] data2 = arg1.GetArrayData();

            if (data1.Length != data2.Length)
            {
                ediabas.flags.zero = false;
            }
            else
            {
                ediabas.flags.zero = Enumerable.SequenceEqual(data1, data2);
            }
        }

        // dataerase
        private static void OpSerase(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0.opData1", "OpSerase: Invalid type");
            }
            EdValueType startIdx;
            switch (arg0.AddrMode)
            {
                case OpAddrMode.IdxImm:
                    startIdx = (EdValueType)arg0.opData2;
                    break;

                case OpAddrMode.IdxReg:
                    {
                        Register arg0Data2 = (Register)arg0.opData2;
                        startIdx = arg0Data2.GetValueData();
                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException("arg0.AddrMode", "OpSerase: Invalid mode");
            }

            Register arg0Data = (Register)arg0.opData1;
            byte[] dataArray = arg0Data.GetArrayData();
            EdValueType len = arg1.GetValueData();

            List<byte> resultByteList = new List<byte>();
            for (int i = 0; i < dataArray.Length; i++)
            {
                if ((i < startIdx) || (i >= startIdx + len))
                {
                    resultByteList.Add(dataArray[i]);
                }
            }
            arg0Data.SetArrayData(resultByteList.ToArray());
        }

        // set carry
        private static void OpSetc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.flags.carry = true;
        }

        // rtoa (set float precision)
        private static void OpSetflt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.floatPrecision = arg0.GetValueData();
        }

        // BEST2: get_token (store spearator)
        private static void OpSetspc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.tokenSeparator = arg0.GetStringData();
            ediabas.tokenIndex = arg1.GetValueData();
        }

        // BEST2: make_error
        private static void OpSett(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType error = arg0.GetValueData();
            if (error == 0)
            {
                error = 0x40000000;
            }
            ediabas.errorTrapBitNr = (int)error;
        }

        // BEST2: set_trap_mask
        private static void OpSettmr(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.errorTrapMask = arg0.GetValueData();
        }

        // BEST2: shdataget
        private static void OpShmget(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpShmget: Invalid type");
            }

            string key = arg1.GetStringData().ToUpper(culture);
            byte[] data;
            if (ediabas.sharedDataDict.TryGetValue(key, out data))
            {
                ediabas.flags.carry = false;
            }
            else
            {
                ediabas.flags.carry = true;
                data = byteArray0;
            }
            arg0.SetArrayData(data);
        }

        // BEST2: shdataset
        private static void OpShmset(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string key = arg0.GetStringData().ToUpper(culture);
            if (ediabas.sharedDataDict.ContainsKey(key))
            {
                ediabas.sharedDataDict[key] = arg1.GetArrayData();
            }
            else
            {
                ediabas.sharedDataDict.Add(key, arg1.GetArrayData());
            }
        }

        // string length
        private static void OpSlen(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSlen: Invalid type");
            }
            arg0.SetRawData((EdValueType)arg1.GetDataLen());
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(arg0.GetValueData(), arg0.GetDataLen());
        }

        // datainsert
        private static void OpSpaste(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSpaste: Invalid type");
            }

            EdValueType startIdx;
            switch (arg0.AddrMode)
            {
                case OpAddrMode.IdxImm:
                    startIdx = (EdValueType)arg0.opData2;
                    break;

                case OpAddrMode.IdxReg:
                    {
                        Register arg0Data2 = (Register)arg0.opData2;
                        startIdx = arg0Data2.GetValueData();
                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException("arg0.AddrMode", "OpSpaste: Invalid mode");
            }

            Register arg0Data = (Register)arg0.opData1;
            byte[] dataArrayDest = arg0Data.GetArrayData();
            byte[] dataArraySource = arg1.GetArrayData();

            if (startIdx >= ediabas.ArrayMaxSize)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                return;
            }
            if (startIdx < dataArrayDest.Length)
            {
                List<byte> resultByteList = new List<byte>();
                for (int i = 0; i < startIdx; i++)
                {
                    resultByteList.Add(dataArrayDest[i]);
                }
                for (int i = 0; i < dataArraySource.Length; i++)
                {
                    resultByteList.Add(dataArraySource[i]);
                }
                for (int i = (int)startIdx; i < dataArrayDest.Length; i++)
                {
                    resultByteList.Add(dataArrayDest[i]);
                }
                if (resultByteList.Count > ediabas.ArrayMaxSize)
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                    return;
                }

                arg0Data.SetArrayData(resultByteList.ToArray());
            }
        }

        // BEST2: strrevers
        private static void OpSrevrs(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSpaste: Invalid type");
            }
            byte[] arrayData = arg0.GetArrayData();
            Array.Reverse(arrayData);
            arg0.SetArrayData(arrayData);
        }

        // BEST2: get_token (store token)
        private static void OpStoken(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpStoken Invalid type");
            }
            if (ediabas.tokenSeparator.Length == 0)
            {
                ediabas.flags.zero = true;
            }
            else
            {
                string splitString = arg1.GetStringData();
                string[] words = splitString.Split(ediabas.tokenSeparator.ToCharArray());
                if ((ediabas.tokenIndex < 1) || (ediabas.tokenIndex > words.Length))
                {
                    ediabas.flags.zero = true;
                }
                else
                {
                    arg0.SetStringData(words[ediabas.tokenIndex - 1]);
                    ediabas.flags.zero = false;
                }
            }
        }

        // BEST2: strcat
        private static void OpStrcat(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpStrcat: Invalid type");
            }

            EdValueType len1 = arg0.GetDataLen();
            string string1 = arg0.GetStringData();
            string string2 = arg1.GetStringData();

            if (len1 + string2.Length > ediabas.ArrayMaxSize)
            {
                string2 = string2.Substring(0, (int)(ediabas.ArrayMaxSize - len1));
            }
            string resultString = string1 + string2;
            arg0.SetStringData(resultString);
        }

        // BEST2: strcmp
        private static void OpStrcmp(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string string1 = arg0.GetStringData();
            string string2 = arg1.GetStringData();

            ediabas.flags.zero = String.Compare(string1, string2, StringComparison.Ordinal) != 0;
        }

        // BEST2: strcut
        private static void OpScut(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpScut: Invalid type");
            }

            byte[] dataArray = arg0.GetArrayData();
            EdValueType len = arg1.GetValueData();
            // len includes terminating 0
            if (len > dataArray.Length)
            {
                arg0.SetArrayData(byteArray0);
            }
            else
            {
                byte[] resultArray = new byte[dataArray.Length - len];
                Array.Copy(dataArray, resultArray, resultArray.Length);
                arg0.SetArrayData(resultArray);
            }
        }

        private static void OpSsize(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSsize: Invalid type");
            }
            EdValueType value = ediabas.ArrayMaxBufSize;
            arg0.SetRawData(value);
        }

        // BEST2: strlen
        private static void OpStrlen(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpStrlen:Invalid type");
            }

            EdValueType result = (EdValueType)arg1.GetStringData().Length;

            arg0.SetRawData(result);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(result, arg0.GetDataLen());
        }

        private static void OpSubb(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSubb: Invalid type");
            }

            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType val0 = arg0.GetValueData(len);
            EdValueType val1 = arg1.GetValueData(len);

            UInt64 diff = (UInt64)val0 - (UInt64)val1;
            arg0.SetRawData((EdValueType)diff);
            ediabas.flags.UpdateFlags((EdValueType)diff, len);
            ediabas.flags.SetOverflow(val0, (EdValueType)(-val1), (EdValueType)diff, len);
            ediabas.flags.SetCarry(diff, len);
        }

        private static void OpSubc(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSubc: Invalid type");
            }

            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType val0 = arg0.GetValueData(len);
            EdValueType val1 = arg1.GetValueData(len);
            if (ediabas.flags.carry)
            {
                val1++;
            }

            UInt64 diff = (UInt64)val0 - (UInt64)val1;
            arg0.SetRawData((EdValueType)diff);
            ediabas.flags.UpdateFlags((EdValueType)diff, len);
            ediabas.flags.SetOverflow(val0, (EdValueType)(-val1), (EdValueType)diff, len);
            ediabas.flags.SetCarry(diff, len);
        }

        // swap array
        private static void OpSwap(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpSwap: Invalid type");
            }

            Register arg0Data = (Register)arg0.opData1;
            byte[] dataArrayDest = arg0Data.GetArrayData(true);
            EdValueType startIdx = (EdValueType)arg0.opData2;
            EdValueType dataLen = (EdValueType)arg0.opData3;

            if (startIdx + dataLen > ediabas.ArrayMaxSize)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0001);
                return;
            }
            Array.Reverse(dataArrayDest, (int)startIdx, (int)dataLen);
            arg0Data.SetArrayData(dataArrayDest, true);
        }

        // BEST2: tabcolumns
        private static void OpTabcols(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                arg0.SetRawData((EdValueType)0);
            }
            else
            {
                EdValueType columns = ediabas.GetTableColumns(ediabas.GetTableFs(), ediabas.tableIndex);
                arg0.SetRawData(columns);
            }
        }

        // BEST2: tabget
        private static void OpTabget(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            if (ediabas.tableRowIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            string entry = ediabas.GetTableEntry(ediabas.GetTableFs(), ediabas.tableIndex, ediabas.tableRowIndex, arg1.GetStringData());
            if (entry == null)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            arg0.SetStringData(entry);
        }

        // BEST2: tabline
        private static void OpTabline(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            bool found = false;
            Int32 rowIndex = ediabas.GetTableLine(ediabas.GetTableFs(), ediabas.tableIndex, arg0.GetValueData(), out found);
            if (rowIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
            }
            ediabas.tableRowIndex = rowIndex;
            ediabas.flags.zero = !found;
        }

        // BEST2: tabrows
        private static void OpTabrows(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                arg0.SetRawData((EdValueType)0);
            }
            else
            {
                EdValueType rows = ediabas.GetTableRows(ediabas.GetTableFs(), ediabas.tableIndex) + 1;  // including header
                arg0.SetRawData(rows);
            }
        }

        // BEST2: tabseek
        private static void OpTabseek(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            bool found = false;
            Int32 rowIndex = ediabas.SeekTable(ediabas.GetTableFs(), ediabas.tableIndex, arg0.GetStringData(), arg1.GetStringData(), out found);
            if (rowIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            ediabas.tableRowIndex = rowIndex;
            ediabas.flags.zero = !found;
        }

        // BEST2: tabseeku
        private static void OpTabseeku(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (ediabas.tableIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            bool found = false;
            Int32 rowIndex = ediabas.SeekTable(ediabas.GetTableFs(), ediabas.tableIndex, arg0.GetStringData(), arg1.GetValueData(), out found);
            if (rowIndex < 0)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
                return;
            }
            ediabas.tableRowIndex = rowIndex;
            ediabas.flags.zero = !found;
        }

        // BEST2: tabset
        private static void OpTabset(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            ediabas.CloseTableFs();
            if (ediabas.sgbdBaseFs != null)
            {
                ediabas.SetTableFs(ediabas.sgbdBaseFs);
            }
            bool found;
            Int32 tableAddr = ediabas.GetTableIndex(ediabas.GetTableFs(), arg0.GetStringData(), out found);
            if (!found)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
            }
            ediabas.tableIndex = tableAddr;
            ediabas.tableRowIndex = -1;
        }

        // BEST2: tabsetext
        private static void OpTabsetex(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            string baseFileName = arg1.GetStringData();
            if (baseFileName.Length > 0)
            {
                string fullFileName = Path.Combine(ediabas.EcuPath, baseFileName + ".prg");
                if (!File.Exists(fullFileName))
                {
                    fullFileName = Path.Combine(ediabas.EcuPath, baseFileName + ".grp");
                }
                if (!File.Exists(fullFileName))
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_SYS_0002);
                    return;
                }

                try
                {
                    ediabas.CloseTableFs();
                    Stream fs = MemoryStreamReader.OpenRead(fullFileName);
                    ediabas.SetTableFs(fs);
                }
                catch (Exception)
                {
                    ediabas.SetError(ErrorCodes.EDIABAS_SYS_0002);
                }
            }
            bool found = false;
            Int32 tableAddr = ediabas.GetTableIndex(ediabas.GetTableFs(), arg0.GetStringData(), out found);
            if (!found)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_BIP_0010);
            }
            ediabas.tableIndex = tableAddr;
            ediabas.tableRowIndex = -1;
        }

        private static void OpTest(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len) & arg1.GetValueData(len);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        private static void OpTicks(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpTicks: Invalid type");
            }
            EdValueType value = (EdValueType)(DateTime.Now.Ticks / 10000);
            arg0.SetRawData(value);
        }

        // BEST2: gettime
        private static void OpTime(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpTime: Invalid type");
            }
            DateTime saveNow = DateTime.Now;
            byte[] resultArray = new byte[3];

            resultArray[0] = (byte)saveNow.Hour;
            resultArray[1] = (byte)saveNow.Minute;
            resultArray[2] = (byte)saveNow.Second;

            arg0.SetArrayData(resultArray);
        }

        // BEST2: uitoad
        private static void OpUfix2dez(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpUfix2dez: Invalid type");
            }

            EdValueType len;
            if (arg1.GetDataType() != typeof(EdValueType))
            {
                len = 1;
            }
            else
            {
                len = arg1.GetDataLen();
            }
            EdValueType value = arg1.GetValueData(len);
            string result;
            switch (len)
            {
                case 1:
                    result = string.Format(culture, "{0}", (Byte)value);
                    break;

                case 2:
                    result = string.Format(culture, "{0}", (UInt16)value);
                    break;

                case 4:
                    result = string.Format(culture, "{0}", (UInt32)value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("len", "OpUfix2dez: Invalid length");
            }
            arg0.SetStringData(result);
        }

        // BEST2: data_to_real (intel byte order)
        private static void OpY42flt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "Opy42flt: Invalid type");
            }

            byte[] dataArray = arg1.GetArrayData();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(dataArray, 0, sizeof(Single));
            }
            EdFloatType value = BitConverter.ToSingle(dataArray, 0);
            arg0.SetRawData(value);
        }

        // BEST2: data_to_real (intel byte order)
        private static void OpY82flt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpY82flt: Invalid type");
            }

            byte[] dataArray = arg1.GetArrayData();
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(dataArray, 0, sizeof(Double));
            }
            EdFloatType value = BitConverter.ToDouble(dataArray, 0);
            arg0.SetRawData(value);
        }

        // BEST2: bcd2ascii
        private static void OpY2bcd(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpY2bcd: Invalid type");
            }

            string result = string.Empty;
            byte[] dataArray = arg1.GetArrayData();
            foreach (byte data in dataArray)
            {
                result += ValueToBcd(data);
            }
            arg0.SetStringData(result);
        }

        // BEST2: hex2ascii
        private static void OpY2hex(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpY2hex: Invalid type");
            }

            string result = string.Empty;
            byte[] dataArray = arg1.GetArrayData();
            foreach (byte data in dataArray)
            {
                result += string.Format(culture, "{0:X02}", data);
            }
            arg0.SetStringData(result);
        }

        // BEST2: set_answer_length
        private static void OpXawlen(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                byte[] dataArray = arg0.GetArrayData();
                int length = dataArray.Length;
                if ((length & 0x01) != 0x00)
                {
                    throw new ArgumentOutOfRangeException("length", "OpXawlen: Invalid data length");
                }

                length >>= 1;
                Int16[] answerArray = new Int16[length];
                for (int i = 0; i < length; i++)
                {
                    int offset = i << 1;
                    Int16 value = (Int16)(
                        dataArray[offset + 0] |
                        (((Int16)dataArray[offset + 1]) << 8)
                        );
                    answerArray[i] = value;
                }
                interfaceClass.CommAnswerLen = answerArray;
            }
        }

        // BEST2: get_battery_voltage
        private static void OpXbatt(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXbat: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                arg0.SetRawData((EdValueType)interfaceClass.BatteryVoltage);
            }
        }

        // BEST2: open_communication
        private static void OpXconnect(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if (interfaceClass == null)
            {
                throw new ArgumentOutOfRangeException("interfaceClass", "OpXconnect: No communication class present");
            }

            interfaceClass.InterfaceConnect();
        }

        // BEST2: close_communication
        private static void OpXhangup(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if (interfaceClass == null)
            {
                throw new ArgumentOutOfRangeException("interfaceClass", "OpXhangup: No communication class present");
            }

            interfaceClass.InterfaceDisconnect();
        }

        // BEST2: get_ignition_voltage
        private static void OpXignit(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXignit: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                arg0.SetRawData((EdValueType)interfaceClass.IgnitionVoltage);
            }
        }

        private static void OpXor(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXor: Invalid type");
            }
            EdValueType len = GetArgsValueLength(arg0, arg1);
            EdValueType value = arg0.GetValueData(len) ^ arg1.GetValueData(len);
            arg0.SetRawData(value);
            ediabas.flags.overflow = false;
            ediabas.flags.UpdateFlags(value, len);
        }

        // BEST2: recv_keybytes
        private static void OpXkeyb(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXkeyb: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                arg0.SetRawData(interfaceClass.KeyBytes);
            }
        }

        // BEST2: set_repeat_counter
        private static void OpXreps(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                interfaceClass.CommRepeats = arg0.GetValueData();
            }
        }

        // BEST2: ifreset
        private static void OpXreset(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                interfaceClass.CommParameter = null;
            }
        }

        // BEST2: send_and_receive
        private static void OpXsend(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXsend: Invalid type");
            }

            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                long startTime = Stopwatch.GetTimestamp();
                byte[] request = arg1.GetArrayData();
                byte[] response;
                if (interfaceClass.TransmitData(request, out response))
                {
                    arg0.SetRawData(response);
                }
                timeMeas += Stopwatch.GetTimestamp() - startTime;
            }
        }

        // BEST2: set_communication_pars
        private static void OpXsetpar(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                byte[] dataArray = arg0.GetArrayData();
                int length = dataArray.Length;
                if ((length & 0x03) != 0x00)
                {
                    throw new ArgumentOutOfRangeException("length", "OpXsetpar: Invalid data length");
                }

                length >>= 2;
                EdValueType[] parsArray = new EdValueType[length];
                for (int i = 0; i < length; i++)
                {
                    int offset = i << 2;
                    EdValueType value =
                        dataArray[offset + 0] |
                        (((EdValueType)dataArray[offset + 1]) << 8) |
                        (((EdValueType)dataArray[offset + 2]) << 16) |
                        (((EdValueType)dataArray[offset + 3]) << 24);
                    parsArray[i] = value;
                }
                interfaceClass.CommParameter = parsArray;
            }
        }

        // BEST2: recv_keybytes
        private static void OpXstate(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXstate: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                arg0.SetRawData(interfaceClass.State);
            }
        }

        // BEST2: stop_frequent
        private static void OpXstopf(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
        }

        // BEST2: iftype
        private static void OpXtype(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXtype: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                arg0.SetStringData(interfaceClass.InterfaceType);
            }
        }

        // BEST2: ifvers
        private static void OpXvers(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            if (arg0.opData1.GetType() != typeof(Register))
            {
                throw new ArgumentOutOfRangeException("arg0", "OpXvers: Invalid type");
            }
            EdInterfaceBase interfaceClass = ediabas.EdInterfaceClass;
            if ((interfaceClass == null) || !interfaceClass.Connected)
            {
                ediabas.SetError(ErrorCodes.EDIABAS_IFH_0056);
            }
            else
            {
                EdValueType value = (EdValueType)interfaceClass.InterfaceVersion;
                arg0.SetRawData(value);
            }
        }

        // BEST2: wait
        private static void OpWait(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            Thread.Sleep((int)(arg0.GetValueData() * 1000));
        }

        // BEST2: waitex
        private static void OpWaitex(EdiabasNet ediabas, OpCode oc, Operand arg0, Operand arg1)
        {
            Thread.Sleep((int)(arg0.GetValueData()));
        }
    }
}
