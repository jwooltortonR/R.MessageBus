﻿using System;
using PublishSubscribe.Messages;
using R.MessageBus.Interfaces;

namespace PublishSubscribe.Consumer2
{
    public class PublishSubscribeMessageHandler : IMessageHandler<PublishSubscribeMessage>
    {
        public void Execute(PublishSubscribeMessage message)
        {
            Console.WriteLine("Consumer 2 Received Message - {0}", message.CorrelationId);
        }

        public IConsumeContext Context { get; set; }
    }
}