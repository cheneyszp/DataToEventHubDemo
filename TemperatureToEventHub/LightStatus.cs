using System;
using System.Collections.Generic;
using System.Linq;

namespace TemperatureToEventHub
{
    public class LightStatus
    {
        public LightStatus()
        {
            pin_r = 1; pin_g = 1; pin_b = 1;
        }

        public LightStatus(int r,int g,int b)
        {
            pin_r = r;pin_g = g;pin_b = b;
        }
        public int LightStatusId { get; set; }

        public int pin_r { get; set; }
        public int pin_g { get; set; }
        public int pin_b { get; set; }

    }
}