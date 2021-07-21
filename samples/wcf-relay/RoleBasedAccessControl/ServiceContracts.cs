//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Threading;

namespace RoleBasedAccessControl
{
    [ServiceContract(Namespace = "")]
    interface IMathService
    {
        [WebInvoke(RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        [OperationContract]
        int Add(int arg1, int arg2);

        [WebInvoke(RequestFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Wrapped)]
        [OperationContract]
        int AddWithSleep(TimeSpan sleepTime, int arg1, int arg2);
    }

    interface IMathClient : IMathService, IClientChannel
    {
    }

    [ServiceContract]
    interface INotificationService
    {
        [OperationContract(IsOneWay = true)]
        void Notify(string eventId, string eventData);
    }

    class MessageLogger : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            MessageBuffer buffer = request.CreateBufferedCopy(short.MaxValue);

            request = buffer.CreateMessage();
            Console.WriteLine(request);

            request = buffer.CreateMessage();

            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
        }
    }

    class MessageLoggerBehavior : IServiceBehavior
    {
        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            MessageLogger logger = new MessageLogger();

            foreach (ChannelDispatcher channelDispatcher in serviceHostBase.ChannelDispatchers)
            {
                foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                {
                    endpointDispatcher.DispatchRuntime.MessageInspectors.Add(logger);
                }
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    class MathService : IMathService
    {
        public MathService()
        {
        }

        public MathService(string name)
        {
            this.Name = name;
        }

        public string Name { get; private set; }

        public int InvocationCount { get; set; }

        public int Add(int arg1, int arg2)
        {
            this.InvocationCount++;

            return arg1 + arg2;
        }

        public int AddWithSleep(TimeSpan sleepTime, int arg1, int arg2)
        {
            Thread.Sleep(sleepTime);

            return this.Add(arg1, arg2);
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    class NotificationService : INotificationService
    {
        static IDictionary<string, string> notifications = new ConcurrentDictionary<string, string>();

        public static IDictionary<string, string> Notifications
        {
            get { return notifications; }
        }

        public NotificationService()
        {
        }

        public void Notify(string eventId, string eventData)
        {
            NotificationService.Notifications[eventId] = eventData;
        }

        internal static string WaitForNotification(string eventId, TimeSpan timeout)
        {
            var stopTime = DateTime.UtcNow.Add(timeout);
            while (true)
            {
                if (NotificationService.Notifications.TryGetValue(eventId, out string data))
                {
                    return data;
                }
                else if (DateTime.UtcNow >= stopTime)
                {
                    throw new InvalidOperationException($"Notification '{eventId}' was not delivered within {timeout}!");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(200));
            }
        }
    }
}
