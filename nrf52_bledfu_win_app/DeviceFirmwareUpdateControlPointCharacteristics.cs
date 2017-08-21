/*Copyright (c) 2015, Nordic Semiconductor ASA
 *
 *Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 *
 *1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 *
 *2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other 
 *materials provided with the distribution.
 *
 *3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific 
 *prior written permission.
 *
 *THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 *PURPOSE ARE DISCLAIMED. *IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF *SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, *DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 *ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED *OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Common.Service.GattService
{
    /// <summary>
    /// DFUControlPoint provide functions to handle logic when writes to ControlPoint Characteristics
    /// </summary>
    public interface IDeviceFirmwareUpdateControlPointCharacteristics
    {
        /// <summary>
        /// This function deals callback received from the peripheral
        /// </summary>
        string[] ProcessData(byte[] data);
        string OTHER_RESPONSE_CODE { get; }
        string OTHER_OP_CODE { get; }
        /// <summary>
        /// The following functions construct commands
        /// </summaryArray.Copy
        IBuffer InitialPacketStartCommand();
        IBuffer InitialPacketCompleteCommand();
        IBuffer RequestPacketReceiptNotificationCommand(Int16 numberOfPackets);
        IBuffer ReceiveFirmwareImageCommand();
        IBuffer PartialOfFirmwareImage(byte[][] trunk, int offSet);
        IBuffer ValidateFirmwareCommand();
        IBuffer StartBootLoaderCommand(FirmwareTypeEnum firmwareType);
        IBuffer ActiveAndResetCommand();
    }

    public static class DfuOperationCode
    {
        public const string StartDfuSucceded = "StartDfuSucceded";
        public const string InitialzeDFUParameterSucceded = "InitialzeDFUParameterSucceded";
        public const string PacketReceiptNotification = "PacketReceiptNotification";
        public const string ReceiveFirmwareImageSucceded = "ReceiveFirmwareImageSucceded";
        public const string ValidateFirmareSucceded = "ValidateFirmareSucceded";
    }

    public enum FirmwareTypeEnum
    {
        SoftDevice = 0x01,
        BootLoader = 0x02,
        Softdevice_Bootloader = 0x03,
        Application = 0x04
    }

    public class DeviceFirmwareUpdateControlPointCharacteristics : IDeviceFirmwareUpdateControlPointCharacteristics
    {
        #region OpCode
        public static byte OpCode_StartDfu = 0x01;
        public static byte OpCode_InitialzeDFUParameter = 0x02;
        public static byte OpCode_ReceiveFirmwareImage = 0x03;
        public static byte OpCode_ValidateFirmware = 0x04;
        public static byte OpCode_ActiveImageAndReset = 0x05;
        public static byte OpCode_PacketReceiptNotificationRequest = 0x08;
        public static byte OpCode_ResponseCode = 0x10;
        public static byte OpCode_PacketReceiptNotification = 0x11;
        #endregion

        #region ResponseValue
        public static byte BLE_DFU_RESP_VAL_SUCCESS = 0x01;     /**< Success.*/
        public static byte BLE_DFU_RESP_VAL_INVALID_STATE = 0x02;     /**< Invalid state.*/
        public static byte BLE_DFU_RESP_VAL_NOT_SUPPORTED = 0x03;     /**< Operation not supported.*/
        public static byte BLE_DFU_RESP_VAL_DATA_SIZE = 0x04;     /**< Data size exceeds limit.*/
        public static byte BLE_DFU_RESP_VAL_CRC_ERROR = 0x05;     /**< CRC Error.*/
        public static byte BLE_DFU_RESP_VAL_OPER_FAILED = 0x06;     /**< Operation failed.*/
        #endregion

        #region DFU_Initial_Packet
        public static byte OpCode_InitialPacketReceive = 0x00;
        public static byte OpCode_InitialPacketComplete = 0x01;
        #endregion

        public static int returnValueCode = 0;
        public static int returnValueOptional = 1;
        private string _OTHER_RESPONSE_CODE = "ResponseCode";
        private string _OTHER_OP_CODE = "OpCode";
        string IDeviceFirmwareUpdateControlPointCharacteristics.OTHER_RESPONSE_CODE
        {
            get
            {
                return _OTHER_RESPONSE_CODE;
            }
        }

        string IDeviceFirmwareUpdateControlPointCharacteristics.OTHER_OP_CODE
        {

            get
            {
                return _OTHER_OP_CODE;
            }
        }


        /// <summary>
        /// Process the raw data received from the device into application usable data, 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public string[] ProcessData(byte[] data)
        {
            byte responseValue = byte.MinValue;
            byte requestedOpCode = byte.MinValue;
            var returnValues = new string[2];

            if (data == null || data.Length == 0)
                new ApplicationArgumentException();
            var opCode = data[0]; //dfu_oper  
            requestedOpCode = data[1]; //dfu_process
            responseValue = data[2]; //dfu_status

            Debug.WriteLine("[ProcessData]OpCode:" + opCode + " requestedOpCode/dfu_process:" + requestedOpCode + " responseValue/dfu_status:" + responseValue);

            if (opCode == OpCode_ResponseCode) //OpCode_ResponseCode=0x10
            {
                if (requestedOpCode == OpCode_StartDfu && responseValue == BLE_DFU_RESP_VAL_SUCCESS)
                {
                    returnValues[returnValueCode] = DfuOperationCode.StartDfuSucceded;
                    returnValues[returnValueOptional] = string.Empty;
                    return returnValues;
                }
                else if (requestedOpCode == OpCode_InitialzeDFUParameter && responseValue == BLE_DFU_RESP_VAL_SUCCESS)
                {
                    returnValues[returnValueCode] = DfuOperationCode.InitialzeDFUParameterSucceded;
                    returnValues[returnValueOptional] = string.Empty;
                    return returnValues;
                }
                else if (requestedOpCode == OpCode_ReceiveFirmwareImage && responseValue == BLE_DFU_RESP_VAL_SUCCESS)
                {
                    returnValues[returnValueCode] = DfuOperationCode.ReceiveFirmwareImageSucceded;
                    returnValues[returnValueOptional] = string.Empty;
                    return returnValues;
                }
                else if (requestedOpCode == OpCode_ValidateFirmware && responseValue == BLE_DFU_RESP_VAL_SUCCESS)
                {
                    returnValues[returnValueCode] = DfuOperationCode.ValidateFirmareSucceded;
                    returnValues[returnValueOptional] = string.Empty;
                    return returnValues;
                }
                returnValues[returnValueCode] = responseValue.ToString();
                returnValues[returnValueOptional] = _OTHER_RESPONSE_CODE;
                return returnValues;
            }
            else if (opCode == OpCode_PacketReceiptNotification) //OpCode_PacketReceiptNotification=0x11
            {
                var receivedBytes = new byte[4];
                Array.Copy(data, 1, receivedBytes, 0, 4);
                returnValues[returnValueCode] = DfuOperationCode.PacketReceiptNotification;
                returnValues[returnValueOptional] = BitConverter.ToInt32(receivedBytes, 0).ToString();
                return returnValues;
            }
            returnValues[returnValueCode] = opCode.ToString();
            returnValues[returnValueOptional] = _OTHER_OP_CODE;
            return returnValues;
        }
        #region Commands
        public IBuffer StartBootLoaderCommand(FirmwareTypeEnum firmwareType)
        {
            var temp = new byte[] { OpCode_StartDfu, (byte)firmwareType };
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer InitialPacketStartCommand()
        {
            var temp = new byte[] { OpCode_InitialzeDFUParameter, OpCode_InitialPacketReceive };
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer InitialPacketCompleteCommand()
        {
            var temp = new byte[] { OpCode_InitialzeDFUParameter, OpCode_InitialPacketComplete };
            var buffer = temp.AsBuffer();
            return buffer;
        }

        public IBuffer RequestPacketReceiptNotificationCommand(Int16 numberOfPackets)
        {
            var bytes = BitConverter.GetBytes(numberOfPackets);
            var temp = new byte[1 + bytes.Length];
            temp[0] = OpCode_PacketReceiptNotificationRequest;
            Array.Copy(bytes, 0, temp, 1, bytes.Length);
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer ReceiveFirmwareImageCommand()
        {
            var temp = new byte[] { OpCode_ReceiveFirmwareImage };
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer PartialOfFirmwareImage(byte[][] trunk, int offSet)
        {
            if (offSet > trunk.Length)
                throw new ApplicationArgumentException();
            var temp = trunk[offSet];
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer ValidateFirmwareCommand()
        {
            var temp = new byte[] { OpCode_ValidateFirmware };
            var buffer = temp.AsBuffer();
            return buffer;
        }
        public IBuffer ActiveAndResetCommand()
        {
            var temp = new byte[] { OpCode_ActiveImageAndReset };
            var buffer = temp.AsBuffer();
            return buffer;
        }
        #endregion
    }

    internal class ApplicationArgumentException : Exception
    {
        private async void log(string data)
        {
            // Create sample file; replace if exists.
            Windows.Storage.StorageFolder storageFolder =
                Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile sampleFile =
                await storageFolder.CreateFileAsync("log_file.txt",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);

            //await Windows.Storage.FileIO.WriteTextAsync(sampleFile, data);
            await Windows.Storage.FileIO.AppendTextAsync(sampleFile, data);
        }

        public ApplicationArgumentException()
        {
            log("ApplicationArgumentException");
        }

        public ApplicationArgumentException(string message) : base(message)
        {
            log("ApplicationArgumentException:" + message);
        }

        public ApplicationArgumentException(string message, Exception innerException) : base(message, innerException)
        {
            log("ApplicationArgumentException:" + message);
        }
    }
}
