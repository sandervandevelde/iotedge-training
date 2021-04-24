# Access the Edge Adevice

*Note*: this is a VM in the Azure portal

Login into the Azure Portal

    portal.azure.com

Navigate to the Resourcegroup with the VMs

    rg-weu-fieldlab-iotedge-training-edge

Navigate to your own IoT Edge Device VM. See presentation. (In this Example we use VM0)

    vm-weu-fieldlab-iotedge-training-0

*Note*: see this is a Linux VM

If the VM is not started, press the Start option (The status field will change)

    The device is autmatically shut down at 19:00 to save costs.

See that the VM has a public IP address. (This address is normally dynamic. Here it is fixed for the demonstration)

    **Remember that IP address**

*Note* : See the status is changing to ‘Running’ if not already

Press Connect and select SSH

Copy the SSH line at 4

    ssh -i <private key path>

Alter this line into:

    ssh edgeadmin0@X.Y.Z.A

Run this command in a the Windows Powershell or a local dosbox on your machine 

    Windows button + R
    CMD
    Enter

Accept the connection if needed

Fill in the right password

    See presentation

See you are logged in.

Get the latest updates

    sudo apt udate
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

This takes a litthe time...

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

See in the Linked IoT hubs, the iot hub is connected.

Back in the Overview Menu, see the ScopeID

    **REMEMBER THE SCOPEID**

Go to Manage enrollments

At the bottom half, check the Individual Enrollments

    You device should not be there yet

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

We go back to the SSH connection with the edge Device

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

*Note*: Remove a # and a space at the start of each line

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

Restart the iot edge daemon service so the changes are picked up

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

Go to the IoT Hub in the same resourcegroup

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

You have now an edge Device

# Deploy a module from the portal

