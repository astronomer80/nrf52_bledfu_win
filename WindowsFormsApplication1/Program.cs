using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System;
using System.Threading.Tasks;
using System.IO;

namespace WindowsFormsApplication1
{
    
    class Test {
        StreamWriter logFile;
        private void init() {
            String time = DateTime.Now.ToString("yyyyMMdd-HH:mm.ss");
            String filename = "Log[" + time + "].txt";
            try
            {
                logFile = new System.IO.StreamWriter(@"F:\Primo\logs\" + filename, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        
        public async Task Test1()
        {
            this.log("Test", "");
            try
            {
                await test();
            }
            catch (Exception e)
            {
                this.log(e.Message, "Test");
            }

        }


        private async    Task test()
        {
            Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };
            
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            
            if (devices.Count>0) 
            {
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name);
                    this.log(device.Name, "Main");
                    
                }
            }
            else
            {
                this.log("No devices", "Test");
                Console.WriteLine("No devices");

                //rootPage.NotifyUser("Could not find any Heart Rate devices. Please make sure your device is paired and powered on!",   NotifyType.StatusMessage);
            }

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
            try
            {
                this.logFile.WriteLine("[" + tag + "]" + data);
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
         
            
            //test.



        }

        
    }
}
