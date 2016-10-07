using Windows.Devices.Bluetooth.GenericAttributeProfile;
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
        private void init() {
            String time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            String filename = "[" + time + "].txt";
            Console.WriteLine(filename);
            try
            {
                logFile = new StreamWriter("F:\\Primo\\logs\\" + filename, true);
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
            Guid UUID = new Guid("00001530-1212-efde-1523-785feabcd123"); //NRF52 DFU Service
            String service = GattDeviceService.GetDeviceSelectorFromUuid(UUID);
            String[] param = new string[] { "System.Devices.ContainerId" };
            this.log("TEST1", "");
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(service, param);
            //Thread.Sleep(2000);
            if (devices.Count>0) 
            {
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name + " " + device.Properties);
                    this.log(device.Name, "Main");                    
                }
            }
            else
            {
                Console.WriteLine("No devices");
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

            Console.ReadLine();
        }
    }
}
