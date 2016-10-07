using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Security.Permissions;
using System.Security;
using System.Threading;

namespace WindowsFormsApplication1
{
    
    class Test {
        public Test() {
            this.init();
        }
        StreamWriter logFile=null;
        String logPath = @"C:\logs\";
        private void init() {
            String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            String filename = "[" + time + "].txt";
            Console.WriteLine(filename);
            try
            {
                logFile = new StreamWriter(logPath + filename, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        public async Task Test1()
        {
            this.log("Test", "");
            try
            {
                await readevices();
            }
            catch (Exception e)
            {
                this.log(e.StackTrace, "Test");
            }

        }


        private async  Task readevices()
        {
            //Guid UUID = new Guid(DFUService.DFUService_UUID); //NRF52 DFU Service
            Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service            
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };
            this.log("TEST1", "");
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            Thread.Sleep(2000);
            if (devices.Count>0) 
            {
                foreach (DeviceInformation device in devices)
                {
                    var deviceAddress = "not available";
                    //Console.WriteLine(device.Name + " " + device.Id);                    
                    if (device.Id.Contains("_") && device.Id.Contains("#"))
                        deviceAddress = device.Id.Split('_')[1].Split('#')[0];
                    Console.WriteLine(device.Name + " " + deviceAddress);
                    //foreach (var prop in device.Properties) {
                    //    Console.WriteLine(prop.Key + " " + prop.Value);                        
                    //}
                    this.log(device.Name, "Main");
                    
                }
            }
            else
            {
                this.log("No devices", "Test");                
            }
            this.log("TEST2", "");

        }

        private async void test2(DeviceInformation device) {
            var deviceContainerId = "{" + device.Properties["System.Devices.ContainerId"] + "}";

            var service = await GattDeviceService.FromIdAsync(device.Id);
            if (service != null)
            {
                //sServiceInitialized = true;
                //await StartFirmwareUpdate();
            }
            else
            {
                //rootPage.NotifyUser("Access to the device is denied, because the application was not granted access, " +
                //    "or the device is currently in use by another application.",
                //    NotifyType.StatusMessage);
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
                if(tag.Equals(""))
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
    }
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(String[] args)
        {
            Console.WriteLine("TestLine");
            if(args.Length>0)
                Console.WriteLine("TestLine " + args[0]);
   
            var test = new Test();
            var task = test.Test1();

            Console.WriteLine("Press a key to close");
            Console.ReadLine();
        }
    }
}
