# Access the Edge device

*Note*: this is a VM in the Azure portal

Login into the Azure Portal

    portal.azure.com

Navigate to the Resource group with the VMs

    rg-weu-fieldlab-iotedge-training-edge

Navigate to your own IoT Edge Device VM. See presentation. (In this Example we use VM0)

    vm-weu-fieldlab-iotedge-training-0

*Note*: see this is a Linux VM

If the VM is not started, press the Start option (The status field will change)

    The device is automatically shut down at 19:00 to save costs.

See that the VM has a public IP address. (This address is normally dynamic. Here it is fixed for the demonstration)

    **Remember that IP address**

*Note* : See the status is changing to ‘Running’ if not already

Press Connect and select SSH

Copy the SSH line at 4

    ssh -i <private key path>

Alter this line into:

    ssh edgeadmin0@X.Y.Z.A

Run this command in at the Windows Powershell or a local DOSBox on your machine 

    Windows button + R
    CMD
    Enter

Accept the connection if needed

Fill in the right password

    See presentation

See you are logged in.

Get the latest updates

    sudo apt update
    sudo apt upgrade

*Note*: When asked for the Superuser password, fill in the password already provided

stop auto update on OS

    sudo nano /etc/apt/apt.conf.d/20auto-upgrades

Alter text to (set 1 to 0 in second line):

    APT::Periodic::Update-Package-Lists "1";
    APT::Periodic::Unattended-Upgrade "0";

Save and Exit the Nano editor

    CTRL-S
    CTRL-X

Install the Moby container engine 

    curl https://packages.microsoft.com/config/ubuntu/20.04/prod.list > ./microsoft-prod.list
    sudo cp ./microsoft-prod.list /etc/apt/sources.list.d/
    sudo curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
    sudo cp ./microsoft.gpg /etc/apt/trusted.gpg.d/ 
    sudo apt update
    sudo apt install moby-engine

Accept the size of the update

    y

This takes a little time...

Install IoT Edge 1.1.1

    sudo curl -L https://github.com/Azure/azure-iotedge/releases/download/1.1.1/libiothsm-std_1.1.1-1-1_debian9_amd64.deb -o libiothsm-std.deb && sudo dpkg -i ./libiothsm-std.deb 
    sudo curl -L https://github.com/Azure/azure-iotedge/releases/download/1.1.1/iotedge_1.1.1-1_debian9_amd64.deb -o iotedge.deb && sudo dpkg -i ./iotedge.deb


Reboot the VM

    sudo reboot

The machine will reboot now. You have to use SSH once again to get in again

Once logged in, Check the IoT Edge runtime version

iotedge version

Check the current (missing) credentials for this edge device

    sudo nano /etc/iotedge/config.yaml


See that the default way for credentials is:

    # Manual provisioning with an IoT Hub connection string (SharedAccessKey authentication only)

What we want to use is

    # DPS provisioning with symmetric key attestation

*Note*: In production, a TPM or X509 Cert is used 

We leave this for now. Go back to the browser.

# Create device Credentials

Go to the Azure portal

    portal.azure.com

Go to the core resource group

    rg-weu-fieldlab-iotedge-training-core

See it contains both an IoT Hub and Device Provisioning Service

Go to the Device provisioning service

    dps-weu-fieldlab-iotedge-training

See in the Linked IoT hubs, the IoT Hub is connected.

Back in the Overview Menu, see the ScopeID

    **REMEMBER THE SCOPEID**

Go to Manage enrollments

At the bottom half, check the Individual Enrollments

    Your device should not be there yet

Add an individual enrollment

    Mechanism: symmetric key

    Registration ID: regDEvice0

    IoT Hub Device: device0

    IoT Edge device: True

    Initial Twin State:
    {
        "tags": {
            "enrolled": true,
            "companyId": "FieldLab",
            "tpm": false
        },
        "properties": {
            "desired": {
            "simulation": true
            }
        }
    }

Save the Enrollment

