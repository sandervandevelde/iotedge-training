namespace WiseOpcUaClientModule
{
  using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Opc.UaFx;
    using Opc.UaFx.Client;

    class Program
    {
        //static int counter;

        private const string DefaultAddress = "opc.tcp:localhost:4849";

        private const string DefaultNodePotentio1 = "ns=2;s=Machine/Line";
        private const string DefaultNodePotentio2 = "ns=2;s=Machine/Line";
        private const string DefaultNodeSwitch1 = "ns=2;s=Machine/Line";
        private const string DefaultNodeSwitch2 = "ns=2;s=Machine/Line";
        private const string DefaultNodeLed1 = "ns=2;s=Machine/Line";
        private const string DefaultNodeLed2 = "ns=2;s=Machine/Line";

        private const string DefaultLicenseKey = "";

        private static ModuleClient ioTHubModuleClient;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
       
            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, ioTHubModuleClient);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // TODO DirectMethod for command

            var thread = new Thread(() => ThreadBody());
            thread.Start();
        }

        
        private static void ThreadBody()
        {
            try
            {
                if (LicenseKey != string.Empty)
                {
                    Opc.UaFx.Licenser.LicenseKey = LicenseKey;
                }

                var address = Address;

                var client = new OpcClient(address);

                client.Connect();

                OpcSubscribeDataChange[] commands = new OpcSubscribeDataChange[] {
                    new OpcSubscribeDataChange(NodePotentio1, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodePotentio2, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeSwitch1, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeSwitch2, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeLed1, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeLed2, OpcDataChangeTrigger.StatusValueTimestamp, HandleDataChangedMachineLineNode),
                };

                OpcSubscription subscription = client.SubscribeNodes(commands);
                Console.WriteLine($"Client started... (listing to {address})");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }

        private static void HandleDataChangedMachineLineNode(object sender, OpcDataChangeReceivedEventArgs e)
        {
            var value = (e.Item.Value.Value as string[]);

            if (value.Length == 0)
            {
                Console.WriteLine("Ignore empty array");
                return;
            }

            Console.WriteLine($"Line ----> \n\t ServerTimeStamp: {e.Item.Value.ServerTimestamp}\n\t SourceTimestamp: {e.Item.Value.SourceTimestamp} \n\t Value: {value}");

            // TODO SEND MESSAGE to CLOUD

            // var s = StrategyFactory.GetStrategy(value[0], value[1]);

            // var jsonMessage = s.ParseArray(value);

            // Console.WriteLine($"Line ----> \n\t ServerTimeStamp: {e.Item.Value.ServerTimestamp}\n\t SourceTimestamp: {e.Item.Value.SourceTimestamp} \n\t Value: {jsonMessage}");

            // using (var message = new Message(Encoding.UTF8.GetBytes(jsonMessage)))
            // {
            //     message.ContentEncoding = "utf-8";
            //     message.ContentType = "application/json";

            //     message.Properties.Add("ContentEncodingX", "PhilipsOpcUa+utf-8+applicaiton/json");

            //     ioTHubModuleClient.SendEventAsync("output1", message).Wait();
            
            //     Console.WriteLine("Json message sent");
            // }
            
        }

        private static string Address { get; set; } = DefaultAddress;

        private static string NodePotentio1 { get; set; } = DefaultNodePotentio1;
        private static string NodePotentio2 { get; set; } = DefaultNodePotentio2;
        private static string NodeSwitch1 { get; set; } = DefaultNodeSwitch1;
        private static string NodeSwitch2 { get; set; } = DefaultNodeSwitch2;
        private static string NodeLed1 { get; set; } = DefaultNodeLed1;
        private static string NodeLed2 { get; set; } = DefaultNodeLed2;

        private static string LicenseKey { get; set; } = DefaultLicenseKey;

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException($"UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("address")) 
                {
                    if (desiredProperties["address"] != null)
                    {
                        Address = desiredProperties["address"];
                    }
                    else
                    {
                        Address = DefaultAddress;
                    }

                    Console.WriteLine($"Address changed to {Address}");

                    reportedProperties["address"] = Address;
                }

                if (desiredProperties.Contains("nodePotentio1")) 
                {
                    if (desiredProperties["nodePotentio1"] != null)
                    {
                        NodePotentio1 = desiredProperties["nodePotentio1"];
                    }
                    else
                    {
                        NodePotentio1 = DefaultNodePotentio1;
                    }

                    Console.WriteLine($"NodePotentio1 changed to {NodePotentio1}");

                    reportedProperties["nodePotentio1"] = NodePotentio1;
                }

                if (desiredProperties.Contains("nodePotentio2")) 
                {
                    if (desiredProperties["nodePotentio2"] != null)
                    {
                        NodePotentio2 = desiredProperties["nodePotentio2"];
                    }
                    else
                    {
                        NodePotentio2 = DefaultNodePotentio2;
                    }

                    Console.WriteLine($"NodePotentio2 changed to {NodePotentio2}");

                    reportedProperties["nodePotentio2"] = NodePotentio2;
                }

                if (desiredProperties.Contains("nodeSwitch1")) 
                {
                    if (desiredProperties["nodeSwitch1"] != null)
                    {
                        NodeSwitch1 = desiredProperties["nodeSwitch1"];
                    }
                    else
                    {
                        NodeSwitch1 = DefaultNodeSwitch1;
                    }

                    Console.WriteLine($"NodeSwitch1 changed to {NodeSwitch1}");

                    reportedProperties["nodeSwitch1"] = NodeSwitch1;
                }

                if (desiredProperties.Contains("nodeSwitch2")) 
                {
                    if (desiredProperties["nodeSwitch2"] != null)
                    {
                        NodeSwitch2 = desiredProperties["nodeSwitch2"];
                    }
                    else
                    {
                        NodeSwitch2 = DefaultNodeSwitch2;
                    }

                    Console.WriteLine($"NodeSwitch2 changed to {NodeSwitch2}");

                    reportedProperties["nodeSwitch2"] = NodeSwitch2;
                }

                if (desiredProperties.Contains("nodeLed1")) 
                {
                    if (desiredProperties["nodeLed1"] != null)
                    {
                        NodeLed1 = desiredProperties["nodeLed1"];
                    }
                    else
                    {
                        NodeLed1 = DefaultNodeLed1;
                    }

                    Console.WriteLine($"NodeLed1 changed to {NodeLed1}");

                    reportedProperties["nodeLed1"] = NodeLed1;
                }

                if (desiredProperties.Contains("nodeLed2")) 
                {
                    if (desiredProperties["nodeLed2"] != null)
                    {
                        NodeLed2 = desiredProperties["nodeLed2"];
                    }
                    else
                    {
                        NodeLed2 = DefaultNodeLed2;
                    }

                    Console.WriteLine($"NodeLed2 changed to {NodeLed2}");

                    reportedProperties["nodeLed2"] = NodeLed2;
                }

                if (desiredProperties.Contains("licenseKey")) 
                {
                    if (desiredProperties["licenseKey"] != null)
                    {
                        LicenseKey = desiredProperties["licenseKey"];
                    }
                    else
                    {
                        LicenseKey = DefaultLicenseKey;
                    }

                    Console.WriteLine($"LicenseKey changed to {LicenseKey}");

                    reportedProperties["licenseKey"] = LicenseKey;
                }

                if (reportedProperties.Count > 0)
                {
                    client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
