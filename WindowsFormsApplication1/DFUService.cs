using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;

using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.UI.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Common.Service.GattService;
using Common.Utility;
using Windows.Foundation;

namespace OTADFUApplication
{
    public enum DeviceFirmwareUpdateStatusEnum
    {
        // Files or image type are not chosen
        FILES_NOT_CHOSEN = 0,
        //Device is not connected
        DEVICE_NOT_CONNECTED = 1,
        //DFU or glucoseService change services are not discoveried
        SERVICES_NOT_AVAILABLE = 2,
        // Files and image type are selected
        READY = 3,
        //start dfu mode
        START_DFU = 4,
        // Sending packs
        SENDING = 5,
        // sending pack complete
        SENDING_COMPLETE = 6,
        // dfu failed
        DFU_TIMEOUT = 7,
        //Sending Errors
        DFU_ERROR
    }
    
    public class DFUService
    {
        #region Properties
        private GattDeviceService service { get; set; }
        private GattCharacteristic controlPoint { get; set; }
        private GattCharacteristic packet { get; set; }
        private GattCharacteristic dFUVersion { get; set; }
        private bool IsServiceChanged = false;
        public bool IsServiceInitialized { get; set; }
        public byte[] firmwareImage { get; private set; }
        private byte[][] firmwareImageTrunks { get; set; }
        public FirmwareTypeEnum firmwareType { get; set; }


        public static int MAX_SIZE_PER_GROUP = 20;
        public static short NUMBER_OF_PACKET_AT_TIME = 10;
        private int sentTimes = 0;
        private int sendedBytes = 0;
        
        public static String DFUService_UUID = "00001530-1212-efde-1523-785feabcd123";
        public static String DFUControlPoint = "00001531-1212-efde-1523-785feabcd123";
        public static String DFUPacket = "00001532-1212-efde-1523-785feabcd123";
        public static String DFUVersion = "00001534-1212-efde-1523-785feabcd123";
        #endregion
        
        private CoreDispatcher dispatcher { get; set; }

        // Make sure to check your device's documentation to find out how many characteristics your specific device has.
        private const int CHARACTERISTIC_INDEX = 0;
        // The DFU Profile specification requires that the ControlPoint characteristic is notifiable.
        private const GattClientCharacteristicConfigurationDescriptorValue CHARACTERISTIC_NOTIFICATION_TYPE =
            GattClientCharacteristicConfigurationDescriptorValue.Notify;

        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser().
        //MainPage rootPage = MainPage.Current;

        private static DFUService instance = new DFUService();

        private PnpObjectWatcher watcher;
        private String deviceContainerId;

        DeviceFirmwareUpdateControlPointCharacteristics deviceFirmwareUpdateControlPointCharacteristics = new DeviceFirmwareUpdateControlPointCharacteristics();


        /// <summary>
        /// Multiple of NUMBER_OF_PACKET_AT_TIME
        /// </summary>
        private int sendFullPackCompleteIndicator = 0;

        /// <summary>
        /// The rest of packets
        /// </summary>
        private int sendPartialPacketsNumberOfTimes = 0;


        #region Events
        public delegate void ServiceChangedIndication(GattCharacteristic sender, GattValueChangedEventArgs args);
        public event ServiceChangedIndication ServiceChanged;
        public delegate void DeviceFirmwareUpdateCompleteIndication(bool IsComplete);
        public event DeviceFirmwareUpdateCompleteIndication DeviceFirmwareUpdateComplete;
        public delegate void ComfirmPacketReceiptIndication(int sizeOfBytesSent, int totalFirmwareLength, string messageType, string messageData);
        public event ComfirmPacketReceiptIndication PacketReceiptConfirmed;
        public event ValueChangeCompletedHandler ValueChangeCompleted;
        public delegate void ValueChangeCompletedHandler(string[] controlPointReturnedValue);
        public event DeviceConnectionUpdatedHandler DeviceConnectionUpdated;
        public delegate void DeviceConnectionUpdatedHandler(bool isConnected);
        #endregion

        public static DFUService Instance
        {
            get { return instance; }
        }

        public GattDeviceService Service
        {
            get { return service; }
        }

