using ConnectTheDotsIoT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.System.Diagnostics;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TemperatureToEventHub
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //这是RGB灯的三个管脚，请将R的线插在5号管脚，G插6号管脚，B插在13号管脚
        private const int LED_PIN_R = 5;
        private const int LED_PIN_G = 6;
        private const int LED_PIN_B = 13;
        private GpioPin pin_r;
        private GpioPin pin_g;
        private GpioPin pin_b;

        //private GpioPin pin;

        // Timer
        private DispatcherTimer ReadSensorTimer;
        // SHT15 Sensor
        private SHT15 sht15 = null;

        // Sensor values
        public static double TemperatureC = 0.0;
        public static double TemperatureF = 0.0;
        public static double Humidity = 0.0;
        public static double CalculatedDewPoint = 0.0;
        public static Boolean isRegistered = false;


        int counter = 0; // dummy temp counter value;

        int uploadHelper = 0;
        int uploadspac = 3;


        public static double WarningTemperature = 30.0;
        public int WarningHelper = 60;

        ConnectTheDotsHelper ctdHelper;

        /// <summary>
        /// Main page constructor
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            InitGPIO();

            // Hard coding guid for sensors. Not an issue for this particular application which is meant for testing and demos
            List<ConnectTheDotsSensor> sensors = new List<ConnectTheDotsSensor>
            {
                new ConnectTheDotsSensor(),
            };
            //！！！请修改这里，填上相应的值，前四个为必要的修改的值，否则无法连上Event Hub
            ctdHelper = new ConnectTheDotsHelper(
               serviceBusNamespace: "这个是Service Bus的命名空间名称",
               eventHubName: "Event Hub的名字",
               keyName: "在Event Hub配置的规则名称（如Send）",
               key: "在Event Hub配置的相应规则的密钥",
               displayName: "SmartLift",
               organization: "Microsoft",
               location: "Beijing",
               sensorList: sensors);

            // Start Timer every 1 seconds
            ReadSensorTimer = new DispatcherTimer();
            ReadSensorTimer.Interval = TimeSpan.FromMilliseconds(1000);
            ReadSensorTimer.Tick += Timer_Tick;
            ReadSensorTimer.Start();

            Unloaded += MainPage_Unloaded;

            //InitializeSensor(DATA_PIN, SCK_PIN);

            // Initialize and Start HTTP Server
            HttpServer WebServer = new HttpServer();

            WebServer.RecivedMeg += (meg, eve) =>
            {
                this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    tbmeg.Text = meg.ToString();
                }).AsTask();

            };

            var asyncAction = ThreadPool.RunAsync((w) => { WebServer.StartServer(); });
        }


        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pin_r = null; pin_b = null; pin_g = null;
                tbmeg.Text = "There is no GPIO controller on this device.";
                return;
            }

            pin_r = gpio.OpenPin(LED_PIN_R);
            pin_b = gpio.OpenPin(LED_PIN_B);
            pin_g = gpio.OpenPin(LED_PIN_G);

            // Show an error if the pin wasn't initialized properly
            if (pin_r == null)
            {
                tbmeg.Text = "There were problems initializing the GPIO pin.";
                return;
            }

            pin_r.Write(GpioPinValue.High);
            pin_b.Write(GpioPinValue.High);
            pin_g.Write(GpioPinValue.High);
            pin_r.SetDriveMode(GpioPinDriveMode.Output);
            pin_b.SetDriveMode(GpioPinDriveMode.Output);
            pin_g.SetDriveMode(GpioPinDriveMode.Output);

            tbmeg.Text = "GPIO管脚检测完毕，正常初始化。";
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            // Cleanup Sensor
            sht15.Dispose();
        }

        // Timer Ro
        private async void Timer_Tick(object sender, object e)
        {
            // Read Raw Temperature and Humidity
            //int RawTemperature = sht15.ReadRawTemperature();

            //TemperatureC = sht15.CalculateTemperatureC(RawTemperature);
            //TemperatureF = sht15.CalculateTemperatureF(RawTemperature);
            //Humidity = sht15.ReadHumidity(TemperatureC);
            //CalculatedDewPoint = sht15.DewPoint(TemperatureC, Humidity);

            StringBuilder _sb = new StringBuilder();
            //_sb.AppendLine("Time: " + DateTime.Now.ToString("h:mm:ss tt"));
            LightStatus result = await GetaString();
            _sb.AppendLine("RGB frome web: " + result.pin_r + " " + result.pin_g + " " + result.pin_b);
            pin_r.Write((GpioPinValue)(result.pin_r));
            pin_g.Write((GpioPinValue)(result.pin_g));
            pin_b.Write((GpioPinValue)(result.pin_b));

            TB.Text = "";


            //var CpuUsage =  Windows.System.Diagnostics.ProcessCpuUsageReport.GetReport();

            //Windows.System.Diagnostics.

            Windows.System.Diagnostics.ProcessDiagnosticInfo info = Windows.System.Diagnostics.ProcessDiagnosticInfo.GetForCurrentProcess();
            var cpuReport = info.CpuUsage.GetReport();
            var cpuUsage = cpuReport.UserTime.Milliseconds*100/(cpuReport.UserTime.Milliseconds+ cpuReport.KernelTime.Milliseconds)+"%";
            var diskReport = info.DiskUsage.GetReport();
            var diskUsage = diskReport.BytesWrittenCount/(diskReport.BytesWrittenCount+ diskReport.WriteOperationCount+diskReport.OtherOperationCount) + "%";
            var memoryReport = info.MemoryUsage.GetReport();
            var memoryUsage = memoryReport.PagedPoolSizeInBytes/(memoryReport.NonPagedPoolSizeInBytes+ memoryReport.PagedPoolSizeInBytes) + "%";

            // Gets the app's current memory usage   
            ulong AppMemoryUsageUlong = MemoryManager.AppMemoryUsage;

            // Gets the app's memory usage limit   
            ulong AppMemoryUsageLimitUlong = MemoryManager.AppMemoryUsageLimit;

            AppMemoryUsageUlong /= 1024 * 1024;
            AppMemoryUsageLimitUlong /= 1024 * 1024;
            string AppMemoryUsageText = "App memory uage - " + AppMemoryUsageUlong.ToString();
            string AppMemoryUsageLimitText = "App memory usage limit - " + AppMemoryUsageLimitUlong.ToString();

            // Gets the app's memory usage level whether low or medium or high   
            string AppMemoryUsageLevelText = "App memory usage level - " + MemoryManager.AppMemoryUsageLevel.ToString();

            memoryUsage = AppMemoryUsageUlong.ToString() + "%";
            Debug.WriteLine("cpuUsage:"+ cpuUsage+ ", diskUsage:"+ diskUsage+ ",memoryUsage:"+ memoryUsage);
            Debug.WriteLine("Memory usage:"+ AppMemoryUsageText);

            tbmeg.Text = "CPU 使用率："+ cpuUsage +"\r\n磁盘使用率：" + diskUsage+"\r\n内存使用率："+ memoryUsage;


            uploadHelper++;
            if (uploadHelper >= uploadspac)
            {
                ConnectTheDotsSensor sensor = ctdHelper.sensors[0];
                sensor.StatusLogId = Guid.NewGuid().ToString();
                sensor.DeviceId = "1";
                sensor.CPUUsage = cpuUsage;
                sensor.DiskUsage = diskUsage;
                sensor.MemoryUsage = memoryUsage;
                //sensor.location = "Beijing";
                //sensor.temperatureC = TemperatureC.ToString();
                //sensor.temperatureF = TemperatureF.ToString();
                if (isRegistered)//这里只是示例，实际场景请使用读写文件的方式
                    sensor.Status = "Live";
                else
                {
                    sensor.Status = "Register";
                    isRegistered = true;
                }
                //upload Data To EventHub
                ctdHelper.SendSensorData(sensor);
                uploadHelper = 0;
            }
            //Debug.WriteLine("Temperature: " + TemperatureC + " C, " + TemperatureF + " F, " + "Humidity: " + Humidity + ", Dew Point: " + CalculatedDewPoint);
            //Debug.WriteLine(_sb.ToString());
        }



        private void SendData(object sender, RoutedEventArgs e)
        {
        }

        public async Task<LightStatus> GetaString()
        {
            try
            {
                //Create HttpClient
                HttpClient httpClient = new HttpClient();

                //Define Http Headers
                httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json");

                //Call
                string ResponseString = await httpClient.GetStringAsync(
                    new Uri("http://controlmyled.azurewebsites.net/api/getlightstatus"));
                //Replace current URL with your URL
                Debug.WriteLine("ResponseString: " + ResponseString);

                LightStatus ls = JsonConvert.DeserializeObject<LightStatus>(ResponseString);
                return ls;

            }

            catch (Exception ex)
            {
                Debug.WriteLine("request error!" + ex.ToString());
                return new LightStatus();
            }
        }
    }
}
