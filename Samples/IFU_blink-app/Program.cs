using nanoFramework.Runtime.InFieldUpdate;
using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace IFU_blink_app
{
    public class Program
    {
        private static GpioController s_GpioController;

        public static void Main()
        {
            Debug.WriteLine("Hello from nanoFramework!");

            // confirm the deployment
            UpdateManager.ConfirmDeploymentImage();

            s_GpioController = new GpioController();

            /////////////////////////////////////////////////
            // Add new configuration for other boards here //
            /////////////////////////////////////////////////
            
            // ORGPALTHREE: PG6 is LED1
            GpioPin led = s_GpioController.OpenPin(PinNumber('G', 6), PinMode.Output);

            /////////////////////////////////////////////////
            /////////////////////////////////////////////////

            led.Write(PinValue.Low);

            // milliseconds
            var toggleRate = 125;

            ///////////////////////////////////////////////////////
            // Adjust the blink count here when changing version //
            ///////////////////////////////////////////////////////
            var blinkCount = 2;
            ///////////////////////////////////////////////////////
            ///////////////////////////////////////////////////////
            
            while (true)
            {
                // blink the LED N times
                for (int i = 0; i < blinkCount * 2; i++)
                {
                    led.Toggle();
                    Thread.Sleep(toggleRate);
                }

                // wait for 1 second before the next blink sequence
                led.Write(PinValue.Low);
                Thread.Sleep(1000);
            }
        }


        static int PinNumber(char port, byte pin)
        {
            if (port < 'A' || port > 'J')
                throw new ArgumentException();

            return ((port - 'A') * 16) + pin;
        }
    }
}
