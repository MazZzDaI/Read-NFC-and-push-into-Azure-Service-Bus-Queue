# AzureServiceBusNfc
Application communicates with NFC reader and send UID of NFC card to Azure Service Bus Queue

Manual:
1. Install PS/SC drivers for the NFC reader
  For example drivers for ACS devices could be downloaded here http://www.acs.com.hk/en/driver/4/acr38-smart-card-reader
2. Connect NFC reader to your PC and ensure that it is recognized by PC
3. Create new or use existing Azure Service Bus Namespace
  You can use manual https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal
4. Create new or use existing Azure Service Bus Queue without sessionning
  You can use manual https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dotnet-get-started-with-queues
5. Create new or use existing SAS Key with Send permission
6. Copy Primary Connection string for the SAS Key with Send permission
7. Paste Primary Connection string to App.config file to "Microsoft.ServiceBus.ConnectionString" area
8. Run application
