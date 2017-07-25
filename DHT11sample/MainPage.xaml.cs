using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.System.Threading;
using Windows.Devices.Gpio;
using TempHumidReader;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DHT11sample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Dht11Sensor dht11;
        public static int errorCount = 0;
        private const int PIN_NUM = 4;
        private DispatcherTimer timer;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            dht11 = new Dht11Sensor();
            if (0 != dht11.Init(PIN_NUM))
            {
                statusText.Text = "Failed to open GPIO pin";
                return;
            }
            statusText.Text = "Init Success!!";

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(4000);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void Timer_Tick(object sender, object e)
        {
            Dht11Reading reading;
            int result = dht11.Sample(out reading);

            if (0 != result)
            {
                
                statusText.Text = String.Format("ErrorCount : {0:d}", ++errorCount);
            }
            else
            {
                humidityText.Text = String.Format(("Humidity : {0:f1}"), reading.Humidity());
                temperatureText.Text = String.Format("Temperature : {0:f1}", reading.Temperature());
            }
        }
    }
}
