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
using System.Diagnostics;
using nrf52_bledfu_win_app;
using Windows.Devices.Bluetooth;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.System;

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
        private GattDescriptor cccd { get; set; }

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
        public static String CCCD = "00002902-0000-1000-8000-00805f9b34fb";
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


        private string _OTHER_RESPONSE_CODE = "ResponseCode";
        private string _OTHER_OP_CODE = "OpCode";
        private bool dfuInitialized = false;
        private MainPage mainProgram;
        //Given bin file
        private StorageFile bin_file;
        //Given dat file
        private StorageFile dat_file;


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
        /// Connect to the device, check if there's the DFU Service and start the firmware update
        /// </summary>
        /// <param name="program">The main class (Could be removed)</param>
        /// <param name="bin_file">The binary or hex file to upload</param>
        /// <param name="dat_file">The descriptor file</param>
        /// <returns></returns>
        public void initializeServiceAsync(MainPage program, StorageFile bin_file, StorageFile dat_file)
        {
            this.mainProgram = program;
            this.bin_file = bin_file;
            this.dat_file = dat_file;

            //TODO Need to add the possibility to choice the file type
            this.firmwareType = FirmwareTypeEnum.Application;
        }

        /// <summary>
        /// Connect to the device, check if there's the DFU Service and start the firmware update
        /// </summary>
        /// <param name="device">The device discovered</param>
        /// <returns></returns>
        public async Task connectToDevice(DeviceInformation device)
        {

            try
            {
                var deviceAddress = "N/A";
                if (device.Id.Contains("-"))
                    deviceAddress = device.Id.Split('-')[1];
                log("Connecting to:" + deviceAddress + "...", "");
                //Perform the connection to the device
                BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                bluetoothLeDevice.ConnectionStatusChanged += ConnectionStatusChanged;
                Debug.WriteLine("Name:" + bluetoothLeDevice.Name);
                int characteristicsControl = 0;
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync();
                if (result.Status == GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("Device " + deviceAddress + " connected. Updating firmware...");
                    //Scan the available services
                    var services = result.Services;
                    foreach (var service_ in services)
                    {
                        Debug.WriteLine("Service " + service_.Uuid);
                        //If DFUService found...
                        if (service_.Uuid == new Guid(DFUService.DFUService_UUID))
                        { //NRF52 DFU Service
                            Debug.WriteLine("DFU Service found");
                            IsServiceInitialized = true;
                            service = service_;
                            //Scan the available characteristics
                            GattCharacteristicsResult result_ = await service.GetCharacteristicsAsync();
                            if (result_.Status == GattCommunicationStatus.Success)
                            {
                                var characteristics = result_.Characteristics;
                                foreach (var characteristic in characteristics)
                                {
                                    Debug.WriteLine("Char " + characteristic.Uuid + "-");
                                    //Debug.WriteLine("Handle " + characteristic.AttributeHandle + "-");
                                    if (characteristic.Uuid.ToString() == DFUService.DFUControlPoint)
                                    {
                                        Debug.WriteLine("DFU Control point found " + characteristic.UserDescription);
                                        this.controlPoint = characteristic;
                                        characteristicsControl++;
                                    }
                                    else if (characteristic.Uuid.ToString() == DFUService.DFUPacket)
                                    {
                                        Debug.WriteLine("Packet found " + characteristic.UserDescription);
                                        this.packet = characteristic;
                                        characteristicsControl++;
                                    }
                                    else if (characteristic.Uuid.ToString() == DFUService.DFUVersion)
                                    {
                                        Debug.WriteLine("DFU Version found " + characteristic.UserDescription);
                                        this.dFUVersion = characteristic;
                                    }


                                    GattDescriptorsResult result2_ = await characteristic.GetDescriptorsAsync();
                                    var descriptors = result2_.Descriptors;
                                    foreach (var descriptor in descriptors)
                                    {
                                        Debug.WriteLine("Descr " + descriptor.Uuid + "-");
                                        Debug.WriteLine("Handle " + descriptor.AttributeHandle + "-");
                                        if (descriptor.Uuid.ToString() == DFUService.CCCD)
                                            this.cccd = descriptor;

                                    }


                                }
                                if (characteristicsControl == 2)
                                    await startFirmwareUpdate(device);
                                else {
                                    log("Controlpoint or packet not found", "");
                                }
                                //UNUSED_startFirmwareUpdate__();
                                //await StartFirmwareUpdate2_();                                        
                            }
                            break;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Status error: " + result.Status.ToString() + " need to restarte the device");

                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("ERROR: Accessing2 your device failed." + Environment.NewLine + e.Message + "\n" + e.StackTrace);
            }
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            log("Connection changed " + sender.ConnectionStatus, "");
        }

        /// <summary>
        /// StartFirmwareUpdate
        /// </summary>
        /// <returns></returns>
        private async Task startFirmwareUpdate(DeviceInformation device)
        {
            if (this.controlPoint.Uuid.ToString() != DFUService.DFUControlPoint)
            {
                Debug.WriteLine("ERROR: Control point not properly set");
                return;
            }
            try
            {
                log("startFirmwareUpdate", "");
                var properties = this.controlPoint.CharacteristicProperties;
                if (properties.HasFlag(GattCharacteristicProperties.Notify))
                {  
                    Debug.WriteLine("Control Point Notify OK");                    
                }
                else {
                    log("ERROR: Notification unable", "startFirmwareUpdate");
                    return;
                }
                
                //If the board is not in DFU mode is necessary to switch in bootloader mode
                if (await checkDFUStatus() == 1)
                {
                    //await Task.Delay(1000);
                    await switchOnBootLoader();
                    await Task.Delay(1000);
                    //this.controlPoint = null;
                    BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                    bluetoothLeDevice.Dispose();
                    await connectToDevice(device);
                    //var res = await device.Pairing.PairAsync();
                    //log("Pair:" + res.Status, "");
                    //
                    return;
                }
                else
                {
                    controlPoint.ValueChanged += controlPoint_ValueChanged;
                    // In order to avoid unnecessary communication with the device, determine if the device is already 
                    // correctly configured to send notifications.
                    // By default ReadClientCharacteristicConfigurationDescriptorAsync will attempt to get the current
                    // value from the system cache and communication with the device is not typically required.
                    await Task.Delay(1000);
                    var currentDescriptorValue = await controlPoint.ReadClientCharacteristicConfigurationDescriptorAsync();

                    if (currentDescriptorValue.Status == GattCommunicationStatus.Success)
                    {
                        log("Descriptor com success " + controlPoint.UserDescription, "");
                    }
                    else
                        log("Descriptor com UNsuccess " + controlPoint.UserDescription, "");
                    try
                    {
                        //await Task.Delay(1000);
                        Debug.WriteLine("Starting firmware update...");
                        

                        //# Send 'START DFU' + Application Command 0x04
                        if (await this.switchOnBootLoader())
                        {                            
                            await Task.Delay(1000);
                            var ret = await this.sendImageSize();
                            log("ImageSizeCommand res: " + ret, "");
                        }
                    }
                    catch (Exception e)
                    {
                        log("Mes: " + e.Message + "\nTrace: " + e.StackTrace, "");
                    }
                }
            }
            catch (Exception e) {
                log(e.Message + " " + e.StackTrace, "[startFirmwareUpdate]");
                Debug.WriteLine(e.Message + " " + e.StackTrace);
                await device.Pairing.UnpairAsync();
            }
        }

        /// <summary>
        /// Enable notifications and Send the 0x01 opcode and the application type data 04.
        /// </summary>
        /// <param name="controlPoint"></param>
        /// <returns></returns>
        private async Task<bool> switchOnBootLoader()
        {
            //Enable Notifications
            var properties = this.controlPoint.CharacteristicProperties;
            if (properties.HasFlag(GattCharacteristicProperties.Notify))
            {
                //log(""+controlPoint.Service.Device.ConnectionStatus, "");
                GattCommunicationStatus status = await controlPoint.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                {
                    //# Send 'START DFU' + Application Command
                    //self._dfu_state_set(0x0104)
                    var buffer = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_StartDfu, (byte)FirmwareTypeEnum.Application);
                    GattCommunicationStatus status1 = await controlPoint.WriteValueAsync(buffer);  //Go in DFU Mode
                    if (status1 == GattCommunicationStatus.Success)
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
                else
                {
                    log("Enable notificaton fail " + status, "switchOnBootLoader");
                    return false;
                }
            }
            else
                return false;

        }

        /// <summary>
        /// Write log data 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="appendname"></param>
        public async void log(string data, String appendname)
        {
            this.mainProgram.log(data, appendname);

        }

        public void ShowBluetoothOffErrorMessage()
        {
            
            var alternative1 = new UICommand("Go to settings", new UICommandInvokedHandler(GoToBluetoothSettingPage), 0);
            var alternative2 = new UICommand("Close", new UICommandInvokedHandler(CloseBluetoothIsOffMessage), 1);
            //ShowMessage(BluetoothIsOffMessageTitle, BluetoothIsOffMessageContent, alternative1, alternative2);
            
        }

        private void CloseBluetoothIsOffMessage(IUICommand command)
        { }

        private async void GoToBluetoothSettingPage(IUICommand command)
        {
            await Window.Current.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:"));
            });
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
                log("sendImageSize", "");
                //TODO put the file names in a static variable or retrive the files from arguments of the main        
                //var folder = await StorageFolder.GetFolderFromPathAsync(this.mainProgram.path);
                //StorageFile img = await folder.GetFileAsync("s132_pca10040.bin");
                IBuffer firmwareImage_buffer = await FileIO.ReadBufferAsync(this.bin_file);
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
           
            // Read the characteristic value
            GattReadResult readResult = await this.dFUVersion.ReadValueAsync();
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
            else
                return 0;
            
            
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
        /// Invoked when Windows receives data from your Bluetooth device.
        /// </summary>
        /// <param name="sender">The GattCharacteristic object whose value is received.</param>
        /// <param name="args">The new characteristic value sent by the device.</param>
        private async void controlPoint_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            log("controlPoint_ValueChanged", "");
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
                    Debug.WriteLine("StartDfuSucceded");
                    //await this.sendInitDFU();
                    var InitialPacketStart = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialzeDFUParameter, DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialPacketReceive);
                    await controlPoint.WriteValueAsync(InitialPacketStart);
                    //Transmit the Init image (DAT).
                    //var folder = await StorageFolder.GetFolderFromPathAsync(this.mainProgram.path);
                    //StorageFile dat = await folder.GetFileAsync("s132_pca10040.dat");
                    IBuffer initialPacket = await FileIO.ReadBufferAsync(this.dat_file);
                    await packet.WriteValueAsync(initialPacket, GattWriteOption.WriteWithoutResponse);

                    //Send 'INIT DFU' + Complete Command
                    var InitialPacketComplete = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialzeDFUParameter, DeviceFirmwareUpdateControlPointCharacteristics.OpCode_InitialPacketComplete);
                    await controlPoint.WriteValueAsync(InitialPacketComplete);
                    break;
                case DfuOperationCode.InitialzeDFUParameterSucceded:

                    Debug.WriteLine("InitializeDFUParameterSucceded");

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
                    Debug.WriteLine("PacketReceiptNotification");

                    var comfirmedBytes = Convert.ToInt32(stepAt[1]);
                    if (PacketReceiptConfirmed != null)
                        PacketReceiptConfirmed(comfirmedBytes, firmwareImage.Length, string.Empty, string.Empty);
                    if (comfirmedBytes == sendedBytes)
                        sendTrunks(firmwareImageTrunks);
                    break;
                case DfuOperationCode.ReceiveFirmwareImageSucceded:
                    Debug.WriteLine("ReceiveFirmwareImageSucceded");
                    //Send this command when the entire image is sent
                    IBuffer ValidateFirmwareCommand = getBufferFromCommand(DeviceFirmwareUpdateControlPointCharacteristics.OpCode_ValidateFirmware);
                    await controlPoint.WriteValueAsync(ValidateFirmwareCommand);

                    break;
                case DfuOperationCode.ValidateFirmareSucceded:
                    Debug.WriteLine("ValidateFirmareSucceded");

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
