using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace nrfutil_caller_app
{
    class Program
    {
        private static bool finished;

        static void Main(string[] args)
        {
            finished = false;

            createZip();

            // Awful workaround: since Main function cannot be declared async, createZip function cannot be awaited.
            // Here a boolean variable is used to wait createZip function manually.
            while (!finished);
        }

        static async Task createZip()
        {
            AppServiceConnection connection = new AppServiceConnection();
            connection.AppServiceName = "nrfutilWrapper";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            var result = await connection.OpenAsync();
            if (result == AppServiceConnectionStatus.Success)
            {
                ValueSet value = new ValueSet();
                value.Add("", "");
                var response = await connection.SendMessageAsync(value);
                if (response.Status == AppServiceResponseStatus.Success)
                {
                    string path = response.Message["path"].ToString();
                    string file = response.Message["file"].ToString();
                    if (file != null)
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = @"nrfutil", Arguments = "dfu genpkg "+  path + "\\output.zip --application " + file, };
                        Process proc = new Process() { StartInfo = startInfo, };

                        startInfo.UseShellExecute = false;
                        startInfo.RedirectStandardOutput = true;
                        startInfo.RedirectStandardError = true;
                        proc.Start();
                        string output = proc.StandardOutput.ReadToEnd();
                        string error = proc.StandardError.ReadToEnd();
                        proc.Close();
                    }
                }
            }
            finished = true;
        }

    }
}