        private DFUService()
        {
            //App.Current.Suspending += App_Suspending;
            //App.Current.Resuming += App_Resuming;
            
            //StartDeviceConnectionWatcher();
        }

        internal async void LoadDFUSettings()
        {
            /*
            dfuSettingViewModel = SettingPivotViewModel.GetInstance().GetDeviceFirmwareUpdateSettingPageViewModel();
            this.SelectedDeviceFirmwareTypeName = dfuSettingViewModel.SelectedDeviceFirmwareTypeName == null ? "Image type:" : dfuSettingViewModel.SelectedDeviceFirmwareTypeName;
            foreach (var token in dfuSettingViewModel.FileToken.Values)
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(token);
                    this.ChosenFiles.Add(file);
                }
            }
            this.ImageFileNames = dfuSettingViewModel.GetShortFileName();
            */

            //if (!IsImagesReadyToSend())
            if (false)
            {
                await UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum.FILES_NOT_CHOSEN);
            }
            else
            {
                await UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum.READY);
            }
        }

        async void deviceFirmwareUpdateService_PacketReceiptConfirmed(int sizeOfBytesSent, int totalFirmwareLength, string messageType, string messageData)
        {
            log("deviceFirmwareUpdateService_PacketReceiptConfirmed", "Status");
            if (messageType != string.Empty)
            {
                await UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum.DFU_ERROR, 0, messageType, messageData);
                return;
            }
            await UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum.SENDING, percentSent: 100);
        }

        async void deviceFirmwareUpdateService_DeviceFirmwareUpdateComplete(bool IsComplete)
        {
            log("deviceFirmwareUpdateService_DeviceFirmwareUpdateComplete:" + IsComplete, "Status");

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
            });
            //await UpdatePogressBar(100);
            await UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum.SENDING_COMPLETE);
        }
        
        private string FILES_NOT_CHOSEN = "File not present";
        private string DEVICE_NOT_CONNECTED = "Please connect your device";
        private string SERVICES_NOT_AVAILABLE = "Try to re-pair the device";
        private string START_DFU = "Waiting for updating...";
        private string READY = "Ready for an update";
        private string SENDING = "Updated";
        private string SENDING_COMPLETE = "Updated complete!";
        private string DFU_TIMEOUT = "Update time out";
        private string DFU_ERROR = "Update error";

        GattCommunicationStatus comStatus;

        public GattCommunicationStatus ComStatus
        {
            get
            {
                return comStatus;
            }
            set
            {
                if (this.comStatus != value)
                {
                    this.comStatus = value;
                    //this.OnPropertyChanged("Status");
                }
            }
        }

        private string status;

        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                if (this.status != value)
                {
                    this.status = value;
                    //this.OnPropertyChanged("Status");
                }
            }
        }



        public async Task<bool> UpdateDFUStatus(DeviceFirmwareUpdateStatusEnum status, int percentSent = 0, string errorType = "none", string errorCode = "none")
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (status)
                {
                    case DeviceFirmwareUpdateStatusEnum.FILES_NOT_CHOSEN:
                        this.Status = FILES_NOT_CHOSEN;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.DEVICE_NOT_CONNECTED:
                        this.Status = DEVICE_NOT_CONNECTED;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.SERVICES_NOT_AVAILABLE:
                        this.Status = SERVICES_NOT_AVAILABLE;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.READY:
                        this.Status = READY;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.START_DFU:
                        this.Status = START_DFU;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.SENDING:
                        //SendingPackStatus(SENDING, percentSent);
                        break;
                    case DeviceFirmwareUpdateStatusEnum.SENDING_COMPLETE:
                        this.Status = SENDING_COMPLETE;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.DFU_TIMEOUT:
                        this.Status = DFU_TIMEOUT;
                        break;
                    case DeviceFirmwareUpdateStatusEnum.DFU_ERROR:
                        log("SendingErrors(DFU_ERROR, errorType, errorCode);", "DFUService");
                        break;
                }
            });
            return true;
        }

        private void App_Resuming(object sender, object e)
        {
            // Since the Windows Runtime will close resources to the device when the app is suspended,
            // the device needs to be reinitialized when the app is resumed.
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            IsServiceInitialized = false;

            // This is an appropriate place to save to persistent storage any datapoint the application cares about.
            // For the purpose of this sample we just discard any values.

            // Allow the GattDeviceService to get cleaned up by the Windows Runtime.
            // The Windows runtime will clean up resources used by the GattDeviceService object when the application is
            // suspended. The GattDeviceService object will be invalid once the app resumes, which is why it must be 
            // marked as invalid, and reinitalized when the application resumes.
            if (service != null)
            {
                service.Dispose();
                service = null;
            }

            if (controlPoint != null)
            {
                controlPoint = null;
            }

            if (watcher != null)
            {
                watcher.Stop();
                watcher = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public async Task InitializeServiceAsync(DeviceInformation device, Program program)
        {
            this.mainProgram = program;
            try
            {
                deviceContainerId = "{" + device.Properties["System.Devices.ContainerId"] + "}";

                service = await GattDeviceService.FromIdAsync(device.Id);
                if (service != null)
                {
                    IsServiceInitialized = true;
                    await StartFirmwareUpdate();
                }
                else
                {
                    Console.WriteLine("Access to the device is denied, because the application was not granted access, " +
                        "or the device is currently in use by another application.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Accessing2 your device failed." + Environment.NewLine + e.Message);
            }
        }

        /// <summary>
        /// Write log data 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="appendname"></param>
        public async void log(string data, string appendname)
        {
            this.mainProgram.log(data, appendname);

        }

        public void ShowBluetoothOffErrorMessage()
        {
            /*
            var alternative1 = new UICommand("Go to settings", new UICommandInvokedHandler(GoToBluetoothSettingPage), 0);
            var alternative2 = new UICommand("Close", new UICommandInvokedHandler(CloseBluetoothIsOffMessage), 1);
            ShowMessage(BluetoothIsOffMessageTitle, BluetoothIsOffMessageContent, alternative1, alternative2);
            */
        }

        public async Task<bool> IsBluetoothSettingOn()
        {
            bool IsBluetoothOn = false;
            try
            {
                Windows.Networking.Proximity.PeerFinder.Start();
                Windows.Networking.Proximity.PeerInformation result = (await Windows.Networking.Proximity.PeerFinder.FindAllPeersAsync()).FirstOrDefault();
                //if(peers != null)
                //	return IsBluetoothOn = false;
                return IsBluetoothOn = true; //boolean variable
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x8007048F)
                {
                    return IsBluetoothOn = false;
                }
            }
            finally
            {
                Windows.Networking.Proximity.PeerFinder.Stop();
            }
            return IsBluetoothOn;
        }

        /// <summary>
        /// Switable to check if the device is reacheable
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>

        public async Task<bool> UpdateAvailableDevice(GattDeviceService service)
        {
            try
            {
                dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    log("ClearCachedDevices", "");
                });
                DeviceInformationCollection result = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(new Guid(DFUService.DFUService_UUID)));
                //DeviceInformationCollection result = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(new Guid(DFUService.DFUControlPoint)));
                if (result.Count > 0)
                {
                    foreach (var device in result)
                    {
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            log("Device name " + device.Name, "");
                            GattDeviceService ble = await GattDeviceService.FromIdAsync(device.Id) as GattDeviceService;
                            try
                            {
                                var wanted = ble.DeviceId;
                                log("DeviceHandles " + ble.AttributeHandle, "");
                                log("DeviceID " + wanted, "");
                                log("ServiceID " + service.DeviceId, "");
                                if (service.DeviceId == wanted)
                                {
                                    //await StartDeviceFirmwareUpdate(device.Name, service);
                                }
                                //DeviceSelectionViewModel.AddBLEDevice(ble);
                            }
                            catch (Exception e)
                            {
                            }
                        });
                    }
                }
                else
                {
                    if (!await IsBluetoothSettingOn())
                    {
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            ShowBluetoothOffErrorMessage();
                        });
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }




        /// <summary>
        /// Configure the Bluetooth device to send notifications whenever the Characteristic value changes
        /// </summary>
        private async Task StartFirmwareUpdate_()
        {
            try
            {
                // Obtain the characteristic for which notifications are to be received
                controlPoint = service.GetCharacteristics(new Guid(DFUService.DFUControlPoint))[CHARACTERISTIC_INDEX];

                // While encryption is not required by all devices, if encryption is supported by the device,
                // it can be enabled by setting the ProtectionLevel property of the Characteristic object.
                // All subsequent operations on the characteristic will work over an encrypted link.
                //characteristic.ProtectionLevel = GattProtectionLevel.EncryptionRequired; //Not necessary

                // Register the event handler for receiving notifications
                controlPoint.ValueChanged += controlPoint_ValueChanged;
               // controlPoint.add_ValueChanged(controlPoint_ValueChanged);

                // In order to avoid unnecessary communication with the device, determine if the device is already 
                // correctly configured to send notifications.
                // By default ReadClientCharacteristicConfigurationDescriptorAsync will attempt to get the current
                // value from the system cache and communication with the device is not typically required.
                var currentDescriptorValue = await controlPoint.ReadClientCharacteristicConfigurationDescriptorAsync();

                if (currentDescriptorValue.Status == GattCommunicationStatus.Success)
                {
                    log("Descriptor com success " + controlPoint.UserDescription, "");
                }
                else
                    log("Descriptor com UNsuccess " + controlPoint.UserDescription, "");

                //Test
                //StartDeviceConnectionWatcher();

                GattCommunicationStatus status = await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(CHARACTERISTIC_NOTIFICATION_TYPE);
                if (status == GattCommunicationStatus.Unreachable)
                {
                    Console.WriteLine("ERROR: Device not connected succesfully. Try to remove the device from the windows bluetooth settings.");
                    return;
                }

                if ((currentDescriptorValue.Status != GattCommunicationStatus.Success) ||
                    (currentDescriptorValue.ClientCharacteristicConfigurationDescriptor != CHARACTERISTIC_NOTIFICATION_TYPE))
                {
                    //Go here only when is a first time the device is used

                    log("Success " + service.DeviceId, "");
                    // Set the Client Characteristic Configuration Descriptor to enable the device to send notifications
                    // when the Characteristic value changes                    
                    status = await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(CHARACTERISTIC_NOTIFICATION_TYPE);
                    if (status == GattCommunicationStatus.Unreachable)
                    {
                        // Register a PnpObjectWatcher to detect when a connection to the device is established,
                        // such that the application can retry device configuration.
                        //StartDeviceConnectionWatcher();
                        log("Unreacheable", "Scenario1");
                    }
                    else
                    {
                        log("GattCommunicationStatus Success", "Scenario1");
                        int dfuVersion = await checkDFUStatus();
                        if (dfuVersion == 1)
                            await switchOnBootLoader(controlPoint);

                        if (status == GattCommunicationStatus.Success)
                            await switchOnBootLoader(controlPoint);
                    }
                }
                else
                {
                    //TODO. Find a way to remove e re-pair the device automatically
                    //rootPage.NotifyUser("ERROR: Device not connected succesfully. Try to remove the device from the windows bluetooth settings.", NotifyType.ErrorMessage);
                }
                //TODO: Correct this part
                int dfuVersion_ = await checkDFUStatus();
                if (dfuVersion_ == 1)
                    await this.switchOnBootLoader(controlPoint);

                //dfuVersion_ = await checkDFUStatus();
                try
                {
                    if (await this.switchOnBootLoader(controlPoint))
                        await this.sendImageSize();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Accessing1 your device failed." + Environment.NewLine + e.Message);
            }
        }

        /// <summary>
        /// Send the 0x01 opcode and the application type data 04.
        /// </summary>
        /// <param name="controlPoint"></param>
        /// <returns></returns>
        private async Task<bool> switchOnBootLoader(GattCharacteristic controlPoint)
        {
            //# Send 'START DFU' + Application Command
            //self._dfu_state_set(0x0104)
            var buffer = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_StartDfu, (byte)FirmwareTypeEnum.Application);
            GattCommunicationStatus status = await controlPoint.WriteValueAsync(buffer);  //Go in DFU Mode
            if (status == GattCommunicationStatus.Success)
            {
                log("switchOnBootLoader success", "switchOnBootLoader");
                return true;
            }
            else
            {
                log("switchOnBootLoader UNsuccess", "switchOnBootLoader");
                return false;
            }
        }

        /// <summary>
        /// Return an IBuffer giving opcode and value
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IBuffer getBufferFromCommand(byte opcode, byte value)
        {
            var temp = new byte[] { opcode, value };
            var buffer = temp.AsBuffer();
            return buffer;
        }

        /// <summary>
        /// Return an IBuffer giving only the opcode
        /// </summary>
        /// <param name="opcode"></param>
        /// <returns></returns>
        public IBuffer getBufferFromCommand(byte opcode)
        {
            var temp = new byte[] { opcode };
            var buffer = temp.AsBuffer();
            return buffer;
        }

        /// <summary>
        /// It consider only a firmware image for an application
        /// </summary>
        /// <param name="softDevice"></param>
        /// <param name="bootLoader"></param>
        /// <returns></returns>
        private int[] GetSizeOfImage(int softDevice = 0, int bootLoader = 0)
        {
            //as the specification, size should be give in order
            //<Length of SoftDevice><Length of Bootloader><Length of Application> 
            var sizes = new int[] { 0, 0, 0 };
            sizes[2] = firmwareImage.Length; //Only the application
            return sizes;
        }

        /// <summary>
        /// Try using this function
        /// </summary>
        /// <param name="sizeOfImage"></param>
        /// <returns></returns>
        public IBuffer ImageSizeCommand(int[] sizeOfImage)
        {
            if (sizeOfImage.Length != 3)
                throw new ArgumentException();
            int softDeviceSize = sizeOfImage[0];
            int bootLoaderSize = sizeOfImage[1];
            int applicationSize = sizeOfImage[2];
            if (softDeviceSize * bootLoaderSize * applicationSize < 0)
                throw new ArgumentException();
            //as the specification <Length of SoftDevice><Length of Bootloader><Length of Application> 
            var softDeviceBytes = BitConverter.GetBytes(softDeviceSize);
            var bootLoaderBytes = BitConverter.GetBytes(bootLoaderSize);
            var applicationSizeBytes = BitConverter.GetBytes(applicationSize);
            byte[] temp = new byte[softDeviceBytes.Length + bootLoaderBytes.Length + applicationSizeBytes.Length];
            Array.Copy(softDeviceBytes, 0, temp, 0, softDeviceBytes.Length);
            Array.Copy(bootLoaderBytes, 0, temp, 4, bootLoaderBytes.Length);
            Array.Copy(applicationSizeBytes, 0, temp, 8, applicationSizeBytes.Length);
            var buffer = WindowsRuntimeBufferExtensions.AsBuffer(temp);

            return buffer;
        }

        /// <summary>
        /// Send image size information
        /// </summary>
        /// <returns></returns>
        private async Task<bool> sendImageSize()
        {
            try
            {   
                //TODO put the file names in a static variable or retrive the files from arguments of the main        
                var folder = await StorageFolder.GetFolderFromPathAsync(this.mainProgram.path);
                StorageFile img = await folder.GetFileAsync("s132_pca10040.bin");
                IBuffer firmwareImage_buffer = await FileIO.ReadBufferAsync(img);
                this.firmwareImage = firmwareImage_buffer.ToArray();

                packet = service.GetCharacteristics(new Guid(DFUService.DFUPacket)).FirstOrDefault();

                IBuffer buffer = ImageSizeCommand(GetSizeOfImage());

                GattCommunicationStatus status = await packet.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
                if (status == GattCommunicationStatus.Success)
                    return true;
            }
            catch (Exception e)
            {
                log("Error: [sendImageSize]" + e.Message, "DFUService");
            }
            return false;
        }
        
        /// <summary>
        /// RequestPacketReceiptNotificationCommand construction command
        /// Use the OpCode OpCode_PacketReceiptNotificationRequest (0x08)
        /// </summary>
        /// <param name="numberOfPackets"></param>
        /// <returns></returns>
        public IBuffer RequestPacketReceiptNotificationCommand(Int16 numberOfPackets)
        {
            var bytes = BitConverter.GetBytes(numberOfPackets);
            var temp = new byte[1 + bytes.Length];
            temp[0] = DeviceFirmwareUpdateControlPointCharacteristics.OpCode_PacketReceiptNotificationRequest;
            Array.Copy(bytes, 0, temp, 1, bytes.Length);
            var buffer = temp.AsBuffer();
            return buffer;
        }

        /// <summary>
        /// Calculate the number of packets to send
        /// </summary>
        /// <param name="trunks"></param>
        private void numberOfTimes(byte[][] trunks)
        {
            sendFullPackCompleteIndicator = (trunks.Length / NUMBER_OF_PACKET_AT_TIME) * NUMBER_OF_PACKET_AT_TIME;
            sendPartialPacketsNumberOfTimes = trunks.Length % NUMBER_OF_PACKET_AT_TIME;
        }
        

        /// <summary>
        /// Send the image file for each trunk
        /// (Called SendingImage in the nRFToolBox)
        /// </summary>
        /// <param name="trunks"></param>
        private void sendTrunks(byte[][] trunks)
        {
            log("Trunk len:" + trunks.Length, "Status");
            log("Trunk:" + sentTimes + " of " + sendFullPackCompleteIndicator, "Status");
            log("sendPartialPacketsNumberOfTimes:" + sendPartialPacketsNumberOfTimes, "Status");
            if (sentTimes == sendFullPackCompleteIndicator)
            {
                int limitation = sentTimes + sendPartialPacketsNumberOfTimes;
                WriteImage(trunks, limitation);
            }
            else if (sentTimes < sendFullPackCompleteIndicator)
            {
                int limitation = sentTimes + NUMBER_OF_PACKET_AT_TIME;
                WriteImage(trunks, limitation);
            }
        }

        /// <summary>
        /// Create a single buffer to write 
        /// </summary>
        /// <param name="trunk"></param>
        /// <param name="offSet"></param>
        /// <returns></returns>
        public IBuffer PartialOfFirmwareImage(byte[][] trunk, int offSet)
        {
            if (offSet > trunk.Length)
                throw new ApplicationArgumentException();
            var temp = trunk[offSet];
            var buffer = temp.AsBuffer();
            return buffer;
        }

        /// <summary>
        /// Write the image trunks to the packet characteristic
        /// </summary>
        /// <param name="trunks"></param>
        /// <param name="limitation"></param>
        private async void WriteImage(byte[][] trunks, int limitation)
        {
            while (sentTimes < limitation)
            {
                var buffer = PartialOfFirmwareImage(trunks, sentTimes);
                await packet.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
                sendedBytes += trunks[sentTimes].Length;
                sentTimes++;
            }
        }

        /// <summary>
        /// Write the image trunks to the packet characteristic
        /// </summary>
        /// <param name="trunks"></param>
        /// <param name="limitation"></param>
        private async void WriteImage2(byte[][] trunks, int limitation)
        {
            while (sentTimes < limitation)
            {
                if (sentTimes > trunks[sentTimes].Length)
                {
                    log("sentTimes:" + sentTimes + " trunks:" + trunks[sentTimes].Length, "Exception");
                    throw new ApplicationArgumentException();
                }

                /*
                 * var buffer = deviceFirmwareUpdateControlPointCharacteristics.
                 * PartialOfFirmwareImage(trunks, sentTimes);
                 */

                var temp = trunks[sentTimes];
                IBuffer buffer = temp.AsBuffer();

                GattCommunicationStatus status = await packet.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
                sendedBytes += trunks[sentTimes].Length;
                if (status == GattCommunicationStatus.Success)
                    log("Trunk:" + sentTimes + " of " + limitation + " Bytes:" + sendedBytes, "Status");
                else
                    log("Trunk:" + sentTimes + " Unreacheable", "Status");

                sentTimes++;
            }
        }

        /// <summary>
        /// Check the DFU Version
        /// </summary>
        /// <returns>1 if the device is not in DFU Mode. 8 if the device is already in DFU mode</returns>
        private async Task<int> checkDFUStatus()
        {
            //var dfuVersionCharacteristics = DFUService.Instance.Service.GetCharacteristics(new Guid(DFUService.DFUVersion));
            var dfuVersionCharacteristics = service.GetCharacteristics(new Guid(DFUService.DFUVersion));

            if (dfuVersionCharacteristics.Count > 0)
            {
                // Read the characteristic value
                GattReadResult readResult = await dfuVersionCharacteristics[0].ReadValueAsync();
                if (readResult.Status == GattCommunicationStatus.Success)
                {
                    byte[] value = new byte[readResult.Value.Length];
                    DataReader.FromBuffer(readResult.Value).ReadBytes(value);

                    if (value[0] == 1)
                        log("The DFU Version of your device is : 1", "DFUService");
                    else if (value[0] == 8)
                        log("The DFU Version of your device is : 8", "DFUService");
                    else
                        log("The DFU Version of your device is not recognized:" + System.Text.Encoding.Unicode.GetChars(value)[0], "DFUService");

                    return value[0];
                }
            }
            return 0;
        }

        /// <summary>
        /// Register to be notified when a connection is established to the Bluetooth device
        /// </summary>
        public void StartDeviceConnectionWatcher()
        {
            watcher = PnpObject.CreateWatcher(PnpObjectType.DeviceContainer,
                new string[] { "System.Devices.Connected" }, String.Empty);

            watcher.Updated += DeviceConnection_Updated;
            //watcher.add_Updated(DeviceConnection_Updated);
            watcher.Start();
        }

        /// <summary>
        /// Invoked when a connection is established to the Bluetooth device
        /// </summary>
        /// <param name="sender">The watcher object that sent the notification</param>
        /// <param name="args">The updated device object properties</param>
        private async void DeviceConnection_Updated(PnpObjectWatcher sender, PnpObjectUpdate args)
        {
            var connectedProperty = args.Properties["System.Devices.Connected"];
            bool isConnected = false;
            if ((deviceContainerId == args.Id) && Boolean.TryParse(connectedProperty.ToString(), out isConnected) &&
                isConnected)
            {
                var status = await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(
                    CHARACTERISTIC_NOTIFICATION_TYPE);

                if (status == GattCommunicationStatus.Success)
                {
                    IsServiceInitialized = true;

                    // Once the Client Characteristic Configuration Descriptor is set, the watcher is no longer required
                    watcher.Stop();
                    watcher = null;
                }

                // Notifying subscribers of connection state updates
                if (DeviceConnectionUpdated != null)
                {
                    DeviceConnectionUpdated(isConnected);
                }
            }
        }

        /// <summary>
        /// Star
        /// </summary>
        /// <returns></returns>
        private async Task StartFirmwareUpdate()
        {
            controlPoint = service.GetCharacteristics(new Guid(DFUService.DFUControlPoint)).FirstOrDefault();
            controlPoint.ValueChanged += controlPoint_ValueChanged;
            //controlPoint.add_ValueChanged(controlPoint_ValueChanged);
            if (await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(CHARACTERISTIC_NOTIFICATION_TYPE) == GattCommunicationStatus.Unreachable)
            {
                Console.WriteLine("ERROR: Device not connected succesfully. Please sure that the board has a DFU BLE Service.");
                return;
            }

            //If the board is not in DFU mode is necessary to switch in bootloader mode
            if (await checkDFUStatus() == 1)
                await this.switchOnBootLoader(controlPoint);
            try
            {
                //# Send 'START DFU' + Application Command 0x01
                if (await this.switchOnBootLoader(controlPoint))
                {
                    await this.sendImageSize();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Invoked when Windows receives data from your Bluetooth device.
        /// </summary>
        /// <param name="sender">The GattCharacteristic object whose value is received.</param>
        /// <param name="args">The new characteristic value sent by the device.</param>
        private async void controlPoint_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var data = new byte[args.CharacteristicValue.Length];

            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            // Process the raw data received from the device.
            //string[] stepAt = deviceFirmwareUpdateControlPointCharacteristics.ProcessData(data);
            var stepAt = deviceFirmwareUpdateControlPointCharacteristics.ProcessData(data);

            var messageType = stepAt[1];

            log("[controlPoint_ValueChanged]Step:" + stepAt[0] + " MessageType:" + messageType, "Steps");

            //Debug
            try
            {
                log("Step:" + stepAt[0] + " comfirmedBytes:" + Convert.ToInt32(stepAt[1]), "Steps");
            }
            catch (Exception e) { }

            if (messageType.Equals(OTHER_OP_CODE) || messageType.Equals(OTHER_RESPONSE_CODE))
            {
                if (PacketReceiptConfirmed != null)
                    PacketReceiptConfirmed(0, 0, messageType, stepAt[0]);
            }
            switch (stepAt[0])
            {
                //Go to next steps only
                case DfuOperationCode.StartDfuSucceded:
                    Console.WriteLine("StartDfuSucceded");
                    //await this.sendInitDFU();
                    var InitialPacketStart = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialzeDFUParameter, DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialPacketReceive);
                    await controlPoint.WriteValueAsync(InitialPacketStart);
                    //Transmit the Init image (DAT).
                    var folder = await StorageFolder.GetFolderFromPathAsync(this.mainProgram.path);
                    StorageFile dat = await folder.GetFileAsync("s132_pca10040.dat");
                    IBuffer initialPacket = await FileIO.ReadBufferAsync(dat);
                    await packet.WriteValueAsync(initialPacket, GattWriteOption.WriteWithoutResponse);

                    //Send 'INIT DFU' + Complete Command
                    var InitialPacketComplete = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialzeDFUParameter, DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialPacketComplete);
                    await controlPoint.WriteValueAsync(InitialPacketComplete);
                    break;
                case DfuOperationCode.InitialzeDFUParameterSucceded:

                    Console.WriteLine("InitializeDFUParameterSucceded");

                    IBuffer RequestPacketReceiptNotificationCommand = this.RequestPacketReceiptNotificationCommand(NUMBER_OF_PACKET_AT_TIME);
                    comStatus = await controlPoint.WriteValueAsync(RequestPacketReceiptNotificationCommand);

                    IBuffer ReceiveFirmwareImageCommand = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_ReceiveFirmwareImage);
                    comStatus = await controlPoint.WriteValueAsync(ReceiveFirmwareImageCommand);
                    log("Lenght:" + firmwareImage.Length, "Status");
                    firmwareImageTrunks = firmwareImage.Slice(MAX_SIZE_PER_GROUP);
                    this.numberOfTimes(firmwareImageTrunks);
                    sendTrunks(firmwareImageTrunks);

                    break;
                case DfuOperationCode.PacketReceiptNotification:
                    Console.WriteLine("PacketReceiptNotification");

                    var comfirmedBytes = Convert.ToInt32(stepAt[1]);
                    if (PacketReceiptConfirmed != null)
                        PacketReceiptConfirmed(comfirmedBytes, firmwareImage.Length, string.Empty, string.Empty);
                    if (comfirmedBytes == sendedBytes)
                        sendTrunks(firmwareImageTrunks);
                    break;
                case DfuOperationCode.ReceiveFirmwareImageSucceded:
                    Console.WriteLine("ReceiveFirmwareImageSucceded");
                    //Send this command when the entire image is sent
                    IBuffer ValidateFirmwareCommand = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_ValidateFirmware);
                    await controlPoint.WriteValueAsync(ValidateFirmwareCommand);

                    break;
                case DfuOperationCode.ValidateFirmareSucceded:
                    Console.WriteLine("ValidateFirmareSucceded");

                    IBuffer ActiveAndResetCommand = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_ActiveImageAndReset);
                    await controlPoint.WriteValueAsync(ActiveAndResetCommand);
                    if (DeviceFirmwareUpdateComplete != null)
                        DeviceFirmwareUpdateComplete(true);

                    Stop();
                    break;

            }

            if (ValueChangeCompleted != null)
            {
                ValueChangeCompleted(stepAt);
            }
        }

        public void Stop()
        {
            if (IsServiceChanged && IsServiceInitialized && controlPoint != null)
                //controlPoint.ValueChanged -= controlPoint_ValueChanged;
                //controlPoint.remove_ValueChanged(controlPoint_ValueChanged);
                return;
            else
            {
                //throw new ServiceNotInitializedException();
            }
        }

        private string _OTHER_RESPONSE_CODE = "ResponseCode";
        private string _OTHER_OP_CODE = "OpCode";
        private bool dfuInitialized = false;
        private Program mainProgram;

        string OTHER_RESPONSE_CODE
        {
            get
            {
                return _OTHER_RESPONSE_CODE;
            }
        }

        string OTHER_OP_CODE
        {

            get
            {
                return _OTHER_OP_CODE;
            }
        }

    }
}
