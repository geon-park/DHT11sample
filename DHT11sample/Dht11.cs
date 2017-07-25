using System;
using System.Collections;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace TempHumidReader
{
    public class Dht11Reading
    {
        private const int BIT_SIZE = 40;
        public BitArray bits;

        public Dht11Reading()
        {
            bits = new BitArray(BIT_SIZE);
        }
        public bool IsValid()
        {
            long value = BitArrayToLong();
            long checksum = ((value >> 32) & 0xff) + ((value >> 24) & 0xff)
                + ((value >> 16) & 0xff) + ((value >> 8) & 0xff);

            return (checksum & 0xff) == (value & 0xff);
        }

        public double Humidity()
        {

            long value = BitArrayToLong();
            return ((value >> 32) & 0xff) + ((value >> 24) & 0xff) / 10.0;
        }

        public double Temperature()
        {
            long value = BitArrayToLong();
            return ((value >> 16) & 0xff) + ((value >> 8) & 0xff) / 10.0;
        }

        private long BitArrayToLong()
        {
            long value = 0;
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i])
                    value += Convert.ToInt64(Math.Pow(2, i));
            }
            return value;
        }
    }

    public class Dht11Sensor
    {
        private const int SAMPLE_HOLD_LOW_MILLIS = 18;
        private GpioPinDriveMode inputDriveMode;
        private GpioPin pin;

        public int Init(int pinNumber)
        {
            var gpio = GpioController.GetDefault();
            if (null == gpio)
            {
                return 1;
            }
            try
            {
                pin = gpio.OpenPin(pinNumber);
            }
            catch
            {
                return 2;
            }

            inputDriveMode = pin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp) ? 
                GpioPinDriveMode.InputPullUp : GpioPinDriveMode.Input;
            pin.SetDriveMode(inputDriveMode);

            return 0;
        }

        private int InternalSample(Dht11Reading Reading)
        {
            // This is the threshold used to determine whether a bit is a '0' or a '1'.
            // A '0' has a pulse time of 76 microseconds, while a '1' has a
            // pulse time of 120 microseconds. 110 is chosen as a reasonable threshold.
            long oneThreshold = 110L * System.Diagnostics.Stopwatch.Frequency / 1000000L; // 110microsec

            // Latch low value onto pin
            pin.Write(GpioPinValue.Low);

            // Set pin as output
            pin.SetDriveMode(GpioPinDriveMode.Output);

            // Wait for at least 18 ms
            Task.Delay(18).Wait();

            // Set pin back to input
            pin.SetDriveMode(inputDriveMode);

            GpioPinValue previousValue = pin.Read();

            // catch the first rising edge
            long initialRisingEdgeTimeoutMillis = 10000L; // 1ms
            long endTickCount = DateTime.Now.Ticks + initialRisingEdgeTimeoutMillis;
            for (;;)
            {
                if (DateTime.Now.Ticks > endTickCount)
                {
                    return 1;
                }

                GpioPinValue value = pin.Read();
                if (value != previousValue)
                {
                    // rising edge?
                    if (value == GpioPinValue.High)
                    {
                        break;
                    }
                    previousValue = value;
                }
            }

            long prevTime = 0;

            long sampleTimeoutMillis = 100000L; // 10ms
            endTickCount = DateTime.Now.Ticks + sampleTimeoutMillis;

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            // capture every falling edge until all bits are received or
            // timeout occurs
            for (int i = 0; i < Reading.bits.Length + 1;)
            {
                if (DateTime.Now.Ticks > endTickCount)
                    return 2;

                GpioPinValue value = pin.Read();
                if ((previousValue == GpioPinValue.High) && (value == GpioPinValue.Low))
                {
                    long now = sw.ElapsedTicks;

                    if (i != 0)
                    {
                        long difference = now - prevTime;
                        Reading.bits[Reading.bits.Length - i] = difference > oneThreshold;
                    }
                    prevTime = now;
                    ++i;
                }
                previousValue = value;
            }

            if (!Reading.IsValid())
            {
                // checksum mismatch
                return 3;
            }
            return 0;
        }

        public int Sample(out Dht11Reading Reading)
        {
            Reading = new Dht11Reading();
            int result = 0;
            int retryCount = 0;
            do
            {
                result = InternalSample(Reading);
            } while (0 != result && (++retryCount < 20));

            return result;
        }
    }
}
