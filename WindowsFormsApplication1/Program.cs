using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System;
using System.Threading.Tasks;

namespace WindowsFormsApplication1
{
    class Test {
        public async Task Test1()
        {
            Test.log("Test", "Test");
            try
            {
                await this.test();
            }
            catch (Exception e)
            {
                Test.log(e.Message, "Test");
            }

        }


        private async Task test()
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
                    Test.log(device.Name, "Main");
                    
                }
            }
            else
            {
                Test.log("No devices", "Test");
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
        public async static void log(string data, string appendname)
        {
            String time = DateTime.Now.ToString("yyyyMMdd-HH:mm.ss");
            data = "[" + time + "]" + data + "\n";

            try
            {
                using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"F:\Primo\logs\log_file_" + appendname + ".txt", true))
                {
                    file.WriteLine(data);
                }
            }
            catch (Exception ex)
            {
                //rootPage.NotifyUser("LOG:" + ex.Message, NotifyType.ErrorMessage);
                log(data, "_");
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
