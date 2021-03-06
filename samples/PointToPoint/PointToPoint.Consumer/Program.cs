﻿using System;
using R.MessageBus;

namespace PointToPoint.Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("*********** Consumer ***********");
            var bus = Bus.Initialize(x =>
            {
                x.ScanForMesssageHandlers = true;
                x.SetQueueName("PointToPoint2");
            });

            bus.StartConsuming();

            Console.ReadLine();
        }
    }
}
