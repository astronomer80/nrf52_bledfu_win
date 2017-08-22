using OTADFUApplication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace nrf52_bledfu_win_app
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CoreDispatcher dispatcher { get; set; }
        String textLog = "";
        String logfilename = "";
        String version = "0.1";
        String app_name = "Arduino OTA_DFU for Nordic nRF5x";
        bool scanonly, devicefound;
        String given_device_address = "cc:32:24:e9:13:1a";
        //String given_device_address = "e8:53:c7:3c:fc:e8";
        private static GattDeviceService service { get; set; }
        public static Boolean verboseMode = true;
        StorageFile bin_file =null, dat_file=null;
        String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        public MainPage()
        {
            this.InitializeComponent();
            time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logfilename = "[" + time + "]_" + app_name + "_LOG.txt";
            this.scanonly = false;
            if (this.scanonly == true)
                this.log("Scan mode Only", "");
            this.init();
        }

        /// <summary>
        /// Defines the Main asynchronous task
        /// </summary>
        /// <returns></returns>
        public async Task init()
        {
            Debug.WriteLine("LogPath:" + logfilename);
            //this.log("MainTask", "");
            try
            {
                this.log(this.app_name, "");
                await this.getFiles();

                if (this.bin_file != null && this.dat_file != null)
                    this.discovery();
                else
                    log("Both bin and dat file not found", "");
                //await scanpaireddevices(scanonly, bin_file, dat_file, device_address);
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
            }
        }

        async Task getFiles()
        {
            try
            {
                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.List;
                openPicker.CommitButtonText = "Bin File";
                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".bin");
                openPicker.FileTypeFilter.Add(".dat");
                openPicker.FileTypeFilter.Add(".zip");
                openPicker.FileTypeFilter.Add(".hex");
                
                var filelist = await openPicker.PickMultipleFilesAsync();
                foreach (var file in filelist)
                {
                    log(file.Name, "");
                    if (file.Name.EndsWith(".bin"))
                        this.bin_file = file;

                    if (file.Name.EndsWith(".dat"))
                        this.dat_file = file;
                }
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
            }
        }

        async Task testAsync()
        {
            try
            {
                        StorageFolder folder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                //StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("test.txt"));
                //var file1 = installedLocation.GetFileAsync("test.txt");
                //StorageFile file = await StorageFile.GetFileFromPathAsync("test.txt");


                //var folder = await StorageFolder.GetFolderFromPathAsync(@"C:\Primo");
                StorageFile img = await folder.GetFileAsync(@"app_blink_dfu_500\s132_pca10040.bin");

                IBuffer firmwareImage_buffer = await FileIO.ReadBufferAsync(img);
                var test = firmwareImage_buffer.ToArray();
                Debug.WriteLine(test);

                

            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
            }
        }

        public async void log(String message, String tag)
        {
            if (tag != "")
                message = "[" + tag + "] " + message;

            Debug.WriteLine(message);
            try
            {
                this.textLog = this.textLog + "\n" + message;
                this.writeOnFile(message);

                try
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        this.textBox.Text = this.textLog;
                    });

                }
                catch (Exception e1)
                {
                    Debug.WriteLine("[log5]" + e1.Message + " " + e1.StackTrace);

                }

            }
            catch (Exception e)
            {
                Debug.WriteLine("[log1]" + e.Message + " " + e.StackTrace);
            }
        }

        /// <summary>
        /// Discovery BLE devices in range
        /// </summary>
        private void discovery()
        {
            Debug.WriteLine("Discovering devices...");
            // Query for extra properties you want returned
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher.Start();
            
            // Query for extra properties you want returned
            DeviceWatcher deviceWatcher2 =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher2.Added += DeviceWatcher_Added;
            deviceWatcher2.Updated += DeviceWatcher_Updated;
            deviceWatcher2.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher2.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher2.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher2.Start();

        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            //throw new NotImplementedException();
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Debug.WriteLine("[DeviceWatcher_EnumerationCompleted]");
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate device)
        {
            //Console.WriteLine("[DeviceWatcher_Removed]" + device.Id);

            String deviceAddress = device.Id.Split('-')[1].Split('#')[0];
            Debug.WriteLine("Removed Device address:[" + deviceAddress + "]");
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Event called when a new device is discovered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="device"></param>
        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation device)
        {
            //Console.WriteLine("[DeviceWatcher_Added]" + device.Name + " ID:" + device.Id);
            String deviceAddress = device.Id.Split('-')[1];

            Debug.WriteLine("Found Device name:[" + device.Name + "] Device address:[" + deviceAddress + "]");
            //Guid UUID = new Guid(DFUService.DFUService_UUID); //NRF52 DFU Service
            //Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service            
            //String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };

            //foreach (var prop in device.Properties) {
            //    Console.WriteLine(prop.Key + " " + prop.Value);                        
            //}                    
            //Console.WriteLine("Scan: " + scanonly + " Given:" + given_device_address + " Found:" + deviceAddress);                        

            if (!scanonly && given_device_address == deviceAddress)
            //TODO Only for test
            //if (!scanonly && true)
            {
                this.devicefound = true;
                try
                {
                    //DFUService dfs =DFUService.Instance;
                    //await dfs.InitializeServiceAsync(device);                    
                    DFUService.Instance.initializeServiceAsync(this, bin_file, dat_file);
                    
                    DFUService.Instance.connectToDevice(device);

                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

            }
        }

        /// <summary>
        /// Read the list of paired devices
        /// </summary>
        /// <param name="dfuMode">If false return only the list of paired devices. 
        /// If false starts the OTA for the device address defined at the argument of Main</param>
        /// <returns></returns>
        private async Task discovery_old(bool dfuMode)
        {
            this.log("readevices", "");
            Guid UUID = new Guid(DFUService.DFUService_UUID); //NRF52 DFU Service
            //Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service            
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            if (devices.Count > 0)
            {
                foreach (DeviceInformation device in devices)
                {
                    var deviceAddress = "not available";
                    //Console.WriteLine(device.Name + " " + device.Id);                    
                    //Parse device address
                    if (device.Id.Contains("_") && device.Id.Contains("#"))
                        deviceAddress = device.Id.Split('_')[1].Split('#')[0];
                    Debug.WriteLine(device.Name + " " + deviceAddress);
                    //foreach (var prop in device.Properties) {
                    //    Console.WriteLine(prop.Key + " " + prop.Value);                        
                    //}
                    //TODO
                    //if(this.macAddress==deviceAddress)
                    if (true)
                    {
                        try
                        {
                            //DFUService dfs =DFUService.Instance;
                            //await dfs.InitializeServiceAsync(device);
                            //await DFUService.Instance.initializeServiceAsync(device, this);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }

                    }

                }
            }
            else
            {
                this.log("No devices", "Test");
            }
            Debug.WriteLine("Press a key to close");

        }



        private async void writeOnFile(String message)
        {
            try
            {
                // Create sample file; replace if exists.
                Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                Windows.Storage.StorageFile sampleFile = await storageFolder.CreateFileAsync(logfilename, Windows.Storage.CreationCollisionOption.OpenIfExists);

                await Windows.Storage.FileIO.AppendTextAsync(sampleFile, message + "\n");
            }
            catch (Exception e)
            {
                Debug.WriteLine("[log]" + e.Message + " " + e.StackTrace);

            }

        }
    }
}
