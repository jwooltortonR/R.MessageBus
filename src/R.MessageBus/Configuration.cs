﻿using System;
using System.Collections.Generic;
using System.Reflection;
using R.MessageBus.Client.RabbitMQ;
using R.MessageBus.Container;
using R.MessageBus.Core;
using R.MessageBus.Interfaces;
using R.MessageBus.Persistance.MongoDb;
using R.MessageBus.Settings;
using Queue = R.MessageBus.Interfaces.Queue;

namespace R.MessageBus
{
    /// <summary>
    /// Bus configuration.
    /// 
    /// Implicit initialization <see cref="Configuration"/>:
    /// Initialize from default values.
    /// 
    /// Explicit initialization <see cref="LoadSettings"/>:
    /// Initialize from BusSettings section of a custom configuration file,
    /// throw exception if the section is not found
    /// </summary>
    public class Configuration : IConfiguration
    {
        private const string DefaultDatabaseName = "RMessageBusPersistantStore";
        private const string DefaultConnectionString = "mongodb://localhost/";
        private const string DefaultHost= "localhost";

        #region Private Fields

        private string _configurationPath;
        private string _endPoint;
        private string _queueName;

        #endregion

        #region Public Properties

        public Type ConsumerType { get; set; }
        public Type ProducerType { get; set; }
        public Type Container { get; set; }
        public Type ProcessManagerFinder { get; set; }
        public Type SerializerType { get; set; }
        public bool ScanForMesssageHandlers { get; set; }
        public string PersistenceStoreConnectionString { get; set; }
        public string PersistenceStoreDatabaseName { get; set; }
        public ITransportSettings TransportSettings { get; set; }
        public IDictionary<string, string> QueueMappings { get; set; } 

        #endregion

        public Configuration()
        {
            var defaultQueueName = Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly().GetName().Name : System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            TransportSettings = new TransportSettings { Queue = new Queue
            {
                Name = defaultQueueName
            }};

            _configurationPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            SetTransportSettings();
            SetPersistanceSettings();

            QueueMappings = new Dictionary<string, string>();

            ConsumerType = typeof(Consumer);
            ProducerType = typeof(Producer);
            Container = typeof(StructuremapContainer);
            ProcessManagerFinder = typeof(MongoDbProcessManagerFinder);
            SerializerType = typeof (JsonMessageSerializer);
        }

        /// <summary>
        /// Adds a message queue mapping. 
        /// </summary>
        /// <param name="messageType">Type of message</param>
        /// <param name="queue">Queue to send the message to</param>
        public void AddQueueMapping(Type messageType, string queue)
        {
            QueueMappings.Add(messageType.FullName, queue);
        }

        /// <summary>
        /// Load settings from configFilePath. 
        /// Use default App.config when configFilePath is not specified. 
        /// </summary>
        /// <param name="configFilePath">configuration file path</param>
        /// <param name="endPoint">RabbitMq settings endpoint name</param>
        public void LoadSettings(string configFilePath = null, string endPoint = null)
        {
            if (null != configFilePath)
            {
                _configurationPath = configFilePath;
            }

            _endPoint = endPoint;

            var configurationManager = new ConfigurationManagerWrapper(_configurationPath);

            var section = configurationManager.GetSection<BusSettings.BusSettings>("BusSettings");

            if (section == null) throw new ArgumentException("BusSettings section not found in the configuration file.");

            SetTransportSettings(section);
            SetPersistanceSettings(section);
        }
        
        /// <summary>
        /// Sets the container.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetContainer<T>() where T : class, IBusContainer
        {
            Container = typeof(T);
        }

        /// <summary>
        /// Sets the serializer type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetSerializer<T>() where T : class, IMessageSerializer
        {
            SerializerType = typeof(T);
        }

        /// <summary>
        /// Sets the process manager finder
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetProcessManagerFinder<T>() where T : class, IProcessManagerFinder
        {
            ProcessManagerFinder = typeof (T);
        }

        /// <summary>
        /// Sets consumer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetConsumer<T>() where T : class, IConsumer 
        {
            ConsumerType = typeof(T);
        }

        /// <summary>
        /// Sets publisher
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetProducer<T>() where T : class, IProducer
        {
            ProducerType = typeof(T);
        }

        /// <summary>
        /// Sets QueueName
        /// </summary>
        public void SetQueueName(string queueName)
        {
            _queueName = queueName;
            TransportSettings.Queue.Name = queueName;
        }

        /// <summary>
        /// Gets QueueName
        /// </summary>
        public string GetQueueName()
        {
            return TransportSettings.Queue.Name;
        }

        /// <summary>
        /// Gets instance of IConsumer type
        /// </summary>
        /// <returns></returns>
        public IConsumer GetConsumer()
        {
            return (IConsumer)Activator.CreateInstance(ConsumerType, TransportSettings);
        }

        /// <summary>
        /// Gets instance of IProducer type
        /// </summary>
        /// <returns></returns>
        public IProducer GetProducer()
        {
            return (IProducer)Activator.CreateInstance(ProducerType, TransportSettings, QueueMappings, GetSerializer());
        }

