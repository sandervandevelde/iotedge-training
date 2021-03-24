namespace WiseOpcUaClientModule
{
    using System;

    public class WiseMessage
    {
        public string deviceId { get; set; }

        public string node { get; set; }

        public int value { get; set; }

        public DateTime timeStamp { get; set; }
    }
}