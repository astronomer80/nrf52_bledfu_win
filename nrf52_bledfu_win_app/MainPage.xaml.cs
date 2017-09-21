using OTADFUApplication;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.AppService;
using GalaSoft.MvvmLight.Messaging;
using Windows.Foundation.Collections;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

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
        //String given_device_address = "e7:59:c9:7e:da:1b";
        String given_device_address = "e8:53:c7:3c:fc:e8";
        private static GattDeviceService service { get; set; }
        public static Boolean verboseMode = true;
        StorageFile bin_file =null, dat_file=null, hex_file = null, zip_file = null;
        String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        private TextBox textBox;
        
        public event RoutedEventHandler TextBoxLoad;
        //public event RoutedEventHandler ButtonsLoad;
        public event RoutedEventHandler PanelLoad;
        public event RoutedEventHandler DevicesListBox_Load;
        public event RoutedEventHandler Progressbar_Load;
        ListBox DevicesListBox;
        private ProgressBar progressbar;
        
        Dictionary<String, DeviceInformation> elementslist = new Dictionary<string, DeviceInformation>();
        StorageFolder localFolder;
        bool flag;
        
        public MainPage()
        {
            this.InitializeComponent();

            time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logfilename = "[" + time + "]_" + app_name + "_LOG.txt";
            this.scanonly = false;

            this.init();
        }


        /// <summary>
        /// Defines the Main asynchronous task
        /// </summary>
        /// <returns></returns>
        public async Task init()
        {
            TextBoxLoad += new RoutedEventHandler(textBoxLoaded);
            //ButtonsLoad += new RoutedEventHandler(ButtonsLoaded);
            PanelLoad += new RoutedEventHandler(panelLoaded);
            DevicesListBox_Load += new RoutedEventHandler(devicesListBox_Loaded);
            Progressbar_Load += new RoutedEventHandler(progressbarLoaded);
            Debug.WriteLine("LogPath:" + logfilename);

            Messenger.Default.Register<Migrate.UWP.Messages.ConnectionReadyMessage>(this, message =>
            {
                if (App.Connection != null)
                {
                    App.Connection.RequestReceived += Connection_RequestReceived;
                    App.Connection.ServiceClosed += AppServiceConnection_ServiceClosed;
                    App.taskInstance.Canceled += TaskInstance_Canceled;
                }
            });

            // Retrieve local folder path
            localFolder = ApplicationData.Current.LocalFolder;

            try
            {
                if (this.scanonly == true)
                    this.log("Scan mode Only", "");

                this.log(this.app_name, "");
          
                this.discovery();
                
                //await scanpaireddevices(scanonly, bin_file, dat_file, device_address);
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
            }
        }

        private void Scanbutton_Click(object sender, RoutedEventArgs e)
        {
            progressbar.Visibility = Visibility.Collapsed;
            this.discovery();
        }

        private async void Filebutton_Click(object sender, RoutedEventArgs e)
        {

            progressbar.Visibility = Visibility.Collapsed;
            await this.getFiles();
        }

        private async void DevicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (this.bin_file == null && this.dat_file == null)
            {
                log("Please first select the file you want to upload.", "");
                return;
            }

            if (args.AddedItems.Count == 0)
                return;
            
            String label = (String)args.AddedItems[args.AddedItems.Count - 1];
            
            DeviceInformation device = elementslist[label.Split('\n')[1]];
            DFUService.Instance.initializeServiceAsync(this, bin_file, dat_file);
            this.progressbar.Visibility = Visibility.Visible;
            await DFUService.Instance.connectToDevice(device);


        }

        private void devicesListBox_Loaded(object sender, RoutedEventArgs args)
        {
            this.DevicesListBox = (ListBox)sender;
           
        //    DevicesListBox.Items.Add("String");
        }

        private void progressbarLoaded(object sender, RoutedEventArgs args)
        {
            this.progressbar = (ProgressBar)sender;
        }

        private void textBoxLoaded(Object sender, RoutedEventArgs e)
        {
            this.textBox= (TextBox)sender;

        }

        private void panelLoaded(Object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Panel loaded");
            RelativePanel panel = (RelativePanel)sender;

        }

        private void ButtonsLoaded(Object sender, RoutedEventArgs e)
        {

            Button button = (Button)sender;
            Button button1 = button;

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

                //delete old output from text view 
                this.textLog = "";
                this.log(this.app_name, "");

                //clear file variables from the old file
                this.bin_file = null;
                this.dat_file = null;
                this.zip_file = null;
                this.hex_file = null;

                //restore progress bar default value
                this.updateProgressBar(0);

                var filelist = await openPicker.PickMultipleFilesAsync();
                foreach (var file in filelist)
                {
                    log(file.Name, "");
                    if (file.Name.EndsWith(".bin"))
                        this.bin_file = file;

                    if (file.Name.EndsWith(".dat"))
                        this.dat_file = file;

                    if (file.Name.EndsWith(".hex"))
                        this.hex_file = file;

                    if (file.Name.EndsWith(".zip"))
                        this.zip_file = file;
                }
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
                return;
            }

            // Check if user chose some weird combination
            if ((this.bin_file != null && this.hex_file != null) || (this.bin_file != null && this.zip_file != null) || (this.hex_file != null && this.zip_file != null))
            {
                log("Please select just one .zip, .bin, .hex file or both .bin and .dat files", "");
                this.bin_file = null;
                this.dat_file = null;
                this.zip_file = null;
                this.hex_file = null;
                return;
            }

            if (this.bin_file != null && this.dat_file != null)
                //files are ready, return
                return;

            if(this.zip_file != null)
            {
                manageZip();
                return;
            }          

            //use nrfutil if needed
            if (this.hex_file != null | this.bin_file != null)
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
        }

        /// <summary>
        /// Function called when AppService is up and running. Use the communication channel to send the file to be passed to nrfutil
        /// and the folder where the nrfutil output will be written. When nrfutil complete its job TaskInstance_Canceled should be
        /// called.
        /// </summary>
        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            try
            {
                File.Delete(localFolder.Path + "\\output.zip");
            }
            catch (ArgumentNullException)
            {
                // File doesn't exist. That's fine, continue.
            }
            var deferral = args.GetDeferral();
            ValueSet value = new ValueSet();
            // send local folder path since nrfutil doesn't have right to write the output file in the installation folder
            value.Add("path", this.localFolder.Path);
            if (this.hex_file != null)
                value.Add("file", this.hex_file.Path);
            else if (this.bin_file != null)
                value.Add("file", this.bin_file.Path);
            else {
                this.log("Error. Please try again", "");
                value.Add("err", null);
            }
            await args.Request.SendResponseAsync(value);
            deferral.Complete();
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            App.appServiceDeferral.Complete();
            // nrfutil has done its job. zip file is ready
            manageZip();
        }

        private void AppServiceConnection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            App.appServiceDeferral.Complete();
            // nrfutil has done its job. zip file is ready
            manageZip();
        }

        /// <summary>
        /// Extract .zip file and put .dat and .bin files in the respective global variables 
        /// </summary>
        private async void manageZip()
        {
            string filePath;
            if(zip_file != null)
            { // user chose a zip file
                filePath = zip_file.Path;
            }
            else
            { // nrfutil was used to create the zip file
                filePath = localFolder.Path + "\\output.zip";
                if (!File.Exists(filePath))
                {
                    log("nrfutil failed to create the package. Please try again.", "Error");
                    return;
                }
            }

            // Remove the old files if any
            if(Directory.Exists(localFolder.Path + "\\output"))
                Directory.Delete(localFolder.Path + "\\output", true);
            ZipFile.ExtractToDirectory(filePath, localFolder.Path + "\\output");
            StorageFolder outDirectory = await localFolder.GetFolderAsync("output");
            String prova = outDirectory.Path;
            foreach (string file in Directory.GetFiles(outDirectory.Path))
            {
                // File path is absolute. Retrieve file name by picking the substring from the last "\" + 1 to remove "\" character
                string filename = file.Substring(file.LastIndexOf("\\") + 1);
                
                if (filename.EndsWith(".bin"))
                    bin_file = await outDirectory.GetFileAsync(filename);
                        
                else if (filename.EndsWith(".dat"))
                    dat_file = await outDirectory.GetFileAsync(filename);
            }
        }

        public async void updateProgressBar(int value)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        if (this.progressbar != null)
                            this.progressbar.Value = value;
                    });
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
                        if( this.textBox != null)
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
            DeviceInformation dev = elementslist[deviceAddress];
            elementslist.Remove(deviceAddress);
            Debug.WriteLine("Removed Device address:[" + deviceAddress + "]");

            removeDevice(dev.Name + "\n" + deviceAddress);    
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        private async void removeDevice(String label)
        {
         await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DevicesListBox.Items.Remove(label);
            });
        }

        private async void addDevice(String devicename)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                { //TODO : manage System.NullReferenceException: 'Object reference not set to an instance of an object.'
                    try
                    {
                        DevicesListBox.Items.Add(devicename);
                    }
                    catch (System.NullReferenceException) { }
                });
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

            //check if the device has already been scanned before to add it
            DeviceInformation dev;
            elementslist.TryGetValue(deviceAddress, out dev);
            if (dev == null)
            { //device is not present yet
                String label = device.Name + "\n" + deviceAddress;
                addDevice(label);
                elementslist.Add(deviceAddress, device);
            }
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
          //          DFUService.Instance.initializeServiceAsync(this, bin_file, dat_file);
                    
          //          DFUService.Instance.connectToDevice(device);

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
