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

    internal class Program
    {
        //static int counter;

        private const string DefaultAddress = "opc.tcp:localhost:4849";
        private const string DefaultNodePotentio1 = "ns=2;s=Machine/Line";
        private const string DefaultNodePotentio2 = "ns=2;s=Machine/Line";
        private const string DefaultNodeSwitch1 = "ns=2;s=Machine/Line";
        private const string DefaultNodeSwitch2 = "ns=2;s=Machine/Line";
        private const string DefaultNodeRelay1 = "ns=2;s=Machine/Line";
        private const string DefaultNodeRelay2 = "ns=2;s=Machine/Line";

        private const string DefaultLicenseKey = "";

        private static ModuleClient ioTHubModuleClient;

        private static OpcClient opcClient;

        private static string _moduleId; 

        private static string _deviceId;

        private static void Main(string[] args)
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
        private static async Task Init()
        {
            _deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
            _moduleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");

            Console.WriteLine();
            Console.WriteLine("");
            Console.WriteLine("  _       _                  _                            _                                                    ");
            Console.WriteLine(" (_)     | |                | |                          (_)                                                   ");
            Console.WriteLine("  _  ___ | |_ ______ ___  __| | __ _  ___ ________      ___ ___  ___ ______ ___  _ __   ___ ______ _   _  __ _ ");
            Console.WriteLine(" | |/ _ \\| __|______/ _ \\/ _` |/ _` |/ _ \\______\\ \\ /\\ / / / __|/ _ \\______/ _ \\| '_ \\ / __|______| | | |/ _` |");
            Console.WriteLine(" | | (_) | |_      |  __/ (_| | (_| |  __/       \\ V  V /| \\__ \\  __/     | (_) | |_) | (__       | |_| | (_| |");
            Console.WriteLine(" |_|\\___/ \\__|      \\___|\\__,_|\\__, |\\___|        \\_/\\_/ |_|___/\\___|      \\___/| .__/ \\___|       \\__,_|\\__,_|");
            Console.WriteLine("                                __/ |                                           | |                            ");
            Console.WriteLine("                               |___/                                            |_|                            ");
            Console.WriteLine("");

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, ioTHubModuleClient);

            Console.WriteLine("Attached routing output: output1."); 

            await ioTHubModuleClient.SetMethodHandlerAsync(
                "lights",
                lightsMethodCallBack,
                ioTHubModuleClient);

            Console.WriteLine("Attached method handler: lights");   

            await ioTHubModuleClient.OpenAsync();

            Console.WriteLine($"Module '{_deviceId}'-'{_moduleId}' initialized.");

            var thread = new Thread(() => ThreadBody());
            thread.Start();
        }

        public class LightsResponse
        {
            public string state1 { get; set; }
            public string state2 { get; set; }
            public string errorMessage { get; set; }
        }

        static async Task<MethodResponse> lightsMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
           var lightsResponse = new LightsResponse();

            try
            {
                 dynamic request = JsonConvert.DeserializeObject(methodRequest.DataAsJson);

                uint relay1 = (uint) request.relay1;
                uint relay2 = (uint) request.relay2;

                OpcStatus result1 = opcClient.WriteNode("ns=2;s=Wise4012E:Relay01", relay1);  // typemismatch was a bitch
                OpcStatus result2 = opcClient.WriteNode("ns=2;s=Wise4012E:Relay02", relay2);

                lightsResponse.state1 = result1.Description;                   
                lightsResponse.state2 = result2.Description;                   
            }
            catch (Exception ex)
            {
               lightsResponse.errorMessage = ex.Message;   
            }            

            var json = JsonConvert.SerializeObject(lightsResponse);
            var response = new MethodResponse(Encoding.UTF8.GetBytes(json), 200);

            await Task.Delay(TimeSpan.FromSeconds(0));

            return response;
        }

        private static void ThreadBody()
        {
            try
            {
                if (LicenseKey != string.Empty)
                {
                    Opc.UaFx.Licenser.LicenseKey = LicenseKey;
                }
                else
                {
                    Console.WriteLine("No license key available.");

                    Opc.UaFx.Licenser.LicenseKey = string.Empty;
                }

                opcClient = new OpcClient(Address);
                opcClient.Connecting += OpcClient_Connecting;
                opcClient.Connected += OpcClient_Connected;
                opcClient.Connect();

                OpcSubscribeDataChange[] commands = new OpcSubscribeDataChange[] {
                    new OpcSubscribeDataChange(NodePotentio1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodePotentio2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeSwitch1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeSwitch2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeRelay1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                    new OpcSubscribeDataChange(NodeRelay2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode),
                };

                OpcSubscription subscription = opcClient.SubscribeNodes(commands);
                Console.WriteLine($"Client started... (listing to {Address})");

                while(true)
                {
                    // keep thread alive
                    Thread.Sleep(1000);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine("Halted...");      
            }
        }



        private static void OpcClient_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("CONNECTED");
        }

        private static void OpcClient_Connecting(object sender, EventArgs e)
        {
            Console.WriteLine("CONNECTING");
        }

        private static void HandleDataChangedMachineLineNode(object sender, OpcDataChangeReceivedEventArgs e)
        {
            var value = Convert.ToInt32(e.Item.Value.Value);

            Console.WriteLine($"ServerTimeStamp: {e.Item.Value.ServerTimestamp}\n\t {(sender as OpcMonitoredItem).NodeId.Value} Value: {value}");

            // SEND MESSAGE to CLOUD

            var wiseMessage =  new WiseMessage
            {
                deviceId = Address,
                timeStamp = DateTime.Now,
                node = (sender as OpcMonitoredItem).NodeId.Value.ToString(),
                value = value,
            };

            var jsonMessage = JsonConvert.SerializeObject(wiseMessage);

            using (var message = new Message(Encoding.UTF8.GetBytes(jsonMessage)))
            {
                message.ContentEncoding = "utf-8";
                message.ContentType = "application/json";

                message.Properties.Add("ContentEncodingX", "PhilipsOpcUa+utf-8+applicaiton/json");

                ioTHubModuleClient.SendEventAsync("output1", message).Wait();

                Console.WriteLine("Json message sent");
            }
        }

        private static string Address { get; set; } = DefaultAddress;
        private static string NodePotentio1 { get; set; } = DefaultNodePotentio1;
        private static string NodePotentio2 { get; set; } = DefaultNodePotentio2;
        private static string NodeSwitch1 { get; set; } = DefaultNodeSwitch1;
        private static string NodeSwitch2 { get; set; } = DefaultNodeSwitch2;
        private static string NodeRelay1 { get; set; } = DefaultNodeRelay1;
        private static string NodeRelay2 { get; set; } = DefaultNodeRelay2;
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

                if (desiredProperties.Contains("nodeRelay1"))
                {
                    if (desiredProperties["nodeRelay1"] != null)
                    {
                        NodeRelay1 = desiredProperties["nodeRelay1"];
                    }
                    else
                    {
                        NodeRelay1 = DefaultNodeRelay1;
                    }

                    Console.WriteLine($"NodeRelay1 changed to {NodeRelay1}");

                    reportedProperties["nodeRelay1"] = NodeRelay1;
                }

                if (desiredProperties.Contains("nodeRelay2"))
                {
                    if (desiredProperties["nodeRelay2"] != null)
                    {
                        NodeRelay2 = desiredProperties["nodeRelay2"];
                    }
                    else
                    {
                        NodeRelay2 = DefaultNodeRelay2;
                    }

                    Console.WriteLine($"NodeRelay2 changed to {NodeRelay2}");

                    reportedProperties["nodeRelay2"] = NodeRelay2;
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

                    Console.WriteLine("Please restart module to activate changes.");
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

    public class WiseMessage
    {
        public string deviceId { get; set; }

        public string node { get; set; }

        public int value { get; set; }

        public DateTime timeStamp { get; set; }
    }
}