# Azure Hybrid Connections samples for Java

There are two sample applications that demonstrate the usage of Hybrid Connections API for exchanging text over web socket mode as well as HTTP mode from Java. The following are the steps required to run the sample apps:

1. Install JDK 1.8 or higher.
2. Install Apache Maven.
3. Clone or pull the latest library code [here](https://github.com/Azure/azure-relay-java).
4. In cmd, go to the project root of azure-relay-java, then run "mvn clean package".
5. Copy/paste over the "...-jar-with-depencies.jar" file under "/target" and place it under the "/lib" folder in the sample apps.
6. If haven't already, create a Relay namespace in Azure portal here, then create a Hybrid Connection instance inside under the "Entities" tab with "Requires Client Authentication" option checked.
7. Obtain a valid connection string from your hybrid connection instance, then set it as an environment variable with variable name as "RELAY_CONNECTION_STRING" and value as the entire connection string.
8. Run the sample listener app first, then run the sample sender app. Running the sender app without a running listener instance will result in an error.