        /// <summary>
        /// Gets instance of IBusContainer type
        /// </summary>
        /// <returns></returns>
        public IBusContainer GetContainer()
        {
            return (IBusContainer)Activator.CreateInstance(Container);
        }

        /// <summary>
        /// Gets instance of IProcessManagerFinder type
        /// </summary>
        /// <returns></returns>
        public IProcessManagerFinder GetProcessManagerFinder()
        {
            return (IProcessManagerFinder)Activator.CreateInstance(ProcessManagerFinder, PersistenceStoreConnectionString, PersistenceStoreDatabaseName);
        }

        public IRequestConfiguration GetRequestConfiguration(ConsumerEventHandler consumeMessageEvent, Guid correlationId)
        {
            var configuration = new RequestConfiguration(this, consumeMessageEvent, correlationId);
            return configuration;
        }

        public IMessageSerializer GetSerializer()
        {
            return (IMessageSerializer)Activator.CreateInstance(SerializerType);
        }

        #region Private Methods

        private void SetTransportSettings(BusSettings.BusSettings section = null)
        {
            if (null != section)
            {
                var endPointSettings = !string.IsNullOrEmpty(_endPoint) ? section.EndpointSettings.GetItemByKey(_endPoint) : section.EndpointSettings.GetItemAt(0);
                var transportSettings = endPointSettings.TransportSettings;

                if (null != transportSettings)
                {
                    TransportSettings = GetTransportSettingsFromBusSettings(transportSettings);

                    return;
                }
            }

            // Set defaults
            TransportSettings = GetTransportSettingsFromDefaults();
        }

        private void SetPersistanceSettings(BusSettings.BusSettings section = null)
        {
            if (null != section)
            {
                var endPointSettings = !string.IsNullOrEmpty(_endPoint) ? section.EndpointSettings.GetItemByKey(_endPoint) : section.EndpointSettings.GetItemAt(0);
                var persistanceSettings = endPointSettings.PersistanceSettings;

                if (null != persistanceSettings)
                {
                    PersistenceStoreDatabaseName = !string.IsNullOrEmpty(persistanceSettings.Database) ? persistanceSettings.Database : DefaultDatabaseName;
                    PersistenceStoreConnectionString = PersistenceStoreConnectionString ?? persistanceSettings.ConnectionString;

                    return;
                }
            }

            // Set defaults
            PersistenceStoreDatabaseName = PersistenceStoreDatabaseName ?? DefaultDatabaseName;
            PersistenceStoreConnectionString = PersistenceStoreConnectionString ?? DefaultConnectionString;
        }

        private ITransportSettings GetTransportSettingsFromBusSettings(BusConfiguration.TransportSettings settings)
        {
            /*
             * QUEUE NAME:
             * If set via fluent API, use it
             * else, if set in the config files, use it
             * else, use default queue name
            */
            string queueName;

            if (!string.IsNullOrEmpty(_queueName))
            {
                queueName = _queueName;
            }
            else if (!string.IsNullOrEmpty(settings.Queue.Name))
            {
                queueName = settings.Queue.Name;
            }
            else
            {
                queueName = TransportSettings.Queue.Name;
            }

            ITransportSettings transportSettings = new TransportSettings();
            transportSettings.Host = settings.Host;
            transportSettings.MaxRetries = settings.Retries.MaxRetries;
            transportSettings.RetryDelay = settings.Retries.RetryDelay;
            transportSettings.Username = settings.Username;
            transportSettings.Password = settings.Password;
            transportSettings.NoAck = settings.NoAck;
            transportSettings.Queue = new Queue
            {
                Name = queueName,
                RoutingKey = settings.Queue.RoutingKey,
                Arguments = GetQueueArguments(settings),
                AutoDelete = settings.Queue.AutoDelete,
                Durable = settings.Queue.Durable,
                Exclusive = settings.Queue.Exclusive,
                IsReadOnly = settings.Queue.IsReadOnly()
            };

            return transportSettings;
        }

        private ITransportSettings GetTransportSettingsFromDefaults()
        {
            ITransportSettings transportSettings = new TransportSettings();
            transportSettings.Host = DefaultHost;
            transportSettings.MaxRetries = 3;
            transportSettings.RetryDelay = 3000;
            transportSettings.Username = null;
            transportSettings.Password = null;
            transportSettings.NoAck = false;
            transportSettings.Queue = new Queue
            {
                Name = TransportSettings.Queue.Name,
                RoutingKey = null,
                Arguments = null,
                AutoDelete = false,
                Durable = true,
                Exclusive = false,
                IsReadOnly = false
            };

            return transportSettings;
        }

        private static Dictionary<string, object> GetQueueArguments(BusConfiguration.TransportSettings settings)
        {
            var queueArguments = new Dictionary<string, object>();
            for (var i = 0; i < settings.Queue.Arguments.Count; i++)
            {
                queueArguments.Add(settings.Queue.Arguments[i].Name, settings.Queue.Arguments[i].Value);
            }
            return queueArguments;
        }

        #endregion
    }
}