﻿using OTADFUApplication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace ConsoleApp2
{
    public class Program
    {
        /// <summary>
        /// The version of the application
        /// </summary>
        String version = "0.1";
        String app_name = "Arduino OTA_DFU for Nordic nRF5x";
        StreamWriter logFile = null;
        /// <summary>
        /// The default path of the log file
        /// </summary>
        String logPath = @".\logs\";
        public static Boolean verboseMode = false;


        static void Main(string[] args)
        {
            Console.WriteLine("Test");

            Program program = new Program();

            Console.WriteLine(program.app_name + " version " + program.version);

            Program.verboseMode = program.checkArgs(args, "-v");
            if (Program.verboseMode)
                Console.WriteLine("Verbose Mode");

            //if (args.Length  > 0) //debug
            if (args.Length == 0)
                Console.WriteLine("Usage: otadfu [help] or [scan] or [update -f <bin_file> -d <dat_file> -a <device_address>] or [update -z <zip_file> -a <device_address>] or [update -h <hex_file> -a <device_address>]");
            else if (args[0] == "help")
            {
                Console.WriteLine("OTA DFU update application for nrf5x MCUs. Visit https://github.com/astronomer80/nrf52_bledfu_win for more information");
                Console.WriteLine("Here a list of commands available:");
                Console.WriteLine("help: Show this help");
                Console.WriteLine("version: Show the version");
                Console.WriteLine("scan: Scan BLE devices already paired with Windows Settings");
                Console.WriteLine("update -f < bin_file > -d < dat_file > -a <device_address>. bin_file is the file generated from the Arduino IDE. dat_file is the init packet generated by nrfutil application. device_address: is the address of the device returned using 'scan' command.");
                Console.WriteLine("update -z < zip_file > -a <device_address>. zip_file is the archive generated by nrfutil application. device_address: is the address of the device returned using 'scan' command.");
                Console.WriteLine("update -h < hex_file/bin_file > -a <device_address>. hex_file or bin_file are the files generated from the Arduino IDE. dat_file is the init packet generated by nrfutil application. device_address: is the address of the device returned using 'scan' command.");
            }
            //Scan only paired BLE devices
            else if (args[0] == "version")
            {
                Console.WriteLine(program.app_name + " version " + program.version);
            }
            //Scan only paired BLE devices
            else if (args[0] == "scan")
            {
                program.MainTask(@".\logs\", true, "", "", "");
                program.discovery_draft();

            }
            //Update procedure
            else if (args[0] == "update")
            {
                //bool check=program.checkArgs(args, "-f -d -a");
                //Console.WriteLine(check);

                if (args.Length >= 7 && args[1] == "-f" && args[3] == "-d" && args[5] == "-a")
                {
                    Console.WriteLine("Update from bin and dat files");
                    if (!File.Exists(args[2]))
                        Console.WriteLine("Error: the " + args[2] + " file doesn't exist");
                    if (!File.Exists(args[4]))
                        Console.WriteLine("Error: the " + args[4] + " file doesn't exist");
                    else
                        program.MainTask(Path.GetDirectoryName(args[3]), false, args[2], args[4], args[6]);

                }
                else if (args.Length >= 5 && args[1] == "-h" && args[3] == "-a")
                {
                    Console.WriteLine("Update from hex/bin file");
                    String hex_file = args[2];
                    if (!File.Exists(hex_file))
                        Console.WriteLine("Error: the " + hex_file + " file doesn't exist");
                    else
                    {
                        String zip_file = hex_file.Replace(".hex", ".zip").Replace(".bin", ".zip");
                        //Console.WriteLine(hex_file);
                        //Console.WriteLine(zip_file);
                        String device_address = args[4];
                        //nrfutil dfu genpkg app_package.zip--application application.hex
                        try
                        {
                            string nrfutilexefilename = @"nrfutil.exe";
                            if (!File.Exists(nrfutilexefilename))
                                Console.WriteLine("nrfutil.exe not found. It should be in the same directory of this executable");
                            else
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = nrfutilexefilename, Arguments = "dfu genpkg " + zip_file + " --application " + hex_file, };
                                Process proc = new Process() { StartInfo = startInfo, };
                                startInfo.UseShellExecute = false;
                                startInfo.RedirectStandardOutput = true;
                                startInfo.RedirectStandardError = true;
                                proc.Start();
                                string output = proc.StandardOutput.ReadToEnd();
                                string error = proc.StandardError.ReadToEnd();
                                Console.WriteLine(output);
                                Console.WriteLine(error);
                                proc.Close();

                                program.updateFromZip(zip_file, device_address);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error with NRFUTIL");
                            Console.WriteLine(ex.StackTrace);
                        }
                    }
                }
                else if (args.Length >= 5 && args[1] == "-z" && args[3] == "-a")
                {
                    Console.WriteLine("Update from zipped package");
                    program.updateFromZip(args[2], args[4]);
                }
                else
                    Console.WriteLine("Invalid update command. Type 'otadfu help' for more information");
            }
            else
            {
                Console.WriteLine("Unknown command. Type 'otadfu help' for more information");

            }

            //DEBUG
            Console.WriteLine("[DEBUG]Press a key to close at the end");
            Console.ReadLine();
        }

        private void discovery_draft()
        {
            // Query for extra properties you want returned
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
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

        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            throw new NotImplementedException();
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Console.WriteLine("[DeviceWatcher_EnumerationCompleted]");
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate device)
        {
            //Console.WriteLine("[DeviceWatcher_Removed]" + device.Id);

            String deviceAddress = device.Id.Split('-')[1].Split('#')[0];
            Console.WriteLine("Removed Device address:[" + deviceAddress + "]");
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            throw new NotImplementedException();
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation device)
        {

            //Console.WriteLine("[DeviceWatcher_Added]" + device.Name + " ID:" + device.Id);
            String deviceAddress = device.Id.Split('-')[1].Split('#')[0];

            Console.WriteLine("Device name:[" + device.Name + "] Device address:[" + deviceAddress + "]");



        }

        async void ConnectDevice(DeviceInformation deviceInfo)
        {
            // Note: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
            // ...
        }

        /// <summary>
        /// It returns true if on the arguments there are all the parameters in the conditions variable
        /// </summary>
        /// <param name="args">Main arguments</param>
        /// <param name="conditions"></param>
        /// <returns></returns>
        private bool checkArgs(String[] args, String conditions)
        {
            bool ret = true;
            String argparams = "";
            String[] conditions_a = conditions.Split(' ');
            foreach (String a in args)
            {
                argparams += a;
            }
            foreach (String c in conditions_a)
            {
                if (!argparams.Contains(c))
                    ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Create the log file in the same folder of the file to upload
        /// </summary>
        /// <param name="logPath"></param>
        private void createLog(String logPath)
        {
            this.logPath = logPath + @"\";
            String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            String logfilename = "[" + time + "]_" + app_name + "_LOG.txt";

            Console.WriteLine("Log filename:" + logfilename);
            try
            {
                Directory.CreateDirectory(logPath);
                logFile = new StreamWriter(logPath + "\\" + logfilename, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Launch the update process starting from a zip file
        /// </summary>
        /// <param name="zip_file"></param>
        /// <param name="device_address"></param>
        private void updateFromZip(String zip_file, String device_address)
        {
            if (!File.Exists(zip_file))
                Console.WriteLine("Error: the " + zip_file + " file doesn't exist");
            else
            {
                String file_dir = Path.GetDirectoryName(zip_file);
                try
                {
                    String bin_file = zip_file.Replace(".zip", ".bin");
                    String dat_file = zip_file.Replace(".zip", ".dat");
                    //Remove previuous file with the same file name
                    File.Delete(bin_file);
                    File.Delete(dat_file);
                    File.Delete(file_dir + "\\manifest.json");
                    //Extract the zip file in the same directory of the exe file
                    ZipFile.ExtractToDirectory(zip_file, file_dir);
                    this.MainTask(file_dir, false, bin_file, dat_file, device_address);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

        }

        /// <summary>
        /// Write log data 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="appendname"></param>
        public void log(string data, string tag)
        {
            Console.WriteLine(data);
            try
            {
                if (tag.Equals(""))
                    this.logFile.WriteLine("[" + tag + "]" + data);
                else
                    this.logFile.WriteLine(data);
                this.logFile.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Defines the Main asynchronous task
        /// </summary>
        /// <returns></returns>
        public async Task MainTask(String logPath, bool scanonly, String bin_file, String dat_file, String device_address)
        {
            this.createLog(logPath);
            //this.log("MainTask", "");
            try
            {
                await scanpaireddevices(scanonly, bin_file, dat_file, device_address);
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Exception on readevices");
            }
        }

        /// <summary>
        /// Read the list of paired devices
        /// </summary>
        /// <param name="scanonly">If true return only the list of paired devices. 
        /// If false starts the OTA for the device address defined at the argument of Main</param>
        /// <returns></returns>
        private async Task scanpaireddevices(bool scanonly, String bin_file, String dat_file, String given_device_address)
        {
            this.log("Scanning BLE devices...", "");
            Guid UUID = new Guid(DFUService.DFUService_UUID); //NRF52 DFU Service
            //Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service            
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            Thread.Sleep(2000);  //TODO Remove this delay
            if (devices.Count > 0)
            {
                Console.WriteLine("BLE devices already paired found:");
                bool found = false;
                foreach (DeviceInformation device in devices)
                {
                    var deviceAddress = "not available";
                    //Console.WriteLine(device.Name + " " + device.Id);                    
                    //Parse device address
                    if (device.Id.Contains("_") && device.Id.Contains("#"))
                        deviceAddress = device.Id.Split('_')[1].Split('#')[0];
                    Console.WriteLine("Device name:[" + device.Name + "] Device address:[" + deviceAddress + "]");
                    //foreach (var prop in device.Properties) {
                    //    Console.WriteLine(prop.Key + " " + prop.Value);                        
                    //}                    
                    if (!scanonly && given_device_address == deviceAddress)
                    //TODO Only for test
                    //if (!scanonly && true)
                    {
                        found = true;
                        try
                        {
                            //DFUService dfs =DFUService.Instance;
                            //await dfs.InitializeServiceAsync(device);
                            await DFUService.Instance.InitializeServiceAsync(device, this, bin_file, dat_file);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }

                    }

                    if (!found)
                        Console.WriteLine("No devices found for the given address: " + given_device_address);

                }
            }
            else
            {
                this.log("No paired BLE devices found. Pair you BLE device in the windows settings first", "Main");
            }


        }
    }
}
