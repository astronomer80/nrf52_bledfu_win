using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Security.Permissions;
using System.Security;
using System.Threading;

namespace OTADFUApplication
{    
    /// <summary>
    /// The main program
    /// </summary>
    public class Program
    {  
        public String macAddress = "";
        public String path = @"E:\nrf52_bledfu_win_console";
        StreamWriter logFile = null;
        String logPath = @"C:\logs\";

        Program(){
            String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            String logfilename = "[" + time + "].txt";
            
            Console.WriteLine("Log filename:" + logfilename);
            try
            {
                logFile = new StreamWriter(logPath + logfilename, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(String[] args)
        {            
            if(args.Length>0)
                Console.WriteLine("Arg0 " + args[0]);
   
            var program = new Program();
            var task1= program.MainTask();
            
            Console.ReadLine();
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
        public async Task MainTask()
        {
            this.log("MainTask", "");
            try
            {
                bool dfuMode = true;
                await readevices(dfuMode);                

            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Test");
            }
        }

        /// <summary>
        /// Read the list of paired devices
        /// </summary>
        /// <param name="dfuMode">If false return only the list of paired devices. 
        /// If false starts the OTA for the device address defined at the argument of Main</param>
        /// <returns></returns>
        private async Task readevices(bool dfuMode)
        {
            this.log("readevices", "");
            //Guid UUID = new Guid(DFUService.DFUService_UUID); //NRF52 DFU Service
            Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service            
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };         
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            Thread.Sleep(2000);
            if (devices.Count > 0)
            {
                foreach (DeviceInformation device in devices)
                {
                    var deviceAddress = "not available";
                    //Console.WriteLine(device.Name + " " + device.Id);                    
                    //Parse device address
                    if (device.Id.Contains("_") && device.Id.Contains("#"))
                        deviceAddress = device.Id.Split('_')[1].Split('#')[0];
                    Console.WriteLine(device.Name + " " + deviceAddress);
                    //foreach (var prop in device.Properties) {
                    //    Console.WriteLine(prop.Key + " " + prop.Value);                        
                    //}
                    //TODO
                    //if(this.macAddress==deviceAddress)
                    if (true) {
                        try
                        {
                            //DFUService dfs =DFUService.Instance;
                            //await dfs.InitializeServiceAsync(device);
                            await DFUService.Instance.InitializeServiceAsync(device, this);
                        }
                        catch (Exception e) {
                            Console.WriteLine(e);
                        }
                        
                    }
                    
                }
            }
            else
            {
                this.log("No devices", "Test");
            }
            Console.WriteLine("Press a key to close");

        }
    }
}