Open the enrollment

    **Remember Registration ID**

    **Remember the primary key**


See there is no registration yet!

    Keep this page open

We go back to the SSH connection with the Edge Device

    Check if the config.yaml file is still open

We are about to change the configuration (Check the correct indentation usage and the #!!!)

Fill in the three values for # DPS provisioning with symmetric key attestation

    # DPS provisioning with symmetric key attestation
    provisioning:
    source: "dps"
    global_endpoint: "https://global.azure-devices-provisioning.net"
    scope_id: "0ne0028B99A"
    attestation:
        method: "symmetric_key"
        registration_id: "regDevice0"
        symmetric_key: "VArsyS5l32M96EEGYR70awcslxHnhnuTMSed+NTZs8ThQQXgyopbiuW6Kk/03KlRHJ3yn2Um1j9FhalSujekHg=="
    always_reprovision_on_startup: true
    dynamic_reprovisioning: false

*Note*: Remove the # and the space at the start of each line

Disable the original (still default) registration

    # Manual provisioning with an IoT Hub connection string (SharedAccessKey authentication only)
    #provisioning:
    #  source: "manual"
    #  device_connection_string: "<ADD DEVICE CONNECTION STRING HERE>"
    #  dynamic_reprovisioning: false

*Note*: Add a # in front of each line

Save and close the file

    CTRL-S
    CTRL-X

Restart the IoT Edge daemon service so the changes are picked up

    sudo systemctl restart iotedge

Check the progress on restarting

    sudo systemctl status iotedge

This should result in an active service:

    edgeadmin0@vm-weu-fieldlab-iotedge-training-0:~$
    edgeadmin0@vm-weu-fieldlab-iotedge-training-0:~$ iotedge version
    iotedge 1.1.1
    edgeadmin0@vm-weu-fieldlab-iotedge-training-0:~$ sudo nano /etc/iotedge/config.yaml
    edgeadmin0@vm-weu-fieldlab-iotedge-training-0:~$ sudo systemctl restart iotedge
    edgeadmin0@vm-weu-fieldlab-iotedge-training-0:~$ sudo systemctl status iotedge
    ● iotedge.service - Azure IoT Edge daemon
        Loaded: loaded (/lib/systemd/system/iotedge.service; enabled; vendor preset: enabled)
        Active: active (running) since Mon 2021-04-19 21:05:55 UTC; 27s ago

The service is now checking the DPS for credentials. If this works the IoT Edge will start downloading the edgeAgent module into Moby

Go to the portal in the browser

Check in the portal the device is now having the status “assigned”. See also the IoT Hub is related to the device

Go to the IoT Hub in the same resource group

    ih-weu-fieldlab-iotedge-training

In menu IoT Edge, see your device is represented  

See the status (runtime response) is not ‘ok’

    Yes, it has status 417 (missing deployment manifest)

See the $edgeAgent is running by now

In the ‘Device Twin’ dialog, You can find the tags we gave it

    Click the ‘Device Twin’ menu button
    Close the dialog with X to go back.

Go to the SSH screen

See if the edgeAgent module is downloaded and started (This should return a list of all modules):

    sudo iotedge list

See the logging of the edgeAgent

    sudo iotedge logs -f edgeAgent

See the edgeAgent is started

    The local routing mechanism certificate will expire in 90 days
    It connects to the Cloud using the AMQP protocol
    See it complains about a missing deployment manifest

*Note*: this is a live capture of the logging. You can stop it with CTRL-C

You have now an Edge Device

# Deploy a module from the portal

Go to the Azure portal

    portal.azure.com

Go to your device registration in the IoT Hub

Select ‘Set modules’ (This will start a full deployment)

We are going to add a module from the Azure IoT Edge Module marketplace

Click *+Add* in the IoT Edge Modules section

Click *+Marketplace module*

A dialog opens (More than 50 modules are selectable)

Fill into the query: 

    temperature

hit enter

The ‘Simulated Temperature Sensor’ module is shown

Select it

See the module is added to the deployment

Click on the name to open the details

See the name and image URI

    **Remember the name**

Gave the module this environment variable so it keeps sensing messages (normally it stops after 500 messages)

MessageCount = -1

Click *add (or Update)*

We now add another module, a public module

Click *+Add* in the IoT Edge Modules section

Click *+IoT Edge module*

A dialog opens

Fill in

    Name: hb
    Image Uri: iotedgefoundation/iot-edge-heartbeat:3.0.2-amd64

This is a [heartbeat module](https://hub.docker.com/r/iotedgefoundation/iot-edge-heartbeat/tags?page=1&ordering=last_updated)

Give it a desired property to slow down the generation of heartbeats

Change the module twin settings

    {
        "interval": 60000
    }

Click *Add (or update)*

Click *Next: Routes*

Remove the route named (This is added by the wizard. It overlaps with the original route)

    SimulatedTemperatureSensorToIoTHub

Click *Review & Create*

We now see the complete Deployment Manifest!

Click *Create*

The deployment manifest is set ready and is now picked up by the device

# See the arrival of the module

Go to the device SSH 

See the logging of the edgeAgent

    sudo iotedge logs -f edgeAgent

You will see the download of the two modules at the end followed by the update of the module twin reported properties

*Note*: If you see errors occurs, stop the log generation and try to figure out what happens

See if the modules are started and running:

    sudo iotedge list

Check the log of each module

    sudo iotedge logs -f hb

    sudo iotedge logs -f SimulatedTemperatureSensor

See that each module generated its own messages

Install the IoT Explorer

    Download and install the [MSI] (https://github.com/Azure/azure-iot-explorer/releases)

Before we can see cloud ingested data, we need the connection string from the IoT Hub

Go to the portal

    portal.azure.com

Go to the IoT Hub

    ih-weu-fieldlab-iotedge-training

Go to the “Built-in endpoints” dialog

See there is a consumer group just for you

    explorerX (replace with your number)

This way, nobody is ‘stealing’ the messages from another developer

    **Remember your consumer group name**

Go to the “shared access policies” dialog

Select the ‘iothubowner’

*Warning*: You are about to copy the most important key of the IoT Hub. Do not lose it!!!

In the dialog copy the 
    
    “Connection string—primary key”

    **Remember it**

Now we have the IoT Hub connection string!

Start the IoT Explorer tool which shows IoT Hub device information

Select *Add Connection* 

Take the connection string from the IoT Hub

    Fill in the connection string 

Hit Save

Your IoT Hub is now queriable

See your device is shown in the device list

Select your device

Select Telemetry

Fill in your consumer group in the field Consumer Group

Hit Start

See the messages arriving in the IoT Explorer and therefore these messages are actually arriving in the IoT Hub

You are now able to add pre-configured modules to an IoT Edge device and consume the messages

# Create your own module

Open VS Code

*Note*: Is Your docker already running?

Press F1

    A list of wizards is shown

Check out the Azure IoT Edge wizards like

    Azure IoT Edge: New IoT Edge Solution

Click on it. This starts the wizard

Select a folder on your disk

    I recommend “c:\git”

Accept that folder

A name for the solution is suggested

Keep the name 

    ‘EdgeSolution’

A list of possible modules is suggested

Select the C# module

A name for the module is suggested

Extend the name with your personal VM number! 

    ‘SampleModuleX’

A URI for the module image is suggested

Keep the current URI 

    “localhost:5000/samplemoduleX”

*Note*: This is the local container repo on your machine

The code is now generated for your module

See that a folder with an application program.cs is created 

*Important* A suggestion is made by VS Code:

    “Required assets to build and debug are missing”

    Accept with YES

    Rebuild the code with “CTRL-SHFT-B”

    Select “Build”

Your code should recompile successfully

See In program.cs 

    it ingests routed messages using input ‘input1’
    it outputs routed messages into output ‘output1’

You now have 'programmed' a module capable of ingesting messages coming from another module and putting it back on the route!

In module.json, we see the URI name and image version. 

    Ignore the first version which describes the version of this document layout

We see also the possible platforms (Linux, Windows / Arm, Intel) supported. If needed you build and push one container for each platform.

*Note*: We stick to ./Dockerfile.amd64 for default Linux support as seen in the VM

Before we can build and push this module, we need to have access to our own container registry, available in the Azure portal

# Container registry administration

Go to the Azure Portal

    Portal.azure.com

In the core resource group, we see the container registry

    Crweufieldlabiotedgetraining

In the “Access keys” menu remember the values

    **REMEMBER Login server, Username, and password**

Go to VS Code

Open the Terminal (Via Menu | View | Terminal)

Type in and run

    docker login crweufieldlabiotedgetraining.azurecr.io 

You are asked for the name and password

    type or paste it

You will get a message *the Login succeeded* 

We have to add this so VS Code can build and push to the container repository on your development device.

*Note*: you need the credentials later on again

# Build and push

You are now ready to start building the module and pushing it

Go to the module.json file

Change the container URI. Replace localhost:5000 by the “Login server” name of our container registry

    crweufieldlabiotedgetraining.azurecr.io/samplemoduleX

Save the module.json file

Right-click module.json, select the last menu list item popping up

    Build and Push IoT Edge module Image

A list of possible Operating Systems and Hardware configurations is shown (This is the same list as available in the module.json file)

    Select AMD64

In the Terminal, you see the progress of the build and push of your module

*Note*: The first time this will take some time due to the download of the master container or new module is based on

See the module is pushed also to the container registry

In the Azure portal, go to the container registry

Select the “Repositories” menu

See the appearance of the module

Drill down into the module

See the name

    docker pull crweufieldlabiotedgetraining.azurecr.io/samplemoduleX:0.0.1-amd64

strip this into

    crweufieldlabiotedgetraining.azurecr.io/samplemoduleX:0.0.1-amd64

    **REMEMBER your own image URI**

You have created your own module.

Let’s consume it!

Go to the portal

    portal.azure.com

Go to the IoT Hub

    ih-weu-fieldlab-iotedge-training

Go to your IoT Edge device

Start the deployment manifest update using

    The ‘Set Modules’ menu 

In “Container Registry Credentials”, Fill in the ACR credentials

    Name: cr
    Address: crweufieldlabiotedgetraining.azurecr.io
    Username: crweufieldlabiotedgetraining
    Password: the pwd

Click *+Add* in the IoT Edge Modules section

Click *+IoT Edge module*

A dialog opens

Fill in

    Name: sample

    Image Uri: crweufieldlabiotedgetraining.azurecr.io/samplemoduleX:0.0.1-amd64    (the image URI you copied earlier)

Click *add (or Update)*

Select *Next: routes*

Replace all routes by these three new routes:

    Sample2cloud:
    FROM /messages/modules/sample/outputs/output1 INTO $upstream

    Heartbeat2cloud:
    FROM /messages/modules/hb/outputs/output1 INTO $upstream

    Simulation2Sample: 
    FROM /messages/modules/SimulatedTemperatureSensor/* INTO BrokeredEndpoint("/modules/sample/inputs/input1")

Only the Sample and Heartbeat modules are allowed to send messages to the cloud. Simulated messages are sent to the Sample module 

Select *Review + Create*

Select *Create*

See the arrival of the sample module in the edgeAgent logging

    sudo iotedge logs -f edgeAgent

See the sample module appearing in the IoT Edge list

    sudo iotedge list

Check the log of the sample module

    sudo iotedge logs -f sample

See how the messages from the simulation flow into the sample module and there are outputted to the cloud

    See the IoT Explorer

You have successfully created and deployed your first module.

Inspired? check out https://github.com/iot-edge-foundation and http://blog.vandevelde-online.com/ for more module examples.



