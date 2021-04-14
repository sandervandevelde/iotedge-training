namespace WiseOpcUaClientModule
{
    using System;
    using System.Collections.Generic;
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
        private static LogLevelMessage.LogLevel DefaultMinimalLogLevel = LogLevelMessage.LogLevel.Warning;

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

            Console.WriteLine("Supported desired properties: address, nodePotentio1, nodePotentio2, nodeSwitch1, nodeSwitch2, nodeRelay1, nodeRelay2, licenseKey, minimalLogLevel."); 

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

        static async Task<MethodResponse> lightsMethodCallBack(MethodRequest methodRequest, object userContext)        
        {
           var lightsResponse = new LightsResponse();

            try
            {
                System.Console.WriteLine($"Lights method: {methodRequest.DataAsJson}");
                dynamic request = JsonConvert.DeserializeObject(methodRequest.DataAsJson);

                uint relay1 = (uint) request.relay1;
                uint relay2 = (uint) request.relay2;

                OpcStatus result1 = opcClient.WriteNode("ns=2;s=Wise4012E:Relay01", relay1);  // typemismatch was a bitch
                OpcStatus result2 = opcClient.WriteNode("ns=2;s=Wise4012E:Relay02", relay2);

                lightsResponse.state1 = result1.Description;                   
                lightsResponse.state2 = result2.Description;     

                Console.WriteLine($"Response 1: '{result1.Description}'; Response 1: '{result2.Description}'");              
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
                opcClient.BreakDetected += OpcClient_BreakDetected;
                opcClient.Disconnected += OpcClient_Disconnected;
                opcClient.Disconnecting += OpcClient_Disconnecting;
                opcClient.Reconnected += OpcClient_Reconnected;
                opcClient.Reconnecting += OpcClient_Reconnecting;

                opcClient.Connect();

                var commands = new List<OpcSubscribeDataChange>();

                if (!string.IsNullOrEmpty(NodePotentio1))
                {
                    commands.Add(new OpcSubscribeDataChange(NodePotentio1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodePotentio1");
                }

                if (!string.IsNullOrEmpty(NodePotentio2))
                {
                    commands.Add(new OpcSubscribeDataChange(NodePotentio2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodePotentio2");
                }

                if (!string.IsNullOrEmpty(NodeSwitch1))
                {
                    commands.Add(new OpcSubscribeDataChange(NodeSwitch1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodeSwitch1");
                }

                if (!string.IsNullOrEmpty(NodeSwitch2))
                {
                    commands.Add(new OpcSubscribeDataChange(NodeSwitch2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodeSwitch2");
                }

                if (!string.IsNullOrEmpty(NodeRelay1))
                {
                    commands.Add(new OpcSubscribeDataChange(NodeRelay1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodeRelay1");
                }

                if (!string.IsNullOrEmpty(NodeRelay2))
                {
                    commands.Add(new OpcSubscribeDataChange(NodeRelay2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                }
                else
                {
                    System.Console.WriteLine("Ignored empty NodeRelay2");
                }

                OpcSubscription subscription = opcClient.SubscribeNodes(commands);

                Console.WriteLine($"Client started... (listening to '{NodePotentio1},{NodePotentio2},{NodeSwitch1},{NodeSwitch2},{NodeRelay1},{NodeRelay2}' at '{Address}')");

                while(true)
                {
                    // keep thread alive
                    Thread.Sleep(1000);
                }
            }
            catch (System.Exception ex)
            {
                // TODO: Test for Timeout towards OPC-UA Server connection
 
                Console.WriteLine($"Fatal ThreadBody exception: {ex.Message}");

                var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Critical, code = "00", message = $"ThreadBody exception: {ex.Message}" };

                SendLogLevelMessage(logLevelMessage).Wait();

                Console.WriteLine("Halted...");    
            }
        }

        private static void OpcClient_Connected(object sender, EventArgs e)
        {
            Console.WriteLine("CONNECTED");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Debug, code = "07", message = "CONNECTED" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_Connecting(object sender, EventArgs e)
        {
            Console.WriteLine("CONNECTING");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Debug, code = "06", message = "CONNECTING" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_Reconnecting(object sender, EventArgs e)
        {
            Console.WriteLine("RECONNECTING");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Error, code = "05", message = "RECONNECTING" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_Reconnected(object sender, EventArgs e)
        {
            Console.WriteLine("RECONNECTED");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Warning, code = "04", message = "RECONNECTED" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_Disconnecting(object sender, EventArgs e)
        {
            Console.WriteLine("DISCONNECTING");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Warning, code = "03", message = "DISCONNECTING" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_Disconnected(object sender, EventArgs e)
        {
            Console.WriteLine("DISCONNECTED");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Error, code = "02", message = "DISCONNECTED" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void OpcClient_BreakDetected(object sender, EventArgs e)
        {

            Console.WriteLine("BREAK DETECTED");

            var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Warning, code = "01", message = "BREAK DETECTED" };

            SendLogLevelMessage(logLevelMessage).Wait();
        }

        private static void HandleDataChangedMachineLineNode(object sender, OpcDataChangeReceivedEventArgs e)
        {
            var value = Convert.ToInt32(e.Item.Value.Value);

            Console.WriteLine($"ServerTimeStamp: {e.Item.Value.ServerTimestamp}\t {(sender as OpcMonitoredItem).NodeId.Value} Value: {value}");

            // SEND MESSAGE to CLOUD

            var wiseMessage =  new WiseMessage
            {
                deviceId = Address,
                timeStamp = DateTime.Now,
                node = (sender as OpcMonitoredItem).NodeId.Value.ToString(),
                value = value,
            };

            var jsonMessage = JsonConvert.SerializeObject(wiseMessage);

            var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

            using (var message = new Message(messageBytes))
            {
                message.ContentEncoding = "utf-8";
                message.ContentType = "application/json";

                message.Properties.Add("ContentEncodingX", "OpcUa+utf-8+application/json");

                ioTHubModuleClient.SendEventAsync("output1", message).Wait();

                var size = CalculateSize(messageBytes);

                Console.WriteLine($"Message with size {size} bytes sent.");
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
        private static LogLevelMessage.LogLevel MinimalLogLevel { get; set; } = DefaultMinimalLogLevel;

        private static async Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                Console.WriteLine("Empty desired properties ignored.");

                return;
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
                
                if (desiredProperties.Contains("minimalLogLevel"))
                {
                    if (desiredProperties["minimalLogLevel"] != null)
                    {
                        var minimalLogLevel = desiredProperties["minimalLogLevel"];

                        // casting from int to enum needed
                        var minimalLogLevelInteger = Convert.ToInt32(minimalLogLevel);

                        MinimalLogLevel = (LogLevelMessage.LogLevel)minimalLogLevelInteger;
                    }
                    else
                    {
                        MinimalLogLevel = DefaultMinimalLogLevel;
                    }

                    Console.WriteLine($"MinimalLogLevel changed to '{MinimalLogLevel}'");

                    reportedProperties["minimalLogLevel"] = MinimalLogLevel;
                }
                else
                {
                    Console.WriteLine($"MinimalLogLevel ignored");
                }

                if (reportedProperties.Count > 0)
                {
                    await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);

                    if (opcClient != null)
                    {
                        opcClient.Disconnect();

                        if (LicenseKey != string.Empty)
                        {
                            Opc.UaFx.Licenser.LicenseKey = LicenseKey;
                        }
                        else
                        {
                            Console.WriteLine("No license key available.");

                            Opc.UaFx.Licenser.LicenseKey = string.Empty;
                        }       
                                    
                        opcClient.ServerAddress = new Uri(Address);
                        opcClient.Connect();

                        var commands = new List<OpcSubscribeDataChange>();

                        if (!string.IsNullOrEmpty(NodePotentio1))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodePotentio1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodePotentio1");
                        }

                        if (!string.IsNullOrEmpty(NodePotentio2))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodePotentio2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodePotentio2");
                        }

                        if (!string.IsNullOrEmpty(NodeSwitch1))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodeSwitch1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodeSwitch1");
                        }

                        if (!string.IsNullOrEmpty(NodeSwitch2))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodeSwitch2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodeSwitch2");
                        }

                        if (!string.IsNullOrEmpty(NodeRelay1))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodeRelay1, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodeRelay1");
                        }

                        if (!string.IsNullOrEmpty(NodeRelay2))
                        {
                            commands.Add(new OpcSubscribeDataChange(NodeRelay2, OpcDataChangeTrigger.StatusValue, HandleDataChangedMachineLineNode));
                        }
                        else
                        {
                            System.Console.WriteLine("Ignored empty NodeRelay2");
                        }

                        OpcSubscription subscription = opcClient.SubscribeNodes(commands);

                        Console.WriteLine($"Client started... (listening to '{NodePotentio1},{NodePotentio2},{NodeSwitch1},{NodeSwitch2},{NodeRelay1},{NodeRelay2}' at '{Address}')");
                    }
                    else
                    {
                        Console.WriteLine("Client construction postponed.");
                    }

                    Console.WriteLine("Changes to desired properties can be enforced by restarting the module.");
                }
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Desired properties change error: {ex.Message}");

                var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Error, code = "98", message = $"Desired properties change error: {ex.Message}" };

                await SendLogLevelMessage(logLevelMessage);

                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine($"Error when receiving desired properties: {exception}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when receiving desired properties: {ex.Message}");

                var logLevelMessage = new LogLevelMessage { logLevel = LogLevelMessage.LogLevel.Error, code = "99", message = $"Error when receiving desired properties: {ex.Message}" };

                await SendLogLevelMessage(logLevelMessage);
            }
        }

        private static async Task SendLogLevelMessage(LogLevelMessage moduleStateMessage)
        {
            if (moduleStateMessage.logLevel < MinimalLogLevel)
            {
                return;
            }

            var jsonMessage = JsonConvert.SerializeObject(moduleStateMessage);

            var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

            using (var message = new Message(messageBytes))
            {
                message.ContentEncoding = "utf-8";
                message.ContentType = "application/json";
                message.Properties.Add("content-type", "application/opcua-error-json");

                await ioTHubModuleClient.SendEventAsync("outputError", message);

                var size = CalculateSize(messageBytes);

                Console.WriteLine($"Error message {moduleStateMessage.code} with size {size} bytes sent.");
            }
        }

        private static int CalculateSize(byte[] messageBytes)
        {
            using (var message = new Message(messageBytes))
            {
                message.ContentEncoding = "utf-8";
                message.ContentType = "application/json";
                message.Properties.Add("content-type", "application/opcua-error-json"); // not flexible

                var result = message.GetBytes().Length;

                foreach (var p in message.Properties)
                {
                    result = result + p.Key.Length + p.Value.Length;
                }

                return result;
            }
        }
    }
}