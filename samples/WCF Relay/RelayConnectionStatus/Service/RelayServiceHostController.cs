//  
//  Copyright © Microsoft Corporation, All Rights Reserved
// 
//  Licensed under the Apache License, Version 2.0 (the "License"); 
//  you may not use this file except in compliance with the License. 
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0 
// 
//  THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//  OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//  ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//  PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//  See the Apache License, Version 2.0 for the specific language
//  governing permissions and limitations under the License. 

namespace RelaySamples
{
    using System;
    using System.ServiceModel;
    using System.Threading;
    using Microsoft.Practices.TransientFaultHandling;
    using Microsoft.ServiceBus;

    public class RelayServiceHostController : IDisposable
    {
        readonly Func<ServiceHost> createServiceHost;
        readonly RetryStrategy retryStrategy;
        int retryCount;
        ServiceHost serviceHost;
        ConnectionStatusBehavior statusBehavior;

        public RelayServiceHostController(Func<ServiceHost> createServiceHost)
        {
            this.createServiceHost = createServiceHost;
            // Use exponential backoff in case of failures. Retry forever
            retryStrategy = new ExponentialBackoff(100000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(1));
        }

        public void Dispose()
        {
            Close();
        }

        public void Start()
        {
            Open();
        }

        public void Stop()
        {
            Close();
        }

        public void Open()
        {
            this.serviceHost = createServiceHost();

            statusBehavior = new ConnectionStatusBehavior();
            statusBehavior.Online += ConnectionStatusOnline;
            statusBehavior.Offline += ConnectionStatusOffline;
            statusBehavior.Connecting += ConnectionStatusConnecting;

            // add the Service Bus credentials to all endpoints specified in configuration
            foreach (var endpoint in serviceHost.Description.Endpoints)
            {
                endpoint.Behaviors.Add(statusBehavior);
            }

            // Register for Service Host fault notification
            serviceHost.Faulted += ServiceHostFaulted;

            // Start the service
            // if Open throws any Exceptions the faulted event will be raised
            try
            {
                serviceHost.Open();
            }
            catch (Exception e)
            {
                // This is handled by the fault handler ServiceHostFaulted
                Console.WriteLine("Encountered exception \"{0}\"", e.Message);
            }
        }

        public void Close()
        {
            if (serviceHost != null)
            {
                serviceHost.Faulted -= ServiceHostFaulted;
                try
                {
                    serviceHost.Close();
                }
                catch
                {
                    serviceHost.Abort();
                }

                serviceHost = null;
            }
        }

        void ServiceHostFaulted(object sender, EventArgs e)
        {
            Console.WriteLine("Relay Echo ServiceHost faulted. {0}", statusBehavior.LastError?.Message);
            serviceHost.Abort();
            serviceHost.Faulted -= ServiceHostFaulted;
            serviceHost = null;

            var isPermanentError = statusBehavior.LastError is RelayNotFoundException
                                   || statusBehavior.LastError is AuthorizationFailedException
                                   || statusBehavior.LastError is AddressAlreadyInUseException;

            // RelayNotFound indicates the Relay Service could not find an endpoint with the name/address used by this client
            // AuthorizationFailed indicates that the SAS/Shared Secret key used by this client is invalid and needs to be updated
            // AddressAlreadyInUseException indicates that this endpoint is in use with incompatible settings (like volatile vs persistent etc)
            // If we encounter one of these conditions, retrying will not change the results. Operator/Admin intervention is required.

            // Further we believe that in all other cases, the ServiceBus Client itself would retry instead of faulting the ServiceHost
            // However, in case there are bugs that we are not currently aware of, restarting the ServiceHost here may overcome such conditions.

            if (!isPermanentError)
            {
                TimeSpan waitPeriod;
                var shouldRetry = retryStrategy.GetShouldRetry();
                if (shouldRetry(retryCount++, statusBehavior.LastError, out waitPeriod))
                {
                    Thread.Sleep(waitPeriod);

                    Open();
                    Console.WriteLine("Relay Echo ServiceHost recreated ");
                }
            }
            else if (statusBehavior.LastError != null)
            {
                Console.WriteLine("Relay Echo Service encountered permanent error {0}", statusBehavior.LastError?.Message);
            }
        }

        void ConnectionStatusOffline(object sender, EventArgs args)
        {
            Console.WriteLine("Going Offline Event. Last error encountered {0}", statusBehavior.LastError?.Message);
        }

        void ConnectionStatusOnline(object sender, EventArgs args)
        {
            // Reset retry count
            retryCount = 0;

            Console.WriteLine("Going Online Event");
        }

        void ConnectionStatusConnecting(object sender, EventArgs args)
        {
            Console.WriteLine("Lost Connection. Reconnecting Event. Last error encountered {0}",
                statusBehavior.LastError?.Message);
        }
    }
}